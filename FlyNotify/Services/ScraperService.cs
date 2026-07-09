using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using HtmlAgilityPack;
using FlyNotify.Models;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace FlyNotify.Services
{
    /*
        Thread-safe background asynchronous network scraper service responsible
        for pulling award seats down and processing capacity string tokens.
    */
    public static partial class ScraperService
    {
        private static readonly HttpClient Client = new();

        // Flag to toggle between live HTTP scraping and local debug HTML mock data files.
        public static bool UseMockData { get; set; } = true;

        static ScraperService()
        {
            Client.Timeout = TimeSpan.FromSeconds(30);
            Client.DefaultRequestVersion = System.Net.HttpVersion.Version20;
            Client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;

            /*
                Seed realistic header values to emulate a standard Chromium desktop client profile.
            */
            Client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            Client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            Client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            Client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            Client.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"");
            Client.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
            Client.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            Client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            Client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            Client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
            Client.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
        }

#pragma warning disable SYSLIB1045, IDE0290
        private static readonly Regex HourRegex = new(@"(\d+)\s*hour");
        private static readonly Regex MinRegex = new(@"(\d+)\s*min");
#pragma warning restore SYSLIB1045, IDE0290

        private static string CleanAstroPropsJson(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() == 2 && 
                element[0].ValueKind == JsonValueKind.Number)
            {
                return CleanAstroPropsJson(element[1]);
            }
            
            if (element.ValueKind == JsonValueKind.Array)
            {
                var sb = new StringBuilder("[");
                bool first = true;
                foreach (var item in element.EnumerateArray())
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append(CleanAstroPropsJson(item));
                }
                sb.Append("]");
                return sb.ToString();
            }
            
            if (element.ValueKind == JsonValueKind.Object)
            {
                var sb = new StringBuilder("{");
                bool first = true;
                foreach (var prop in element.EnumerateObject())
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append(JsonSerializer.Serialize(prop.Name));
                    sb.Append(":");
                    sb.Append(CleanAstroPropsJson(prop.Value));
                }
                sb.Append("}");
                return sb.ToString();
            }
            
            return JsonSerializer.Serialize(element);
        }

        private static JsonElement GetUnwrappedProperty(JsonElement element, string name)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var prop))
            {
                return Unwrap(prop);
            }
            return default;
        }

        private static JsonElement Unwrap(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() == 2)
            {
                return element[1];
            }
            return element;
        }

        private static bool IsRegionMatch(string destAirport, string targetRegion, Dictionary<string, string> airportToRegion)
        {
            if (destAirport.Equals(targetRegion, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (airportToRegion.TryGetValue(destAirport, out var regionCode))
            {
                if (regionCode.Equals(targetRegion, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Handle macro wildcards
                if (targetRegion.Equals("AU", StringComparison.OrdinalIgnoreCase) && regionCode.Equals("OC", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                if (targetRegion.Equals("US", StringComparison.OrdinalIgnoreCase) && 
                    (regionCode.Equals("WN", StringComparison.OrdinalIgnoreCase) || 
                     regionCode.Equals("CN", StringComparison.OrdinalIgnoreCase) || 
                     regionCode.Equals("EN", StringComparison.OrdinalIgnoreCase) || 
                     regionCode.Equals("SP", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        /*
            Main asynchronous processing worker thread pipeline loop.
        */
        public static async Task<List<FlightProfile>> ExecuteScrapeAsync(FlightProfile profile, Action<string>? progressCallback = null)
        {
            string targetFlightNumber = profile.FlightNumber;
            progressCallback?.Invoke("Searching...");

            var discoveredProfiles = new List<FlightProfile>();

            try
            {
                string htmlContent;
                if (UseMockData)
                {
                    string debugPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "FlyNotify",
                        "qantas.html"
                    );
                    htmlContent = await System.IO.File.ReadAllTextAsync(debugPath);
                }
                else
                {
                    /*
                        Verify and load Chromium browser from the common application AppData directory.
                    */
                    var storageDirectory = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "FlyNotify"
                    );

                    var fetcherOptions = new PuppeteerSharp.BrowserFetcherOptions
                    {
                        Path = storageDirectory
                    };
                    var fetcher = new PuppeteerSharp.BrowserFetcher(fetcherOptions);

                    string executablePath;
                    var installedBrowser = fetcher.GetInstalledBrowsers().FirstOrDefault();
                    if (installedBrowser == null)
                    {
                        progressCallback?.Invoke("Downloading browser...");
                        installedBrowser = await fetcher.DownloadAsync();
                        progressCallback?.Invoke("Searching...");
                    }
                    executablePath = installedBrowser.GetExecutablePath();

                    var options = new PuppeteerSharp.LaunchOptions
                    {
                        Headless = true,
                        ExecutablePath = executablePath,
                        Args = new[] 
                        { 
                            "--no-sandbox", 
                            "--disable-setuid-sandbox",
                            "--disable-gpu",
                            "--disable-dev-shm-usage" 
                        }
                    };

                    using var browser = await PuppeteerSharp.Puppeteer.LaunchAsync(options);
                    using var page = await browser.NewPageAsync();

                    await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                    string targetUrl = profile.BuildQantasQueryUrl();
                    await page.GoToAsync(targetUrl, new PuppeteerSharp.NavigationOptions
                    {
                        WaitUntil = new[] { PuppeteerSharp.WaitUntilNavigation.Networkidle2 }
                    });

                    htmlContent = await page.GetContentAsync();
                }

                var htmlDoc = new HtmlAgilityPack.HtmlDocument();
                htmlDoc.LoadHtml(htmlContent);
                
                var astroIsland = htmlDoc.DocumentNode.SelectSingleNode("//astro-island[contains(@component-url, 'SearchApp')]");
                if (astroIsland == null)
                {
                    throw new Exception("The flights application module ('SearchApp') was not found in the page layout. This can occur if the Qantas site structure changed, or if the request was blocked/rate-limited by Cloudflare or standard security policies.");
                }

                string encodedProps = astroIsland.GetAttributeValue("props", "");
                if (string.IsNullOrEmpty(encodedProps))
                {
                    throw new Exception("The flight search payload attributes were missing or empty in the page layout.");
                }

                string decodedProps = System.Net.WebUtility.HtmlDecode(encodedProps);

                var airportToRegionCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                using var propsDoc = JsonDocument.Parse(decodedProps);

                if (propsDoc.RootElement.TryGetProperty("airports", out var airportsVal))
                {
                    var airportsArray = Unwrap(airportsVal);
                    if (airportsArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var entry in airportsArray.EnumerateArray())
                        {
                            var apt = Unwrap(entry);
                            string code = GetUnwrappedProperty(apt, "code").GetString() ?? "";
                            string regionCode = GetUnwrappedProperty(apt, "regionCode").GetString() ?? "";
                            if (!string.IsNullOrEmpty(code))
                            {
                                airportToRegionCode[code] = regionCode;
                            }
                        }
                    }
                }

                var initialData = GetUnwrappedProperty(propsDoc.RootElement, "initialData");
                if (initialData.ValueKind == JsonValueKind.Undefined)
                {
                    throw new Exception("The initial search data block was not found inside the page layout payload.");
                }

                var okObj = GetUnwrappedProperty(initialData, "ok");
                if (okObj.ValueKind == JsonValueKind.Undefined)
                {
                    throw new Exception("The search status details were not found inside the page layout payload.");
                }

                var rawFlights = GetUnwrappedProperty(okObj, "flights");
                if (rawFlights.ValueKind != JsonValueKind.Array)
                {
                    throw new Exception("The search flights segment is not in a valid layout format.");
                }

                string cleanJson = CleanAstroPropsJson(rawFlights);

                using var doc = JsonDocument.Parse(cleanJson);
                var flightsArray = doc.RootElement;

                var executionTierOrder = new[]
                {
                    CabinClasses.First,
                    CabinClasses.Business,
                    CabinClasses.PremiumEconomy,
                    CabinClasses.Economy
                };

                foreach (var flight in flightsArray.EnumerateArray())
                {
                    if (flight.TryGetProperty("origin", out var originObj) && originObj.TryGetProperty("code", out var originCode) &&
                        flight.TryGetProperty("destination", out var destObj) && destObj.TryGetProperty("code", out var destCode))
                    {
                        string origin = originCode.GetString() ?? "";
                        string dest = destCode.GetString() ?? "";

                        bool destMatches = profile.ArrivalAirport.Equals("ALL", StringComparison.OrdinalIgnoreCase) ||
                                           IsRegionMatch(dest, profile.ArrivalAirport, airportToRegionCode);

                        if (origin.Equals(profile.DepartureAirport, StringComparison.OrdinalIgnoreCase) && destMatches)
                        {
                            if (flight.TryGetProperty("departsAt", out var departsAtProp))
                            {
                                string departsAt = departsAtProp.GetString() ?? "";
                                if (DateTime.TryParse(departsAt, out DateTime flightDate) && flightDate.Date == profile.TravelDate.Date)
                                {
                                    string flightNo = "QF000";
                                    if (flight.TryGetProperty("legs", out var legsProp) && legsProp.ValueKind == JsonValueKind.Array && legsProp.GetArrayLength() > 0)
                                    {
                                        var firstLeg = legsProp[0];
                                        if (firstLeg.TryGetProperty("flightNumber", out var fNoProp))
                                        {
                                            flightNo = fNoProp.GetString() ?? "QF000";
                                        }
                                    }

                                    if (!string.IsNullOrEmpty(targetFlightNumber) &&
                                        targetFlightNumber != "TBD" &&
                                        targetFlightNumber != "TDB" &&
                                        targetFlightNumber != "QF000" &&
                                        !flightNo.Equals(targetFlightNumber, StringComparison.OrdinalIgnoreCase))
                                    {
                                        continue;
                                    }

                                    string departsTimeStr = "--:--";
                                    DateTime depTime = DateTime.MinValue;
                                    if (flight.TryGetProperty("departsAt", out var depAtProp) && DateTime.TryParse(depAtProp.GetString(), out depTime))
                                    {
                                        departsTimeStr = depTime.ToString("HH:mm");
                                    }

                                    string arrivesTimeStr = "--:--";
                                    if (flight.TryGetProperty("arrivesAt", out var arrAtProp) && DateTime.TryParse(arrAtProp.GetString(), out DateTime arrTime))
                                    {
                                        arrivesTimeStr = arrTime.ToString("HH:mm");
                                        if (depTime != DateTime.MinValue)
                                        {
                                            int offsetDays = (arrTime.Date - depTime.Date).Days;
                                            if (offsetDays > 0)
                                            {
                                                arrivesTimeStr += $"+{offsetDays}";
                                            }
                                            else if (offsetDays < 0)
                                            {
                                                arrivesTimeStr += $"{offsetDays}";
                                            }
                                        }
                                    }

                                    string durationStr = "0:00";
                                    if (flight.TryGetProperty("duration", out var durProp))
                                    {
                                        string rawDuration = durProp.GetString() ?? "";
                                        int hours = 0;
                                        int minutes = 0;

                                        var hrMatch = HourRegex.Match(rawDuration);
                                        if (hrMatch.Success)
                                        {
                                            hours = int.Parse(hrMatch.Groups[1].Value);
                                        }

                                        var minMatch = MinRegex.Match(rawDuration);
                                        if (minMatch.Success)
                                        {
                                            minutes = int.Parse(minMatch.Groups[1].Value);
                                        }

                                        durationStr = $"{hours}:{minutes:D2}";
                                    }

                                    var statusBuilder = new StringBuilder();
                                    bool hasAvailableSeats = false;

                                    if (flight.TryGetProperty("cabins", out var cabinsObj) && cabinsObj.ValueKind == JsonValueKind.Object)
                                    {
                                        foreach (var cabinTier in executionTierOrder)
                                        {
                                            if (profile.SelectedCabins.HasFlag(cabinTier))
                                            {
                                                string jsonKey = cabinTier switch
                                                {
                                                    CabinClasses.Economy => "Economy",
                                                    CabinClasses.PremiumEconomy => "PremiumEconomy",
                                                    CabinClasses.Business => "Business",
                                                    CabinClasses.First => "First",
                                                    _ => ""
                                                };

                                                int seats = 0;
                                                if (!string.IsNullOrEmpty(jsonKey) && cabinsObj.TryGetProperty(jsonKey, out var cabinInfo) && cabinInfo.ValueKind == JsonValueKind.Object)
                                                {
                                                    if (cabinInfo.TryGetProperty("seats", out var seatsProp) && seatsProp.ValueKind == JsonValueKind.Number)
                                                    {
                                                        seats = seatsProp.GetInt32();
                                                    }
                                                }

                                                if (seats > 0)
                                                {
                                                    hasAvailableSeats = true;
                                                }

                                                int finalDisplayCount = seats >= 5 ? 5 : seats;
                                                if (statusBuilder.Length > 0)
                                                {
                                                    statusBuilder.Append(' ');
                                                }
                                                statusBuilder.Append($"{cabinTier.ToFareBucketCode()}{finalDisplayCount}");
                                            }
                                        }
                                    }

                                    /*
                                        Skip matching wildcard flight options that contain zero available seats.
                                    */
                                    if (profile.ArrivalAirport.Equals("ALL", StringComparison.OrdinalIgnoreCase) && !hasAvailableSeats)
                                    {
                                        continue;
                                    }

                                    string finalStatusOutput = statusBuilder.Length > 0 ? statusBuilder.ToString() : "No Classes Found";

                                    var matchedProfile = new FlightProfile
                                    {
                                        DepartureAirport = origin,
                                        ArrivalAirport = dest,
                                        TravelDate = profile.TravelDate,
                                        TravelEndDate = profile.TravelEndDate,
                                        PassengerCount = profile.PassengerCount,
                                        SelectedCabins = profile.SelectedCabins,
                                        FlightNumber = flightNo,
                                        DepartureTime = departsTimeStr,
                                        ArrivalTime = arrivesTimeStr,
                                        Duration = durationStr,
                                        AvailabilityStatus = finalStatusOutput,
                                        LastChecked = DateTime.Now
                                    };

                                    discoveredProfiles.Add(matchedProfile);
                                }
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Scraper Exception Core]: {ex.Message}");
                progressCallback?.Invoke("Error occurred.");

                /*
                    Notify user of scrape failure details via a standard system dialog.
                */
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    MessageBox.Show(
                        $"A scraper error occurred while processing the route {profile.DepartureAirport} -> {profile.ArrivalAirport}.\n\n" +
                        $"Error Type: {ex.GetType().Name}\n" +
                        $"Message: {ex.Message}\n\n" +
                        $"Stack Trace:\n{ex.StackTrace}",
                        "Scraper Execution Failure",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                });
            }

            return discoveredProfiles;
        }
    }
}
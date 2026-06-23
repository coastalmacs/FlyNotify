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
        private static readonly Regex NextJsPayloadRegex = new("self\\.__next_f\\.push\\(\\s*\\[\\s*\\d+\\s*,\\s*\"([^\"]*(?:\\\\.[^\"]*)*)\"\\s*\\]\\s*\\)", RegexOptions.Singleline);
        private static readonly Regex HourRegex = new(@"(\d+)\s*hour");
        private static readonly Regex MinRegex = new(@"(\d+)\s*min");
#pragma warning restore SYSLIB1045, IDE0290

        private static string ReconstructNextJsPayload(string html)
        {
            var sb = new StringBuilder();
            var regex = NextJsPayloadRegex;
            var matches = regex.Matches(html);

            foreach (Match match in matches)
            {
                string escapedVal = match.Groups[1].Value;
                try
                {
                    string unescaped = Regex.Unescape(escapedVal);
                    sb.Append(unescaped);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NextJS Segment Unescape Failure]: {ex.Message}");
                }
            }

            return sb.ToString();
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
                        WaitUntil = new[] { PuppeteerSharp.WaitUntilNavigation.DOMContentLoaded }
                    });

                    htmlContent = await page.GetContentAsync();
                }

                string payload = ReconstructNextJsPayload(htmlContent);

                // Find "flights":[ array
                int flightsIndex = payload.IndexOf("\"flights\":[");
                if (flightsIndex < 0)
                {
                    throw new Exception("The flights data array ('\"flights\":[') was not found in the page payload. This can occur if the Qantas site structure changed, or if the request was blocked/rate-limited by Cloudflare or standard security policies.");
                }

                // Extract substring starting at [
                int startBracketIndex = flightsIndex + 10; // starts at '['
                string searchSub = payload[startBracketIndex..];

                // Find matching closing bracket for the array
                int paginationIndex = searchSub.IndexOf(",\"pagination\"");
                if (paginationIndex < 0)
                {
                    throw new Exception("The closing pagination identifier (',\"pagination\"') was not found in the payload, making it impossible to extract flights JSON segment.");
                }

                string flightsJsonArray = searchSub[..paginationIndex];

                // Clean backslash-escaped quotes inside JSON array
                string cleanJson = flightsJsonArray.Replace("\\\"", "\"");

                // Parse JSON array
                using var doc = JsonDocument.Parse(cleanJson);
                var flightsArray = doc.RootElement;
                if (flightsArray.ValueKind != JsonValueKind.Array)
                {
                    throw new Exception("The parsed flights JSON payload is not a valid JSON Array structure.");
                }

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
                                           dest.Equals(profile.ArrivalAirport, StringComparison.OrdinalIgnoreCase);

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
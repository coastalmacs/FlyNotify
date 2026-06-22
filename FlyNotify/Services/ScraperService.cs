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

namespace FlyNotify.Services
{
    /*
        Thread-safe background asynchronous network scraper service responsible
        for pulling award seats down and processing capacity string tokens.
    */
    public static partial class ScraperService
    {
        private static readonly HttpClient Client = new();

        static ScraperService()
        {
            Client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            Client.Timeout = TimeSpan.FromSeconds(30);
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
        public static async Task ExecuteScrapeAsync(FlightProfile profile)
        {
            UpdateProfileStatus(profile, "Searching...", "TBD", "TBD", "TBD", "TBD");

            try
            {
                // string targetUrl = profile.BuildQantasQueryUrl();
                // string htmlContent = await Client.GetStringAsync(targetUrl);
                string debugPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FlyNotify",
                    "qantas.html"
                );
                string htmlContent = await System.IO.File.ReadAllTextAsync(debugPath);

                string payload = ReconstructNextJsPayload(htmlContent);

                // Find "flights":[ array
                int flightsIndex = payload.IndexOf("\"flights\":[");
                if (flightsIndex < 0)
                {
                    UpdateProfileStatus(profile, "TBD", "TBD", "TBD", "TBD", "TBD");
                    return;
                }

                // Extract substring starting at [
                int startBracketIndex = flightsIndex + 10; // starts at '['
                string searchSub = payload[startBracketIndex..];

                // Find matching closing bracket for the array
                int paginationIndex = searchSub.IndexOf(",\"pagination\"");
                if (paginationIndex < 0)
                {
                    UpdateProfileStatus(profile, "Parse Error (End)", "TBD", "TBD", "TBD", "TBD");
                    return;
                }

                string flightsJsonArray = searchSub[..paginationIndex];

                // Clean backslash-escaped quotes inside JSON array
                string cleanJson = flightsJsonArray.Replace("\\\"", "\"");

                // Parse JSON array
                using var doc = JsonDocument.Parse(cleanJson);
                var flightsArray = doc.RootElement;
                if (flightsArray.ValueKind != JsonValueKind.Array)
                {
                    UpdateProfileStatus(profile, "Parse Error (Array)", "TBD", "TBD", "TBD", "TBD");
                    return;
                }

                var executionTierOrder = new[]
                {
                    CabinClasses.First,
                    CabinClasses.Business,
                    CabinClasses.PremiumEconomy,
                    CabinClasses.Economy
                };

                JsonElement? matchedFlight = null;

                foreach (var flight in flightsArray.EnumerateArray())
                {
                    if (flight.TryGetProperty("origin", out var originObj) && originObj.TryGetProperty("code", out var originCode) &&
                        flight.TryGetProperty("destination", out var destObj) && destObj.TryGetProperty("code", out var destCode))
                    {
                        string origin = originCode.GetString() ?? "";
                        string dest = destCode.GetString() ?? "";

                        if (origin.Equals(profile.DepartureAirport, StringComparison.OrdinalIgnoreCase) &&
                            dest.Equals(profile.ArrivalAirport, StringComparison.OrdinalIgnoreCase))
                        {
                            if (flight.TryGetProperty("departsAt", out var departsAtProp))
                            {
                                string departsAt = departsAtProp.GetString() ?? "";
                                if (DateTime.TryParse(departsAt, out DateTime flightDate) && flightDate.Date == profile.TravelDate.Date)
                                {
                                    matchedFlight = flight;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (matchedFlight == null)
                {
                    UpdateProfileStatus(profile, "TBD", "TBD", "TBD", "TBD", "TBD");
                    return;
                }

                var flightObj = matchedFlight.Value;
                string flightNo = "QF000";
                if (flightObj.TryGetProperty("legs", out var legsProp) && legsProp.ValueKind == JsonValueKind.Array && legsProp.GetArrayLength() > 0)
                {
                    var firstLeg = legsProp[0];
                    if (firstLeg.TryGetProperty("flightNumber", out var fNoProp))
                    {
                        flightNo = fNoProp.GetString() ?? "QF000";
                    }
                }

                string departsTimeStr = "--:--";
                DateTime depTime = DateTime.MinValue;
                if (flightObj.TryGetProperty("departsAt", out var depAtProp) && DateTime.TryParse(depAtProp.GetString(), out depTime))
                {
                    departsTimeStr = depTime.ToString("HH:mm");
                }

                string arrivesTimeStr = "--:--";
                if (flightObj.TryGetProperty("arrivesAt", out var arrAtProp) && DateTime.TryParse(arrAtProp.GetString(), out DateTime arrTime))
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
                if (flightObj.TryGetProperty("duration", out var durProp))
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
                if (flightObj.TryGetProperty("cabins", out var cabinsObj) && cabinsObj.ValueKind == JsonValueKind.Object)
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

                            int finalDisplayCount = seats >= 5 ? 5 : seats;
                            if (statusBuilder.Length > 0)
                            {
                                statusBuilder.Append(' ');
                            }
                            statusBuilder.Append($"{cabinTier.ToFareBucketCode()}{finalDisplayCount}");
                        }
                    }
                }

                string finalStatusOutput = statusBuilder.Length > 0 ? statusBuilder.ToString() : "No Classes Found";
                UpdateProfileStatus(profile, finalStatusOutput, flightNo, departsTimeStr, arrivesTimeStr, durationStr);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Scraper Exception Core]: {ex.Message}");
                UpdateProfileStatus(profile, "Scrape Error", "TBD", "TBD", "TBD", "TBD");
            }
        }

        private static void UpdateProfileStatus(
            FlightProfile profile,
            string status,
            string flightNo,
            string deptTime,
            string arrTime,
            string duration)
        {
            if (Application.Current?.Dispatcher.CheckAccess() ?? true)
            {
                profile.AvailabilityStatus = status;
                profile.FlightNumber = flightNo;
                profile.DepartureTime = deptTime;
                profile.ArrivalTime = arrTime;
                profile.Duration = duration;
                profile.LastChecked = DateTime.Now;
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    profile.AvailabilityStatus = status;
                    profile.FlightNumber = flightNo;
                    profile.DepartureTime = deptTime;
                    profile.ArrivalTime = arrTime;
                    profile.Duration = duration;
                    profile.LastChecked = DateTime.Now;
                }));
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlyNotify.Models;
using FlyNotify.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FlyNotify.Web.Services
{
    public class SchedulerWorker : BackgroundService
    {
        private readonly FlightService _flightService;
        private readonly IConfiguration _config;
        private readonly SemaphoreSlim _scrapeSemaphore = new(1, 1);

        public SchedulerWorker(FlightService flightService, IConfiguration config)
        {
            _flightService = flightService;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            SystemLog.Log("Daily Flight Scheduler Service Started.");

            // Calculate timezone
            var tzName = _config.GetValue<string>("Scheduler:TimeZone") ?? "Australia/Sydney";
            TimeZoneInfo localTz;
            try
            {
                localTz = TimeZoneInfo.FindSystemTimeZoneById(tzName);
            }
            catch
            {
                SystemLog.Log($"[TimeZone Error] Timezone '{tzName}' not found. Defaulting to UTC.");
                localTz = TimeZoneInfo.Utc;
            }

            var targetHour = _config.GetValue<int>("Scheduler:Hour", 10);
            var targetMinute = _config.GetValue<int>("Scheduler:Minute", 0);

            while (!stoppingToken.IsCancellationRequested)
            {
                var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, localTz);
                var nextRunLocal = nowLocal.Date.AddHours(targetHour).AddMinutes(targetMinute);

                if (nowLocal >= nextRunLocal)
                {
                    nextRunLocal = nextRunLocal.AddDays(1);
                }

                var delay = nextRunLocal - nowLocal;
                SystemLog.Log($"Next automated scan scheduled for {nextRunLocal:yyyy-MM-dd HH:mm:ss} (In {delay.TotalHours:F2} hours)");

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                SystemLog.Log("Triggering daily automated scheduled scan...");
                await RunBatchQueryAsync(true, stoppingToken);
            }
        }

        public async Task<bool> RunBatchQueryAsync(bool isLive, CancellationToken token)
        {
            var acquired = await _scrapeSemaphore.WaitAsync(0, token);
            if (!acquired)
            {
                SystemLog.Log("Scan skipped: Another scraping process is currently running.");
                return false;
            }

            try
            {
                var profilesToQuery = _flightService.GetProfiles();
                if (profilesToQuery.Count == 0)
                {
                    SystemLog.Log("No flights configured to monitor.");
                    return true;
                }

                // Clean out expired profiles first
                var cleanedProfiles = profilesToQuery.Where(p => p.TravelDate.Date >= DateTime.Today).ToList();
                if (cleanedProfiles.Count != profilesToQuery.Count)
                {
                    SystemLog.Log($"Removed {profilesToQuery.Count - cleanedProfiles.Count} expired profiles.");
                    _flightService.UpdateProfilesList(cleanedProfiles);
                    profilesToQuery = cleanedProfiles;
                }

                SystemLog.Log($"Starting flight query batch (Count: {profilesToQuery.Count}, Live: {isLive})");

                // Get snapshot before run to track changes for email alerts
                var snapshot = new Dictionary<string, string>();
                foreach (var p in profilesToQuery)
                {
                    string key = GetProfileKey(p);
                    if (!snapshot.ContainsKey(key))
                    {
                        snapshot[key] = $"{p.AvailabilityStatus}|{p.DetailedStatus}";
                    }
                }

                ScraperService.UseMockData = !isLive;
                var sortedProfiles = profilesToQuery.OrderByDescending(p => p.IsWildcardOrRegion).ToList();
                var random = new Random();
                int liveQueriesScraped = 0;

                for (int i = 0; i < sortedProfiles.Count; i++)
                {
                    var profile = sortedProfiles[i];

                    if (IsProfileCoveredByWildcard(profile, sortedProfiles))
                    {
                        SystemLog.Log($"Skipping {profile.DepartureAirport} -> {profile.ArrivalAirport} (covered by ALL/wildcard query)...");
                        continue;
                    }

                    if (liveQueriesScraped > 0 && isLive)
                    {
                        int delayMs = random.Next(2000, 5000);
                        SystemLog.Log($"Waiting {delayMs / 1000.0:F1}s before next query to mimic human browsing behavior...");
                        await Task.Delay(delayMs, token);
                    }

                    if (isLive) liveQueriesScraped++;

                    SystemLog.Log($"Scraping route {profile.DepartureAirport} -> {profile.ArrivalAirport}...");

                    List<FlightProfile> results;
                    try
                    {
                        results = await ScraperService.ExecuteScrapeAsync(profile, msg =>
                        {
                            SystemLog.Log($"[{profile.DepartureAirport} -> {profile.ArrivalAirport}] {msg}");
                        });
                    }
                    catch (Exception ex)
                    {
                        SystemLog.Log($"Batch query failed on route {profile.DepartureAirport} -> {profile.ArrivalAirport}: {ex.Message}");
                        break;
                    }

                    if (profile.IsWildcardOrRegion)
                    {
                        foreach (var result in results)
                        {
                            string seatsDetail = result.AvailabilityStatus;
                            result.DetailedStatus = seatsDetail;
                            result.AvailabilityStatus = "Available";

                            var existing = sortedProfiles.FirstOrDefault(p =>
                                p.DepartureAirport.Equals(result.DepartureAirport, StringComparison.OrdinalIgnoreCase) &&
                                p.ArrivalAirport.Equals(result.ArrivalAirport, StringComparison.OrdinalIgnoreCase) &&
                                p.TravelDate.Date == result.TravelDate.Date &&
                                p.PassengerCount == result.PassengerCount &&
                                p.SelectedCabins == result.SelectedCabins &&
                                (p.FlightNumber.Equals(result.FlightNumber, StringComparison.OrdinalIgnoreCase) || p.FlightNumber == "TBD"));

                            if (existing != null)
                            {
                                existing.FlightNumber = result.FlightNumber;
                                existing.DepartureTime = result.DepartureTime;
                                existing.ArrivalTime = result.ArrivalTime;
                                existing.Duration = result.Duration;
                                existing.AvailabilityStatus = "Available";
                                existing.DetailedStatus = seatsDetail;
                                existing.LastChecked = DateTime.Now;
                            }
                            else
                            {
                                sortedProfiles.Add(result);
                            }
                        }

                        var coveredSpecifics = sortedProfiles.Where(p => IsProfileCoveredByWildcard(p, new[] { profile })).ToList();
                        foreach (var specific in coveredSpecifics)
                        {
                            var match = results.FirstOrDefault(r =>
                                r.ArrivalAirport.Equals(specific.ArrivalAirport, StringComparison.OrdinalIgnoreCase) &&
                                (specific.FlightNumber == "TBD" || r.FlightNumber.Equals(specific.FlightNumber, StringComparison.OrdinalIgnoreCase)));

                            if (match != null)
                            {
                                specific.FlightNumber = match.FlightNumber;
                                specific.DepartureTime = match.DepartureTime;
                                specific.ArrivalTime = match.ArrivalTime;
                                specific.Duration = match.Duration;
                                specific.AvailabilityStatus = "Available";
                                specific.DetailedStatus = match.DetailedStatus;
                                specific.LastChecked = DateTime.Now;
                            }
                            else
                            {
                                specific.AvailabilityStatus = "Checked";
                                specific.DetailedStatus = "No Classes Found";
                                specific.FlightNumber = "TBD";
                                specific.DepartureTime = "TBD";
                                specific.ArrivalTime = "TBD";
                                specific.Duration = "TBD";
                                specific.LastChecked = DateTime.Now;
                            }
                        }

                        if (results.Count > 0)
                        {
                            profile.AvailabilityStatus = "Available";
                            profile.DetailedStatus = string.Join(" | ", results.Select(r => $"{r.ArrivalAirport}: {r.DetailedStatus}"));
                            profile.FlightNumber = "TBD";
                            profile.DepartureTime = "TBD";
                            profile.ArrivalTime = "TBD";
                            profile.Duration = "TBD";
                            profile.LastChecked = DateTime.Now;
                        }
                        else
                        {
                            profile.AvailabilityStatus = "Checked";
                            profile.DetailedStatus = "No Classes Found";
                            profile.FlightNumber = "TBD";
                            profile.DepartureTime = "TBD";
                            profile.ArrivalTime = "TBD";
                            profile.Duration = "TBD";
                            profile.LastChecked = DateTime.Now;
                        }
                    }
                    else
                    {
                        if (results.Count > 0)
                        {
                            var firstMatch = results[0];
                            profile.FlightNumber = firstMatch.FlightNumber;
                            profile.DepartureTime = firstMatch.DepartureTime;
                            profile.ArrivalTime = firstMatch.ArrivalTime;
                            profile.Duration = firstMatch.Duration;
                            profile.AvailabilityStatus = "Available";
                            profile.DetailedStatus = firstMatch.AvailabilityStatus;
                            profile.LastChecked = DateTime.Now;
                        }
                        else
                        {
                            profile.AvailabilityStatus = "Checked";
                            profile.DetailedStatus = "No Classes Found";
                            profile.FlightNumber = "TBD";
                            profile.DepartureTime = "TBD";
                            profile.ArrivalTime = "TBD";
                            profile.Duration = "TBD";
                            profile.LastChecked = DateTime.Now;
                        }
                    }
                }

                // Update the single service state
                _flightService.UpdateProfilesList(sortedProfiles);

                // Perform email change summary comparison
                await CompareAndSendNotificationAsync(snapshot, sortedProfiles);

                SystemLog.Log("Batch flight scan complete.");
                return true;
            }
            catch (Exception ex)
            {
                SystemLog.Log($"[Scheduler Error] Fail: {ex.Message}");
                return false;
            }
            finally
            {
                _scrapeSemaphore.Release();
            }
        }

        private bool IsProfileCoveredByWildcard(FlightProfile specific, IEnumerable<FlightProfile> activeProfiles)
        {
            if (specific.IsWildcardOrRegion) return false;
            return activeProfiles.Any(allProfile =>
                allProfile.IsWildcardOrRegion &&
                allProfile.DepartureAirport.Equals(specific.DepartureAirport, StringComparison.OrdinalIgnoreCase) &&
                allProfile.TravelDate.Date == specific.TravelDate.Date &&
                allProfile.TravelEndDate.Date == specific.TravelEndDate.Date &&
                allProfile.PassengerCount == specific.PassengerCount &&
                (specific.SelectedCabins & allProfile.SelectedCabins) == specific.SelectedCabins);
        }

        private string GetProfileKey(FlightProfile profile)
        {
            return $"{profile.DepartureAirport.ToUpper()}-{profile.ArrivalAirport.ToUpper()}-{profile.TravelDate:yyyyMMdd}-{profile.FlightNumber.ToUpper()}-{profile.PassengerCount}-{profile.CabinClass}";
        }

        private async Task CompareAndSendNotificationAsync(Dictionary<string, string> snapshot, List<FlightProfile> currentProfiles)
        {
            var changes = new List<string>();
            var currentKeys = new HashSet<string>();

            foreach (var profile in currentProfiles)
            {
                string key = GetProfileKey(profile);
                currentKeys.Add(key);

                string routeInfo = $"{profile.DepartureAirport} -> {profile.ArrivalAirport} on {profile.TravelDate:yyyy-MM-dd} (Flight: {profile.FlightNumber}, Cabin: {profile.CabinClass})";

                if (snapshot.TryGetValue(key, out string? oldStatus))
                {
                    string newStatus = $"{profile.AvailabilityStatus}|{profile.DetailedStatus}";
                    if (oldStatus != newStatus)
                    {
                        string[] oldParts = oldStatus.Split('|');
                        string oldAvail = oldParts[0];
                        string oldDetail = oldParts.Length > 1 ? oldParts[1] : "TBD";

                        changes.Add($"- [STATUS CHANGED] {routeInfo}: {oldAvail} ({oldDetail}) -> {profile.AvailabilityStatus} ({profile.DetailedStatus})");
                    }
                }
                else
                {
                    changes.Add($"- [NEW FLIGHT FOUND] {routeInfo}: {profile.AvailabilityStatus} ({profile.DetailedStatus})");
                }
            }

            foreach (var kvp in snapshot)
            {
                if (!currentKeys.Contains(kvp.Key))
                {
                    string[] parts = kvp.Key.Split('-');
                    if (parts.Length >= 6)
                    {
                        string dept = parts[0];
                        string dest = parts[1];
                        string dateStr = parts[2];
                        string flight = parts[3];
                        string cabin = parts[5];

                        if (dest.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                        {
                            changes.Add($"- [WILDCARD RESOLVED] {dept} -> {dest} on {dateStr} (Flight: {flight}, Cabin: {cabin}) was checked and replaced with specific flight results.");
                        }
                        else
                        {
                            changes.Add($"- [REMOVED] {dept} -> {dest} on {dateStr} (Flight: {flight}, Cabin: {cabin})");
                        }
                    }
                }
            }

            if (changes.Count > 0)
            {
                string changeSummary = string.Join("\n", changes);
                SystemLog.Log($"Detected flight changes. Sending email notification...\n{changeSummary}");
                await EmailService.SendStatusAlertAsync(changeSummary);
            }
            else
            {
                SystemLog.Log("No status changes detected. Alert email skipped.");
            }
        }
    }
}

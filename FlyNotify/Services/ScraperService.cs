using System;
using System.Text;
using System.Net.Http;
using System.Collections.Generic;
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
    public static class ScraperService
    {
        private static readonly HttpClient Client = new HttpClient();

        static ScraperService()
        {
            Client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            Client.Timeout = TimeSpan.FromSeconds(30);
        }

        /*
            Main asynchronous processing worker thread pipeline loop.
        */
        public static async Task ExecuteScrapeAsync(FlightProfile profile)
        {
            UpdateProfileStatus(profile, "Searching...", "TBD", "TBD", "TBD", "TBD");

            try
            {
                string targetUrl = profile.BuildQantasQueryUrl();
                string htmlContent = await Client.GetStringAsync(targetUrl);

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlContent);

                /*
                    ---------------------------------------------------------------------
                    DATA PRESENTATION AND CALCULATION CORE
                    ---------------------------------------------------------------------
                    Define the structural evaluation order requested by the specification:
                    From Highest tier (First) down to Lowest tier (Economy).
                */
                var executionTierOrder = new[]
                {
                    CabinClasses.First,
                    CabinClasses.Business,
                    CabinClasses.PremiumEconomy,
                    CabinClasses.Economy
                };

                var statusBuilder = new StringBuilder();
                bool primaryMetadataCaptured = false;

                string flightNo = "QF000";
                string deptTime = "--:--";
                string arrTime = "--:--";
                string duration = "--h --m";

                foreach (var cabinTier in executionTierOrder)
                {
                    // Evaluate if this specific tier was included in the profile's query matrix
                    if (profile.SelectedCabins.HasFlag(cabinTier))
                    {
                        string bucketLabel = cabinTier.ToFareBucketCode(); 

                        /*
                            Locate the row specific to this cabin class.
                            Matches text descriptions within the table structure (e.g., "First", "Business").
                        */
                        string searchClassName = cabinTier == CabinClasses.PremiumEconomy ? "Premium Economy" : cabinTier.ToString();
                        var cabinRowNode = htmlDoc.DocumentNode.SelectSingleNode($"//tr[contains(., '{searchClassName}')]");

                        if (cabinRowNode != null)
                        {
                            // Capture core structural parameters from the first valid row returned
                            if (!primaryMetadataCaptured)
                            {
                                var fNoNode = cabinRowNode.SelectSingleNode(".//td[contains(@class, 'flight-number')]");
                                var dTNode = cabinRowNode.SelectSingleNode(".//td[contains(@class, 'dept-time')]");
                                var aTNode = cabinRowNode.SelectSingleNode(".//td[contains(@class, 'arr-time')]");
                                var durNode = cabinRowNode.SelectSingleNode(".//td[contains(@class, 'duration')]");

                                if (fNoNode != null) { flightNo = fNoNode.InnerText.Trim(); }
                                if (dTNode != null) { deptTime = dTNode.InnerText.Trim(); }
                                if (aTNode != null) { arrTime = aTNode.InnerText.Trim(); }
                                if (durNode != null) { duration = durNode.InnerText.Trim(); }

                                primaryMetadataCaptured = true;
                            }

                            // Extract seat availability counts out from the target cell container
                            var seatsCellNode = cabinRowNode.SelectSingleNode(".//td[contains(@class, 'seats-available') or contains(@class, 'seats')]");
                            int capturedSeatCount = 0;

                            if (seatsCellNode != null)
                            {
                                string seatText = seatsCellNode.InnerText.Trim();

                                // Strip out trailing text modifiers like "seats" or "+" symbols
                                string numericPart = new string(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(seatText, char.IsDigit)));

                                if (int.TryParse(numericPart, out int parsedCount))
                                {
                                    capturedSeatCount = parsedCount;
                                }
                                else if (seatText.Contains("Available") || seatText.Contains("Yes") || seatText.Contains("+"))
                                {
                                    // Fallback indicator default if text shows presence without an explicit digit
                                    capturedSeatCount = 5;
                                }
                            }

                            // Enforce capacity ceiling constraint rules (Clamp values to a max of 5)
                            int finalDisplayCount = capturedSeatCount >= 5 ? 5 : capturedSeatCount;

                            if (statusBuilder.Length > 0)
                            {
                                statusBuilder.Append(" ");
                            }
                            statusBuilder.Append($"{bucketLabel}{finalDisplayCount}");
                        }
                        else
                        {
                            // If a chosen cabin tier row cannot be found in the DOM, it means 0 seats are available
                            if (statusBuilder.Length > 0)
                            {
                                statusBuilder.Append(" ");
                            }
                            statusBuilder.Append($"{bucketLabel}0");
                        }
                    }
                }

                string finalStatusOutput = statusBuilder.Length > 0 ? statusBuilder.ToString() : "No Classes Found";

                // Marshall calculations back onto the primary grid framework thread context
                UpdateProfileStatus(profile, finalStatusOutput, flightNo, deptTime, arrTime, duration);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Scraper Exception Core]: {ex.Message}");
                UpdateProfileStatus(profile, "Scrape Error", "ERR", "--:--", "--:--", "--h --m");
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
            if (Application.Current == null)
            {
                return;
            }

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
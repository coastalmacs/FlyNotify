using System;
using System.Text;

namespace FlyNotify.Models
{
    public class FlightProfile
    {
        public required string DepartureAirport { get; set; }
        public required string ArrivalAirport { get; set; }
        public DateTime TravelDate { get; set; }
        public required string FlightNumber { get; set; }
        public required string DepartureTime { get; set; }
        public required string ArrivalTime { get; set; }
        public required string Duration { get; set; }
        public required string CabinClass { get; set; }
        public int PassengerCount { get; set; }
        public required string AvailabilityStatus { get; set; }
        public DateTime LastChecked { get; set; }

        public string TravelDateString
        {
            get { return TravelDate.ToString("yyyy-MM-dd"); }
        }


        public string FullScheduleDisplay
        {
            get
            {
                if (DepartureTime == "Pending" || ArrivalTime == "Pending")
                {
                    return $"{TravelDateString}";
                }
                else
                {
                    return $"{TravelDateString} ({DepartureTime} - {ArrivalTime})";
                }
            }
        }

        public string LastCheckedDisplay
        {
            get
            {
                if (LastChecked == DateTime.MinValue)
                {
                    return "Never";
                }
                else
                {
                    return LastChecked.ToString("yyyy-MM-dd HH:mm:ss");
                }
            }
        }

        /*
            SPECIFICATION ENGINE 1: Generates a perfectly formatted Qantas Reward Finder 
            query string utilizing structural compact parameters.
        */
        public string BuildQantasQueryUrl()
        {
            // For single specific dates, pass the exact same date twice separated by an uppercase 'I'
            string dateRangeParam = $"{TravelDateString}I{TravelDateString}";

            var urlBuilder = new StringBuilder("https://flightrewardfinder.qantas.com/");
            urlBuilder.Append($"?o={Uri.EscapeDataString(DepartureAirport)}");
            urlBuilder.Append($"&d={Uri.EscapeDataString(ArrivalAirport)}");
            urlBuilder.Append($"&c={Uri.EscapeDataString(CabinClass)}"); // Maps directly to comma-separated descriptive words
            urlBuilder.Append($"&p={PassengerCount}");
            urlBuilder.Append($"&dr={dateRangeParam}");
            urlBuilder.Append("&pg=1"); // Forces default page index 1

            return urlBuilder.ToString();
        }

        /*
            SPECIFICATION ENGINE 2: Maps descriptive UI string entries to strict ExpertFlyer 
            fare bucket alphabetic characters and compiles a complete results query.
        */
        public string BuildExpertFlyerQueryUrl()
        {
            // Translate comma-separated descriptive classes into single-character fare buckets
            var fareBucketBuilder = new System.Collections.Generic.List<string>();
            string normalizedCabin = CabinClass.ToUpper();

            if (normalizedCabin.Contains("FIRST"))
            {
                fareBucketBuilder.Add("P"); // P corresponds with First availability on Qantas
            }
            if (normalizedCabin.Contains("BUSINESS"))
            {
                fareBucketBuilder.Add("U"); // U corresponds with Business availability on Qantas
            }
            if (normalizedCabin.Contains("PREMIUM"))
            {
                fareBucketBuilder.Add("W"); // Standard industry code for Premium Economy
            }
            if (normalizedCabin.Contains("ECONOMY") && !normalizedCabin.Contains("PREMIUM"))
            {
                fareBucketBuilder.Add("X"); // Standard industry award code for Economy
            }

            string classFilterParam = string.Join(",", fareBucketBuilder);

            // Structure departure time parameter with specific spec-mandated 'T00%3A00' trailing time component
            string dateTimeParam = $"{TravelDateString}T00%3A00";

            var urlBuilder = new StringBuilder("https://www.expertflyer.com/air/availability/results");
            urlBuilder.Append($"?origin={Uri.EscapeDataString(DepartureAirport)}");
            urlBuilder.Append($"&destination={Uri.EscapeDataString(ArrivalAirport)}");
            urlBuilder.Append($"&departureDateTime={dateTimeParam}");
            urlBuilder.Append("&returnDateTime="); // Omitted empty for a single-leg monitoring query
            urlBuilder.Append("&alliance=none"); // Spec restriction rule
            urlBuilder.Append("&excludeCodeshares=false"); // Spec restriction rule

            if (!string.IsNullOrEmpty(classFilterParam))
            {
                urlBuilder.Append($"&classFilter={Uri.EscapeDataString(classFilterParam)}");
            }

            // Append mandatory result configuration matrix appendix directly to end of string
            urlBuilder.Append("&pcc=USA+%28Default%29&resultsDisplay=single");

            return urlBuilder.ToString();
        }

    }
}
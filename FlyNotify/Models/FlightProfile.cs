using System;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace FlyNotify.Models
{
    public class FlightProfile
    {
        // Required from user
        public required string DepartureAirport { get; set; }
        public required string ArrivalAirport { get; set; }
        public DateTime TravelDate { get; set; }
        public int PassengerCount { get; set; } = 1;

        [JsonIgnore]
        public CabinClasses SelectedCabins { get; set; } = CabinClasses.Business;

        // Scraped by system
        public string FlightNumber { get; set; } = "TDB";
        public  string DepartureTime { get; set; } = "TBD";
        public  string ArrivalTime { get; set; } = "TDB";
        public  string Duration { get; set; } = "TBD";
        public string TargetCabin { get; set; } = "TBD";
        public required string AvailabilityStatus { get; set; } = "TBD";
        public DateTime LastChecked { get; set; } = DateTime.MinValue;

        public string CabinClass
        {
            get
            {
                return SelectedCabins.ToQantasString();
            }
            set
            {
                // Facilitates safe string parsing back into flags when reloading from JSON files
                CabinClasses parsedFlags = CabinClasses.None;

                if (!string.IsNullOrEmpty(value))
                {
                    string normalized = value.ToUpper();

                    if (normalized.Contains("ECONOMY") && !normalized.Contains("PREMIUM"))
                    {
                        parsedFlags |= CabinClasses.Economy;
                    }
                    if (normalized.Contains("PREMIUM"))
                    {
                        parsedFlags |= CabinClasses.PremiumEconomy;
                    }
                    if (normalized.Contains("BUSINESS"))
                    {
                        parsedFlags |= CabinClasses.Business;
                    }
                    if (normalized.Contains("FIRST"))
                    {
                        parsedFlags |= CabinClasses.First;
                    }
                }

                SelectedCabins = parsedFlags == CabinClasses.None ? CabinClasses.Business : parsedFlags;
            }
        }


        public string TravelDateString
        {
            get { return TravelDate.ToString("yyyy-MM-dd"); } 
        }

        public string FullScheduleDisplay
        {
            get
            {
                if (DepartureTime == "TBD" || ArrivalTime == "TBD")
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
            string dateRangeParam = $"{TravelDateString}I{TravelDateString}"; // Spec date layout requirement

            var urlBuilder = new StringBuilder("https://flightrewardfinder.qantas.com/");
            urlBuilder.Append($"?o={Uri.EscapeDataString(DepartureAirport)}");
            urlBuilder.Append($"&d={Uri.EscapeDataString(ArrivalAirport)}");
            urlBuilder.Append($"&c={Uri.EscapeDataString(SelectedCabins.ToQantasString())}"); // e.g. "Business,First"
            urlBuilder.Append($"&p={PassengerCount}");
            urlBuilder.Append($"&dr={dateRangeParam}");
            urlBuilder.Append("&pg=1");

            return urlBuilder.ToString();
        }

        /*
            SPECIFICATION ENGINE 2: Maps descriptive UI string entries to strict ExpertFlyer 
            fare bucket alphabetic characters and compiles a complete results query.
        */
        public string BuildExpertFlyerQueryUrl()
        {
            string dateTimeParam = $"{TravelDateString}T00%3A00"; // Strict spec timestamp
            string classFilterParam = SelectedCabins.ToFareBucketCode(); // e.g. "U,P"

            var urlBuilder = new StringBuilder("https://www.expertflyer.com/air/availability/results");
            urlBuilder.Append($"?origin={Uri.EscapeDataString(DepartureAirport)}");
            urlBuilder.Append($"&destination={Uri.EscapeDataString(ArrivalAirport)}");
            urlBuilder.Append($"&departureDateTime={dateTimeParam}");
            urlBuilder.Append("&returnDateTime=");
            urlBuilder.Append("&alliance=none"); // Mandatory spec parameter
            urlBuilder.Append("&excludeCodeshares=false"); // Mandatory spec parameter

            if (!string.IsNullOrEmpty(classFilterParam))
            {
                urlBuilder.Append($"&classFilter={Uri.EscapeDataString(classFilterParam)}");
            }

            urlBuilder.Append("&pcc=USA+%28Default%29&resultsDisplay=single"); // Mandatory spec appendix payload

            return urlBuilder.ToString();
        }

    }
}
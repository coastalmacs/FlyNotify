using System;

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
                return $"{TravelDateString} ({DepartureTime} - {ArrivalTime})";
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
    }
}
using System;
using System.Collections.Generic;
using System.Text;

namespace FlyNotify.Models
{
    public class FlightProfile
    {

            public string Departure { get; set; }
            public string Arrival { get; set; }
            public DateTime TravelDate { get; set; }
            public string FlightNumber { get; set; }
            public string DepartureTime { get; set; }
            public string ArrivalTime { get; set; }
            public string DurationString { get; set; }
            public string CabinClass { get; set; }
            public int PassengerCount { get; set; }
            public string AvailabilityStatus { get; set; }
            public string LastCheckedString { get; set; }

            public string TravelDateString
            {
                get
                {
                    return TravelDate.ToString("yyyy-MM-dd");
                }
            }
    }
}

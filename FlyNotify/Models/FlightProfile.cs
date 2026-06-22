using System;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace FlyNotify.Models
{
    public class FlightProfile : System.ComponentModel.INotifyPropertyChanged
    {
        private string _departureAirport = "";
        private string _arrivalAirport = "";
        private DateTime _travelDate;
        private DateTime _travelEndDate;
        private int _passengerCount = 1;
        private CabinClasses _selectedCabins = CabinClasses.Business;
        private string _flightNumber = "TDB";
        private string _departureTime = "TBD";
        private string _arrivalTime = "TBD";
        private string _duration = "TBD";
        private string _targetCabin = "TBD";
        private string _availabilityStatus = "TBD";
        private DateTime _lastChecked = DateTime.MinValue;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        /*
            Triggers WPF data binding updates for specific property path listeners.
        */
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        /*
            Updates field values securely and triggers bound UI element redraw requests.
        */
        protected bool SetField<T>(ref T field, T value, string propertyName)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public required string DepartureAirport
        {
            get
            {
                return _departureAirport;
            }
            set
            {
                SetField(ref _departureAirport, value, nameof(DepartureAirport));
            }
        }

        public required string ArrivalAirport
        {
            get
            {
                return _arrivalAirport;
            }
            set
            {
                SetField(ref _arrivalAirport, value, nameof(ArrivalAirport));
            }
        }

        public DateTime TravelDate
        {
            get
            {
                return _travelDate;
            }
            set
            {
                if (SetField(ref _travelDate, value, nameof(TravelDate)))
                {
                    OnPropertyChanged(nameof(TravelDateString));
                    OnPropertyChanged(nameof(FullScheduleDisplay));
                }
            }
        }

        public DateTime TravelEndDate
        {
            get
            {
                return _travelEndDate == DateTime.MinValue ? TravelDate : _travelEndDate;
            }
            set
            {
                if (_travelEndDate != value)
                {
                    _travelEndDate = value;
                    OnPropertyChanged(nameof(TravelEndDate));
                    OnPropertyChanged(nameof(TravelEndDateString));
                    OnPropertyChanged(nameof(FullScheduleDisplay));
                }
            }
        }

        public int PassengerCount
        {
            get
            {
                return _passengerCount;
            }
            set
            {
                SetField(ref _passengerCount, value, nameof(PassengerCount));
            }
        }

        [JsonIgnore]
        public CabinClasses SelectedCabins
        {
            get
            {
                return _selectedCabins;
            }
            set
            {
                if (SetField(ref _selectedCabins, value, nameof(SelectedCabins)))
                {
                    OnPropertyChanged(nameof(CabinClass));
                }
            }
        }

        public string FlightNumber
        {
            get
            {
                return _flightNumber;
            }
            set
            {
                SetField(ref _flightNumber, value, nameof(FlightNumber));
            }
        }

        public string DepartureTime
        {
            get
            {
                return _departureTime;
            }
            set
            {
                if (SetField(ref _departureTime, value, nameof(DepartureTime)))
                {
                    OnPropertyChanged(nameof(FullScheduleDisplay));
                }
            }
        }

        public string ArrivalTime
        {
            get
            {
                return _arrivalTime;
            }
            set
            {
                if (SetField(ref _arrivalTime, value, nameof(ArrivalTime)))
                {
                    OnPropertyChanged(nameof(FullScheduleDisplay));
                }
            }
        }

        public string Duration
        {
            get
            {
                return _duration;
            }
            set
            {
                SetField(ref _duration, value, nameof(Duration));
            }
        }

        public string TargetCabin
        {
            get
            {
                return _targetCabin;
            }
            set
            {
                SetField(ref _targetCabin, value, nameof(TargetCabin));
            }
        }

        public required string AvailabilityStatus
        {
            get
            {
                return _availabilityStatus;
            }
            set
            {
                SetField(ref _availabilityStatus, value, nameof(AvailabilityStatus));
            }
        }

        public DateTime LastChecked
        {
            get
            {
                return _lastChecked;
            }
            set
            {
                if (SetField(ref _lastChecked, value, nameof(LastChecked)))
                {
                    OnPropertyChanged(nameof(LastCheckedDisplay));
                }
            }
        }

        public string CabinClass
        {
            get
            {
                return SelectedCabins.ToFareClassCode();
            }
            set
            {
                // Facilitates safe string parsing back into flags when reloading from JSON files
                CabinClasses parsedFlags = CabinClasses.None;

                if (!string.IsNullOrEmpty(value))
                {
                    string normalized = value.ToUpper();

                    if (normalized.Contains('F') || normalized.Contains("FIRST"))
                    {
                        parsedFlags |= CabinClasses.First;
                    }
                    if (normalized.Contains('J') || normalized.Contains("BUSINESS"))
                    {
                        parsedFlags |= CabinClasses.Business;
                    }
                    if (normalized.Contains('W') || normalized.Contains("PREMIUM"))
                    {
                        parsedFlags |= CabinClasses.PremiumEconomy;
                    }
                    if (normalized.Contains('Y') || (normalized.Contains("ECONOMY") && !normalized.Contains("PREMIUM")))
                    {
                        parsedFlags |= CabinClasses.Economy;
                    }
                }

                SelectedCabins = parsedFlags == CabinClasses.None ? CabinClasses.Business : parsedFlags;
            }
        }


        public string TravelDateString
        {
            get
            {
                return TravelDate.ToString("yyyy-MM-dd");
            }
        }

        public string TravelEndDateString
        {
            get
            {
                return TravelEndDate.ToString("yyyy-MM-dd");
            }
        }

        public string GetFormattedDateRange()
        {
            if (TravelDate == TravelEndDate)
            {
                return TravelDate.ToString("d MMM yyyy");
            }

            if (TravelDate.Year == TravelEndDate.Year)
            {
                if (TravelDate.Month == TravelEndDate.Month)
                {
                    return $"{TravelDate.Day}-{TravelEndDate.Day} {TravelDate:MMM yyyy}";
                }
                else
                {
                    return $"{TravelDate:d MMM} - {TravelEndDate:d MMM} {TravelDate:yyyy}";
                }
            }
            else
            {
                return $"{TravelDate:d MMM yyyy} - {TravelEndDate:d MMM yyyy}";
            }
        }

        public string FullScheduleDisplay
        {
            get
            {
                string dateRange = GetFormattedDateRange();

                if (DepartureTime == "TBD" || ArrivalTime == "TBD" || DepartureTime == "--:--" || ArrivalTime == "--:--")
                {
                    return dateRange;
                }
                else
                {
                    return $"{dateRange} ({DepartureTime}-{ArrivalTime})";
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
                    return LastChecked.ToString("yyyy-MM-dd HH:mm");
                }
            }
        }

        /*
            SPECIFICATION ENGINE 1: Generates a perfectly formatted Qantas Reward Finder 
            query string utilizing structural compact parameters.
        */
        public string BuildQantasQueryUrl()
        {
            string dateRangeParam = $"{TravelDateString}I{TravelEndDateString}"; // Spec date layout requirement

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
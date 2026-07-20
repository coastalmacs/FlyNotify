using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FlyNotify.Models;
using Microsoft.Extensions.Configuration;

namespace FlyNotify.Web.Services
{
    public class FlightService
    {
        private readonly string _storageFile;
        private readonly List<FlightProfile> _profiles = new();
        private readonly object _lock = new();
        private static readonly JsonSerializerOptions SerializationOptions = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true
        };

        public FlightService(IConfiguration config)
        {
            var dataDir = config.GetValue<string>("Scraper:DataDirectory") ?? "data";
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            _storageFile = Path.Combine(dataDir, "flights.json");
            LoadProfiles();
        }

        public List<FlightProfile> GetProfiles()
        {
            lock (_lock)
            {
                return _profiles.ToList();
            }
        }

        public void AddProfile(FlightProfile profile)
        {
            lock (_lock)
            {
                // Filter out expired dates
                _profiles.RemoveAll(p => p.TravelDate.Date < DateTime.Today);

                // Check for duplicates
                var existing = _profiles.FirstOrDefault(p =>
                    p.DepartureAirport.Equals(profile.DepartureAirport, StringComparison.OrdinalIgnoreCase) &&
                    p.ArrivalAirport.Equals(profile.ArrivalAirport, StringComparison.OrdinalIgnoreCase) &&
                    p.TravelDate.Date == profile.TravelDate.Date &&
                    p.FlightNumber.Equals(profile.FlightNumber, StringComparison.OrdinalIgnoreCase) &&
                    p.CabinClass == profile.CabinClass);

                if (existing == null)
                {
                    _profiles.Add(profile);
                    SaveProfiles();
                }
            }
        }

        public void DeleteProfile(string departure, string arrival, string travelDate, string flightNumber)
        {
            lock (_lock)
            {
                _profiles.RemoveAll(p =>
                    p.DepartureAirport.Equals(departure, StringComparison.OrdinalIgnoreCase) &&
                    p.ArrivalAirport.Equals(arrival, StringComparison.OrdinalIgnoreCase) &&
                    p.TravelDate.ToString("yyyy-MM-dd") == travelDate &&
                    p.FlightNumber.Equals(flightNumber, StringComparison.OrdinalIgnoreCase));
                SaveProfiles();
            }
        }

        public void SaveProfiles()
        {
            lock (_lock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(_profiles, SerializationOptions);
                    File.WriteAllText(_storageFile, json, System.Text.Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    SystemLog.Log($"[Storage Save Failure]: {ex.Message}");
                }
            }
        }

        public void UpdateProfilesList(List<FlightProfile> newList)
        {
            lock (_lock)
            {
                _profiles.Clear();
                _profiles.AddRange(newList);
                SaveProfiles();
            }
        }

        private void LoadProfiles()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_storageFile))
                    {
                        string json = File.ReadAllText(_storageFile, System.Text.Encoding.UTF8);
                        var list = JsonSerializer.Deserialize<List<FlightProfile>>(json, SerializationOptions);
                        if (list != null)
                        {
                            list.RemoveAll(p => p.TravelDate.Date < DateTime.Today);
                            _profiles.AddRange(list);
                        }
                    }
                }
                catch (Exception ex)
                {
                    SystemLog.Log($"[Storage Load Failure]: {ex.Message}");
                }
            }
        }
    }
}

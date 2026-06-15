using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FlyNotify.Models
{
    /*
        Thread-safe file system I/O persistence manager responsible for 
        serializing flight query matrices to flat JSON configuration files
        safely located inside the Windows Roaming AppData User Profile sandbox.
    */
    public static class FlightProfilePersistence
    {
        // Path mapped cleanly to the Windows Roaming AppData folder system (%appdata%\FlyNotify)
        private static readonly string StorageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlyNotify"
        );

        private static readonly string StorageFile = Path.Combine(StorageDirectory, "flights.json");

        // Statically structured serialization configuration settings
        private static readonly JsonSerializerOptions SerializationOptions = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true
        };

        /*
            Serializes an active collection list object matrix to the User Profile disk space.
        */
        public static void SaveProfiles(IEnumerable<FlightProfile> profiles)
        {
            try
            {
                // Guarantee the physical directory architecture exists before attempting file writes
                if (!Directory.Exists(StorageDirectory))
                {
                    Directory.CreateDirectory(StorageDirectory);
                }

                // Transform and flush data contract matrix directly using UTF-8 text encoding
                string jsonPayload = JsonSerializer.Serialize(profiles, SerializationOptions);
                File.WriteAllText(StorageFile, jsonPayload, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // Trace exception failure boundaries out to runtime debugging hooks
                System.Diagnostics.Debug.WriteLine($"[Persistence Save Failure]: {ex.Message}");
            }
        }

        /*
            Deserializes historical user records back out from the User Profile sandbox area.
        */
        public static List<FlightProfile> LoadProfiles()
        {
            try
            {
                // Evaluate file visibility boundaries prior to initializing parsing operations
                if (!File.Exists(StorageFile))
                {
                    return [];
                }

                string jsonPayload = File.ReadAllText(StorageFile, System.Text.Encoding.UTF8);
                List<FlightProfile>? recoveredProfiles = JsonSerializer.Deserialize<List<FlightProfile>>(jsonPayload, SerializationOptions);

                return recoveredProfiles ?? [];
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Persistence Load Failure]: {ex.Message}");
                return [];
            }
        }
    }
}
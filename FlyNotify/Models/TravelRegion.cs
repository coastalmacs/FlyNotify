using System;

namespace FlyNotify.Models
{
    /*
        Defines the valid regional tracking codes and macro wildcards 
        outlined within the FlyNotify App Specification data dictionary.
    */
    public enum TravelRegion
    {
        AU,  // Australia Domestic / Tasman
        NZ,  // New Zealand Local
        US,  // United States and North America
        UK,  // United Kingdom and Ireland
        EU,  // Continental Europe
        SE,  // South East Asia
        NA,  // North East Asia
        ME,  // Middle East
        WN,  // West Coast North America
        CN,  // Central North America
        EN,  // East Coast North America
        LA,  // South America
        AF   // Africa and Indian Ocean Regions
    }

    /*
        Provides operational text descriptions for administrative diagnostic logging.
    */
    public static class TravelRegionExtensions
    {
        /*
            Resolves the compact two-letter spec token into a friendly region name.
        */
        public static string GetDescription(this TravelRegion region)
        {
            switch (region)
            {
                case TravelRegion.AU:
                    return "Australia";
                case TravelRegion.NZ:
                    return "New Zealand";
                case TravelRegion.US:
                    return "United States Territories";
                case TravelRegion.UK:
                    return "United Kingdom and Ireland";
                case TravelRegion.EU:
                    return "Continental Europe";
                case TravelRegion.SE:
                    return "South East Asia";
                case TravelRegion.NA:
                    return "North East Asia";
                case TravelRegion.ME:
                    return "Middle East";
                case TravelRegion.WN:
                    return "West Coast North America";
                case TravelRegion.CN:
                    return "Central North America";
                case TravelRegion.EN:
                    return "East Coast North America";
                case TravelRegion.LA:
                    return "Latin and South America";
                case TravelRegion.AF:
                    return "Africa";
                default:
                    return "Unknown Region";
            }
        }
    }
}
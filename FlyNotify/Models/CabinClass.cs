using System;
using System.Collections.Generic;

namespace FlyNotify.Models
{
    /*
        Bitwise Flags decoration allowing a singular profile matrix row 
        to store and query multiple cabin criteria simultaneously.
    */
    [Flags]
    public enum CabinClasses
    {
        None = 0,
        Economy = 1,
        PremiumEconomy = 2,
        Business = 4,
        First = 8
    }

    /*
        Type-safe evaluation extensions mapping flag combinations 
        directly to strict external URL parameters.
    */
    public static class CabinClassesExtensions
    {
        /*
            Maps selected flags to a comma-separated list of official 
            ExpertFlyer single-letter award fare bucket codes.
        */
        public static string ToFareBucketCode(this CabinClasses cabins)
        {
            var buckets = new List<string>();

            if (cabins.HasFlag(CabinClasses.First))
            {
                buckets.Add("P"); // P corresponds with First class
            }
            if (cabins.HasFlag(CabinClasses.Business))
            {
                buckets.Add("U"); // U corresponds with Business class
            }
            if (cabins.HasFlag(CabinClasses.PremiumEconomy))
            {
                buckets.Add("Z"); // Standard industry Premium Economy award code
            }
            if (cabins.HasFlag(CabinClasses.Economy))
            {
                buckets.Add("X"); // Standard industry Economy award code
            }

            return string.Join(",", buckets);
        }

        /*
            Maps selected flags to a comma-separated list of descriptive words 
            natively expected by flightrewardfinder.qantas.com.
        */
        public static string ToQantasString(this CabinClasses cabins)
        {
            var labels = new List<string>();

            if (cabins.HasFlag(CabinClasses.Economy))
            {
                labels.Add("Economy");
            }
            if (cabins.HasFlag(CabinClasses.PremiumEconomy))
            {
                labels.Add("Premium Economy");
            }
            if (cabins.HasFlag(CabinClasses.Business))
            {
                labels.Add("Business");
            }
            if (cabins.HasFlag(CabinClasses.First))
            {
                labels.Add("First");
            }

            return string.Join(",", labels);
        }
    }
}
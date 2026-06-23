using System;
using System.Collections.Generic;
using System.Linq;

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
        public static string ToFareBucketCode(this CabinClasses cabins, string? airlineCode = null)
        {
            var buckets = new List<string>();
            string airline = airlineCode?.ToUpper() ?? "DEFAULT";

            if (cabins.HasFlag(CabinClasses.First))
            {
                switch (airline)
                {
                    case "QF":
                        buckets.AddRange(new[] { "F", "A", "P" });
                        break;
                    case "BA":
                        buckets.AddRange(new[] { "F", "A", "Z" });
                        break;
                    case "AA":
                        buckets.AddRange(new[] { "F", "A", "Z" });
                        break;
                    case "JL":
                        buckets.AddRange(new[] { "F", "A", "Z" });
                        break;
                    case "EK":
                        buckets.AddRange(new[] { "F", "A", "P", "Z" });
                        break;
                    default:
                        buckets.Add("P");
                        break;
                }
            }
            if (cabins.HasFlag(CabinClasses.Business))
            {
                switch (airline)
                {
                    case "QF":
                        buckets.AddRange(new[] { "J", "C", "D", "I", "U" });
                        break;
                    case "BA":
                        buckets.AddRange(new[] { "J", "C", "D", "I", "R", "U" });
                        break;
                    case "AA":
                        buckets.AddRange(new[] { "J", "C", "D", "I", "R", "U" });
                        break;
                    case "JL":
                        buckets.AddRange(new[] { "J", "C", "D", "I", "U", "X" });
                        break;
                    case "EK":
                        buckets.AddRange(new[] { "J", "C", "D", "I", "O" });
                        break;
                    default:
                        buckets.Add("U");
                        break;
                }
            }
            if (cabins.HasFlag(CabinClasses.PremiumEconomy))
            {
                switch (airline)
                {
                    case "QF":
                        buckets.AddRange(new[] { "W", "R", "T", "Z" });
                        break;
                    case "BA":
                        buckets.AddRange(new[] { "W", "E", "T", "P" });
                        break;
                    case "AA":
                        buckets.AddRange(new[] { "W", "P", "X" });
                        break;
                    case "JL":
                        buckets.AddRange(new[] { "W", "E", "P" });
                        break;
                    case "EK":
                        buckets.AddRange(new[] { "W", "E", "T" });
                        break;
                    default:
                        buckets.Add("Z");
                        break;
                }
            }
            if (cabins.HasFlag(CabinClasses.Economy))
            {
                switch (airline)
                {
                    case "QF":
                        buckets.AddRange(new[] { "Y", "B", "E", "H", "K", "L", "M", "N", "O", "Q", "S", "V", "X" });
                        break;
                    case "BA":
                        buckets.AddRange(new[] { "Y", "B", "H", "K", "M", "L", "V", "S", "N", "Q", "O", "G", "X" });
                        break;
                    case "AA":
                        buckets.AddRange(new[] { "Y", "B", "E", "G", "H", "K", "L", "M", "N", "O", "Q", "S", "T", "V" });
                        break;
                    case "JL":
                        buckets.AddRange(new[] { "Y", "B", "H", "K", "L", "M", "N", "Q", "R", "S", "T", "V" });
                        break;
                    case "EK":
                        buckets.AddRange(new[] { "Y", "B", "E", "G", "H", "K", "L", "M", "N", "Q", "R", "S", "T", "U", "V", "W", "X" });
                        break;
                    default:
                        buckets.Add("X");
                        break;
                }
            }

            return string.Join(",", buckets.Distinct());
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

        /*
            Maps selected flags to a comma-separated list of official 
            IATA single-letter fare class codes (F, J, W, Y).
        */
        public static string ToFareClassCode(this CabinClasses cabins)
        {
            var codes = new List<string>();

            if (cabins.HasFlag(CabinClasses.First))
            {
                codes.Add("F");
            }
            if (cabins.HasFlag(CabinClasses.Business))
            {
                codes.Add("J");
            }
            if (cabins.HasFlag(CabinClasses.PremiumEconomy))
            {
                codes.Add("W");
            }
            if (cabins.HasFlag(CabinClasses.Economy))
            {
                codes.Add("Y");
            }

            return string.Join(" ", codes);
        }
    }
}
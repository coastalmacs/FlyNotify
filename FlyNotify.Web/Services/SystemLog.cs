using System;
using System.Collections.Generic;

namespace FlyNotify.Web.Services
{
    public static class SystemLog
    {
        private static readonly List<string> Logs = new();
        private static readonly object Lock = new();

        public static void Log(string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            Console.WriteLine(line);
            lock (Lock)
            {
                Logs.Add(line);
                if (Logs.Count > 500)
                {
                    Logs.RemoveAt(0);
                }
            }
        }

        public static List<string> GetLogs()
        {
            lock (Lock)
            {
                return new List<string>(Logs);
            }
        }

        public static void Clear()
        {
            lock (Lock)
            {
                Logs.Clear();
            }
        }
    }
}

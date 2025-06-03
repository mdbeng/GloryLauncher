using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Linq;

namespace CanaryLauncherUpdate
{
    public class CountdownEvent
    {
        public string Name { get; set; }
        public DateTime EndTime { get; set; }
        public long TimestampMs { get; set; }
        
        // Calculate remaining time from current moment
        public TimeSpan GetRemainingTime()
        {
            var now = DateTime.Now;
            return EndTime > now ? EndTime - now : TimeSpan.Zero;
        }
        
        // Format the remaining time as a string
        public string GetFormattedRemainingTime()
        {
            var remaining = GetRemainingTime();
            
            if (remaining == TimeSpan.Zero)
                return "Event started!";
                
            if (remaining.Days > 0)
                return $"{remaining.Days}d {remaining.Hours}h {remaining.Minutes}m";
            else
                return $"{remaining.Hours}h {remaining.Minutes}m {remaining.Seconds}s";
        }
    }

    public class CountdownService
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const string COUNTDOWNS_URL = "https://gloryot.com/?countdowns";
        private static DateTime lastFetchTime = DateTime.MinValue;
        private static List<CountdownEvent> cachedCountdowns = new List<CountdownEvent>();
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(5); // Cache for 5 minutes

        static CountdownService()
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            httpClient.Timeout = TimeSpan.FromSeconds(10); // 10 second timeout
        }

        public static async Task<List<CountdownEvent>> FetchCountdownsAsync(bool forceRefresh = false)
        {
            // Check cache first (unless force refresh is requested)
            if (!forceRefresh && DateTime.Now - lastFetchTime < CACHE_DURATION && cachedCountdowns.Count > 0)
            {
                return cachedCountdowns;
            }

            try
            {
                string html = await httpClient.GetStringAsync(COUNTDOWNS_URL);
                
                // Extract the events JSON from the HTML
                var eventsMatch = Regex.Match(html, @"const events = (\[.*?\]);", RegexOptions.Singleline);
                
                if (eventsMatch.Success)
                {
                    string eventsJson = eventsMatch.Groups[1].Value;
                    var eventsList = JsonConvert.DeserializeObject<List<dynamic>>(eventsJson);
                    
                    var countdowns = new List<CountdownEvent>();
                    
                    foreach (var eventItem in eventsList)
                    {
                        string name = eventItem.name.ToString();
                        long timestamp = (long)eventItem.timestamp;
                        
                        // Convert timestamp to DateTime
                        var endTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime.ToLocalTime();
                        
                        countdowns.Add(new CountdownEvent
                        {
                            Name = name,
                            EndTime = endTime,
                            TimestampMs = timestamp
                        });
                    }
                    
                    // Cache the results
                    cachedCountdowns = countdowns;
                    lastFetchTime = DateTime.Now;
                    
                    return countdowns;
                }
                
                // If we couldn't extract the events, return the cached countdowns or fallback
                return cachedCountdowns.Count > 0 ? cachedCountdowns : GetFallbackCountdowns();
            }
            catch (Exception)
            {
                // If we have cached results, return them even if they're old
                if (cachedCountdowns.Count > 0)
                {
                    return cachedCountdowns;
                }
                
                // Return fallback data if fetching fails and no cache
                return GetFallbackCountdowns();
            }
        }

        private static List<CountdownEvent> GetFallbackCountdowns()
        {
            // Create some fallback countdowns in case we can't fetch from the website
            return new List<CountdownEvent>
            {
                new CountdownEvent
                {
                    Name = "Battle Royale",
                    EndTime = DateTime.Now.AddDays(1).Date.AddHours(20), // Tomorrow at 8 PM
                    TimestampMs = new DateTimeOffset(DateTime.Now.AddDays(1).Date.AddHours(20)).ToUnixTimeMilliseconds()
                },
                new CountdownEvent
                {
                    Name = "Double XP",
                    EndTime = DateTime.Now.AddDays(3).Date.AddHours(12), // In 3 days at noon
                    TimestampMs = new DateTimeOffset(DateTime.Now.AddDays(3).Date.AddHours(12)).ToUnixTimeMilliseconds()
                }
            };
        }
    }
}
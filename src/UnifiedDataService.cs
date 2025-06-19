using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Linq;

namespace CanaryLauncherUpdate
{
    public class BoostedCreature
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string ImageUrl { get; set; }
    }

    public class CountdownEvent
    {
        public string Name { get; set; }
        public DateTime EndTime { get; set; }
        public long TimestampMs { get; set; }

        public string GetFormattedRemainingTime()
        {
            var timeRemaining = EndTime - DateTime.Now;
            
            if (timeRemaining.TotalSeconds <= 0)
            {
                return "00:00:00";
            }

            if (timeRemaining.TotalDays >= 1)
            {
                return $"{(int)timeRemaining.TotalDays}d {timeRemaining.Hours:D2}:{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";
            }
            else
            {
                return $"{timeRemaining.Hours:D2}:{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";
            }
        }
    }

    public class UnifiedGameData
    {
        public List<NewsItem> News { get; set; } = new List<NewsItem>();
        public List<CountdownEvent> Countdowns { get; set; } = new List<CountdownEvent>();
        public BoostedCreature BoostedCreature { get; set; }
        public BoostedCreature BoostedBoss { get; set; }
        public DateTime FetchTime { get; set; }
        public bool IsFromCache { get; set; }
    }

    public class UnifiedDataService
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const string BASE_URL = "https://gloryot.com";
        private const string UNIFIED_API_URL = "https://gloryot.com/?apilauncher"; // Unified API endpoint
        private static DateTime lastFetchTime = DateTime.MinValue;
        private static UnifiedGameData cachedData = null;
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(5);

        static UnifiedDataService()
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        /// <summary>
        /// Fetches all launcher data (news, countdowns, boosted creatures) using minimal requests to stay under rate limit
        /// </summary>
        public static async Task<UnifiedGameData> FetchAllDataAsync(bool forceRefresh = false)
        {
            // Check cache first (unless force refresh is requested)
            if (!forceRefresh && DateTime.Now - lastFetchTime < CACHE_DURATION && cachedData != null)
            {
                cachedData.IsFromCache = true;
                return cachedData;
            }

            try
            {
                // Fetch data using minimal requests approach
                var unifiedData = await FetchUnifiedDataAsync();
                cachedData = unifiedData;
                lastFetchTime = DateTime.Now;
                cachedData.IsFromCache = false;
                return cachedData;
            }
            catch (Exception)
            {
                // If we have cached results, return them even if they're old
                if (cachedData != null)
                {
                    cachedData.IsFromCache = true;
                    return cachedData;
                }
                
                // Return fallback data if fetching fails and no cache
                return GetFallbackData();
            }
        }

        /// <summary>
        /// Fetches data using minimal requests (max 4 total: 2 base + 2 news content)
        /// </summary>
        private static async Task<UnifiedGameData> FetchUnifiedDataAsync()
        {
            // Create tasks for parallel execution - only 2 base requests
            var mainPageTask = httpClient.GetStringAsync(UNIFIED_API_URL);
            var newsArchiveTask = httpClient.GetStringAsync($"{BASE_URL}/?news/archive");

            // Wait for both requests to complete
            await Task.WhenAll(mainPageTask, newsArchiveTask);

            var mainPageHtml = await mainPageTask;
            var newsArchiveHtml = await newsArchiveTask;
            
            // Parse the unified HTML response that contains all data
            var unifiedData = new UnifiedGameData
            {
                FetchTime = DateTime.Now
            };

            // Extract boosted creatures from the main page
            var (creature, boss) = ExtractBoostedCreaturesFromHtml(mainPageHtml);
            unifiedData.BoostedCreature = creature;
            unifiedData.BoostedBoss = boss;

            // Extract countdowns from the main page
            unifiedData.Countdowns = ExtractCountdownsFromHtml(mainPageHtml);

            // Extract news from the news archive page - limit to 2 news items to stay under 5 requests total
            unifiedData.News = await ExtractNewsFromHtml(newsArchiveHtml, maxNewsItems: 2);

            return unifiedData;
        }

        private static (BoostedCreature creature, BoostedCreature boss) ExtractBoostedCreaturesFromHtml(string html)
        {
            var creature = ExtractBoostedCreature(html);
            var boss = ExtractBoostedBoss(html);
            return (creature, boss);
        }

        private static BoostedCreature ExtractBoostedCreature(string html)
        {
            try
            {
                var creatureMatch = Regex.Match(html, 
                    @"<img\s+id=""Creature""\s+src=""images/animated-outfits/animoutfit\.php\?id=(\d+)&addons=(\d+)&head=(\d+)&body=(\d+)&legs=(\d+)&feet=(\d+)&mount=(\d+)""\s+alt=""[^""]*""\s+title=""Today's boosted creature:\s*([^""]+)""",
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);

                if (creatureMatch.Success)
                {
                    return new BoostedCreature
                    {
                        Name = creatureMatch.Groups[8].Value.Trim(),
                        Type = "Creature",
                        ImageUrl = $"{BASE_URL}/images/animated-outfits/animoutfit.php?id={creatureMatch.Groups[1].Value}&addons={creatureMatch.Groups[2].Value}&head={creatureMatch.Groups[3].Value}&body={creatureMatch.Groups[4].Value}&legs={creatureMatch.Groups[5].Value}&feet={creatureMatch.Groups[6].Value}&mount={creatureMatch.Groups[7].Value}"
                    };
                }
            }
            catch (Exception)
            {
                // Ignore parsing errors
            }

            return GetFallbackCreature();
        }

        private static BoostedCreature ExtractBoostedBoss(string html)
        {
            try
            {
                var bossMatch = Regex.Match(html, 
                    @"<img\s+id=""Boss""\s+src=""images/animated-outfits/animoutfit\.php\?id=(\d+)&addons=(\d+)&head=(\d+)&body=(\d+)&legs=(\d+)&feet=(\d+)&mount=(\d+)""\s+alt=""[^""]*""\s+title=""Today's boosted boss:\s*([^""]+)""",
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);

                if (bossMatch.Success)
                {
                    return new BoostedCreature
                    {
                        Name = bossMatch.Groups[8].Value.Trim(),
                        Type = "Boss",
                        ImageUrl = $"{BASE_URL}/images/animated-outfits/animoutfit.php?id={bossMatch.Groups[1].Value}&addons={bossMatch.Groups[2].Value}&head={bossMatch.Groups[3].Value}&body={bossMatch.Groups[4].Value}&legs={bossMatch.Groups[5].Value}&feet={bossMatch.Groups[6].Value}&mount={bossMatch.Groups[7].Value}"
                    };
                }
            }
            catch (Exception)
            {
                // Ignore parsing errors
            }

            return GetFallbackBoss();
        }

        private static List<CountdownEvent> ExtractCountdownsFromHtml(string html)
        {
            try
            {
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
                        
                        var endTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime.ToLocalTime();
                        
                        countdowns.Add(new CountdownEvent
                        {
                            Name = name,
                            EndTime = endTime,
                            TimestampMs = timestamp
                        });
                    }
                    
                    return countdowns;
                }
            }
            catch (Exception)
            {
                // Ignore parsing errors
            }

            return GetFallbackCountdowns();
        }

        private static async Task<List<NewsItem>> ExtractNewsFromHtml(string archiveHtml, int maxNewsItems = 2)
        {
            try
            {
                var newsItems = new List<NewsItem>();
                
                var newsMatches = Regex.Matches(archiveHtml,
                    @"<tr[^>]*>.*?icon_(\d+)_small\.gif.*?(\d+\.\d+\.\d+).*?href=""([^""]*)"">([^<]+)</a>.*?</tr>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                // Limit to specified number of news items to avoid too many requests
                var limitedMatches = newsMatches.Cast<Match>().Take(maxNewsItems);
                
                // Create tasks for parallel content fetching
                var contentTasks = new List<Task<(NewsItem item, string content)>>();
                
                foreach (Match match in limitedMatches)
                {
                    if (match.Groups.Count >= 5)
                    {
                        var newsItem = new NewsItem
                        {
                            IconType = match.Groups[1].Value,
                            Date = match.Groups[2].Value.Trim(),
                            Url = match.Groups[3].Value,
                            Title = match.Groups[4].Value.Trim()
                        };

                        // Create task to fetch content
                        contentTasks.Add(FetchNewsContentAsync(newsItem));
                    }
                }

                // Wait for all content fetching to complete
                var results = await Task.WhenAll(contentTasks);
                
                // Combine results
                foreach (var (item, content) in results)
                {
                    item.Content = content;
                    newsItems.Add(item);
                }

                return newsItems;
            }
            catch (Exception)
            {
                return GetFallbackNews();
            }
        }

        private static async Task<(NewsItem item, string content)> FetchNewsContentAsync(NewsItem newsItem)
        {
            try
            {
                string fullUrl = newsItem.Url.StartsWith("http") ? newsItem.Url : BASE_URL + "/" + newsItem.Url.TrimStart('?');
                string html = await httpClient.GetStringAsync(fullUrl);
                
                var contentMatch = Regex.Match(html, 
                    @"<td[^>]*style=""padding-left:10px;padding-right:10px;""[^>]*><p>(.*?)</p></td>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (contentMatch.Success)
                {
                    string content = contentMatch.Groups[1].Value;
                    
                    // Clean up HTML tags and format for display
                    content = Regex.Replace(content, @"<br\s*/?>", " ", RegexOptions.IgnoreCase);
                    content = Regex.Replace(content, @"<img[^>]*>", "", RegexOptions.IgnoreCase);
                    content = Regex.Replace(content, @"<[^>]+>", "", RegexOptions.IgnoreCase);
                    content = System.Net.WebUtility.HtmlDecode(content);
                    content = Regex.Replace(content, @"\s+", " ", RegexOptions.Multiline);
                    content = content.Trim();
                    
                    if (content.Length > 150)
                    {
                        content = content.Substring(0, 150) + "...";
                    }
                    
                    return (newsItem, content);
                }
                
                return (newsItem, "Click to read the full article...");
            }
            catch
            {
                return (newsItem, $"📰 {newsItem.Title}\n📅 {newsItem.Date}\n\nClick to read the full article...");
            }
        }

        private static UnifiedGameData GetFallbackData()
        {
            return new UnifiedGameData
            {
                News = GetFallbackNews(),
                Countdowns = GetFallbackCountdowns(),
                BoostedCreature = GetFallbackCreature(),
                BoostedBoss = GetFallbackBoss(),
                FetchTime = DateTime.Now,
                IsFromCache = false
            };
        }

        private static List<NewsItem> GetFallbackNews()
        {
            return new List<NewsItem>
            {
                new NewsItem
                {
                    Title = "Welcome to GloryOT!",
                    Date = DateTime.Now.ToString("dd.MM.yyyy"),
                    Content = "🎮 New Features:\n• Enhanced Battle Royale system\n• 1 vs 1 duels with ranking\n• New PvP zones and events\n• Renovated guild system\n\n⚡ Recent Updates:\n• Improved class balance\n• New epic items and equipment\n• Performance optimization\n• Critical bug fixes",
                    IconType = "0"
                },
                new NewsItem
                {
                    Title = "Server Updates",
                    Date = DateTime.Now.AddDays(-1).ToString("dd.MM.yyyy"),
                    Content = "📅 Upcoming Events:\n• Guild tournament this weekend\n• Double experience event\n• New epic quest available\n\n⚠️ Important:\nGloryOT can be dangerous. Stay alert!",
                    IconType = "3"
                }
            };
        }

        private static List<CountdownEvent> GetFallbackCountdowns()
        {
            return new List<CountdownEvent>
            {
                new CountdownEvent
                {
                    Name = "Battle Royale",
                    EndTime = DateTime.Now.AddDays(1).Date.AddHours(20),
                    TimestampMs = new DateTimeOffset(DateTime.Now.AddDays(1).Date.AddHours(20)).ToUnixTimeMilliseconds()
                },
                new CountdownEvent
                {
                    Name = "Double XP",
                    EndTime = DateTime.Now.AddDays(3).Date.AddHours(12),
                    TimestampMs = new DateTimeOffset(DateTime.Now.AddDays(3).Date.AddHours(12)).ToUnixTimeMilliseconds()
                }
            };
        }

        private static BoostedCreature GetFallbackCreature()
        {
            return new BoostedCreature
            {
                Name = "Loading...",
                Type = "Creature",
                ImageUrl = null
            };
        }

        private static BoostedCreature GetFallbackBoss()
        {
            return new BoostedCreature
            {
                Name = "Loading...",
                Type = "Boss",
                ImageUrl = null
            };
        }
    }
}
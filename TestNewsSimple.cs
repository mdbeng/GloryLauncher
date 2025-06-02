using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace NewsTest
{
    class SimpleNewsTest
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Testing GloryOT News Fetching (Simple)...");
            Console.WriteLine("=========================================");
            
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", 
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                    
                    string html = await httpClient.GetStringAsync("https://gloryot.com/?news/archive");
                    
                    Console.WriteLine("HTML Length: " + html.Length);
                    Console.WriteLine();
                    
                    // Look for news table rows
                    var matches = Regex.Matches(html,
                        @"<tr[^>]*bgcolor=""[^""]*""[^>]*>.*?<img[^>]*src=""[^""]*news/icon_(\d+)_small\.gif""[^>]*>.*?<td[^>]*>([^<]+)</td>.*?<a[^>]*href=""([^""]*)"">([^<]+)</a>",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    
                    Console.WriteLine($"Found {matches.Count} news matches");
                    
                    foreach (Match match in matches)
                    {
                        Console.WriteLine($"Icon: {match.Groups[1].Value}");
                        Console.WriteLine($"Date: {match.Groups[2].Value.Trim()}");
                        Console.WriteLine($"URL: {match.Groups[3].Value}");
                        Console.WriteLine($"Title: {match.Groups[4].Value.Trim()}");
                        Console.WriteLine(new string('-', 40));
                    }
                    
                    if (matches.Count == 0)
                    {
                        Console.WriteLine("No matches found. Let's check for any news icons:");
                        var iconMatches = Regex.Matches(html, @"news/icon_\d+_small\.gif", RegexOptions.IgnoreCase);
                        Console.WriteLine($"Found {iconMatches.Count} news icons");
                        
                        foreach (Match iconMatch in iconMatches)
                        {
                            Console.WriteLine($"Icon: {iconMatch.Value}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NewsDebug
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Debugging GloryOT News Parsing...");
            Console.WriteLine("=================================");
            
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", 
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                    
                    string html = await httpClient.GetStringAsync("https://gloryot.com/?news/archive");
                    
                    Console.WriteLine($"HTML Length: {html.Length}");
                    Console.WriteLine();
                    
                    // Look for the table content section
                    var tableMatch = Regex.Match(html, @"<table class=""TableContent""[^>]*>(.*?)</table>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (tableMatch.Success)
                    {
                        Console.WriteLine("Found TableContent section");
                        string tableContent = tableMatch.Groups[1].Value;
                        
                        // Look for individual news rows
                        var newsMatches = Regex.Matches(tableContent,
                            @"<tr[^>]*bgcolor=""[^""]*""[^>]*>(.*?)</tr>",
                            RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        
                        Console.WriteLine($"Found {newsMatches.Count} table rows");
                        
                        foreach (Match match in newsMatches)
                        {
                            Console.WriteLine("Row content:");
                            Console.WriteLine(match.Groups[1].Value);
                            Console.WriteLine(new string('-', 50));
                            
                            // Try to extract news data from this row
                            var newsData = Regex.Match(match.Groups[1].Value,
                                @"<img\s+src=""[^""]*news/icon_(\d+)_small\.gif""[^>]*>.*?<td[^>]*>([^<]+)</td>.*?<a\s+href=""([^""]*)"">([^<]+)</a>",
                                RegexOptions.IgnoreCase | RegexOptions.Singleline);
                            
                            if (newsData.Success)
                            {
                                Console.WriteLine($"Icon: {newsData.Groups[1].Value}");
                                Console.WriteLine($"Date: {newsData.Groups[2].Value.Trim()}");
                                Console.WriteLine($"URL: {newsData.Groups[3].Value}");
                                Console.WriteLine($"Title: {newsData.Groups[4].Value.Trim()}");
                            }
                            else
                            {
                                Console.WriteLine("Could not extract news data from this row");
                            }
                            Console.WriteLine();
                        }
                    }
                    else
                    {
                        Console.WriteLine("Could not find TableContent section");
                        
                        // Look for any news icons
                        var iconMatches = Regex.Matches(html, @"news/icon_\d+_small\.gif", RegexOptions.IgnoreCase);
                        Console.WriteLine($"Found {iconMatches.Count} news icons in total HTML");
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
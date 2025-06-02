using System;
using System.Threading.Tasks;
using CanaryLauncherUpdate;

namespace NewsTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Testing GloryOT News Fetching...");
            Console.WriteLine("================================");
            
            try
            {
                var newsItems = await NewsService.FetchNewsAsync();
                
                if (newsItems != null && newsItems.Count > 0)
                {
                    Console.WriteLine($"Successfully fetched {newsItems.Count} news items:");
                    Console.WriteLine();
                    
                    foreach (var item in newsItems)
                    {
                        Console.WriteLine($"Title: {item.Title}");
                        Console.WriteLine($"Date: {item.Date}");
                        Console.WriteLine($"Icon Type: {item.IconType}");
                        Console.WriteLine($"Content: {item.Content.Substring(0, Math.Min(100, item.Content.Length))}...");
                        Console.WriteLine($"URL: {item.Url}");
                        Console.WriteLine(new string('-', 50));
                    }
                    
                    Console.WriteLine();
                    Console.WriteLine("Formatted news for display:");
                    Console.WriteLine(new string('=', 50));
                    Console.WriteLine(NewsService.FormatNewsForDisplay(newsItems));
                }
                else
                {
                    Console.WriteLine("No news items found or failed to fetch news.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error testing news service: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
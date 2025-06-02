using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

class QuickNewsTest
{
    static async Task Main()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            
            string html = await client.GetStringAsync("https://gloryot.com/?news/archive");
            
            Console.WriteLine("=== TESTING NEWS PARSING ===");
            
            // Test 1: Look for any news icons
            var icons = Regex.Matches(html, @"news/icon_\d+_small\.gif");
            Console.WriteLine($"Found {icons.Count} news icons");
            
            // Test 2: Look for news links
            var links = Regex.Matches(html, @"href=""[^""]*news/archive/\d+""");
            Console.WriteLine($"Found {links.Count} news links");
            
            // Test 3: Simple pattern for news rows
            var simpleRows = Regex.Matches(html, 
                @"<tr[^>]*>.*?icon_(\d+)_small\.gif.*?(\d+\.\d+\.\d+).*?href=""([^""]*)"">([^<]+)</a>.*?</tr>", 
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            Console.WriteLine($"Found {simpleRows.Count} news items with simple pattern");
            
            foreach (Match match in simpleRows)
            {
                Console.WriteLine($"Icon: {match.Groups[1].Value}");
                Console.WriteLine($"Date: {match.Groups[2].Value}");
                Console.WriteLine($"URL: {match.Groups[3].Value}");
                Console.WriteLine($"Title: {match.Groups[4].Value}");
                Console.WriteLine("---");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        
        Console.ReadKey();
    }
}
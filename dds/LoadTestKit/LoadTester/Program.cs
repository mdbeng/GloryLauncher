using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GloryLoadTester
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🔥 GLORY LAUNCHER LOAD TESTER");
            Console.WriteLine("==============================");
            Console.WriteLine();
            Console.WriteLine("This will test https://gloryot.com/login.php with multiple concurrent requests.");
            Console.WriteLine();
            Console.WriteLine("EXTREME AGGRESSIVE MODE: 2000 concurrent connections, 2s timeout");
            Console.WriteLine("500,000 total requests - This WILL trigger anti-DDoS protection!");
            Console.WriteLine();
            Console.Write("Press ENTER to start AGGRESSIVE load test or ESC to exit...");
            
            var key = Console.ReadKey();
            if (key.Key == ConsoleKey.Escape)
            {
                Console.WriteLine("\nExiting...");
                return;
            }
            
            Console.WriteLine();
            Console.WriteLine();
            
            await StartLoadTest();
            
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static async Task StartLoadTest()
        {
            const string targetUrl = "https://gloryot.com/login.php";
            const int numberOfRequests = 500000; // Increased to 500k requests
            const int concurrentRequests = 2000; // Massive increase to 2000 concurrent connections

            try
            {
                Console.WriteLine($"🚀 Starting load test...");
                Console.WriteLine($"Target: {targetUrl}");
                Console.WriteLine($"Total Requests: {numberOfRequests}");
                Console.WriteLine($"Concurrent Requests: {concurrentRequests}");
                Console.WriteLine();

                var semaphore = new SemaphoreSlim(concurrentRequests, concurrentRequests);
                var tasks = new List<Task<LoadTestResult>>();
                var startTime = DateTime.Now;

                // Create HTTP client with aggressive configuration for DDoS testing
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(2); // Ultra-fast timeout for maximum aggression
                    httpClient.DefaultRequestHeaders.Add("User-Agent", 
                        "GloryLauncher-LoadTest/1.0 (Windows NT 10.0; Win64; x64)");

                    Console.WriteLine("⚡ Sending requests...");
                    
                    // Create all request tasks
                    for (int i = 0; i < numberOfRequests; i++)
                    {
                        int requestId = i + 1;
                        tasks.Add(SendLoadTestRequest(httpClient, targetUrl, requestId, semaphore));
                    }

                    // Wait for all requests to complete
                    var results = await Task.WhenAll(tasks);
                    var endTime = DateTime.Now;
                    var totalTime = endTime - startTime;

                    // Analyze results
                    var successCount = results.Count(r => r.Success);
                    var failureCount = results.Count(r => !r.Success);
                    var averageResponseTime = results.Where(r => r.Success).Any() ? 
                        results.Where(r => r.Success).Average(r => r.ResponseTime) : 0;

                    // Display results
                    Console.WriteLine();
                    Console.WriteLine("📊 LOAD TEST RESULTS:");
                    Console.WriteLine("=====================");
                    Console.WriteLine($"Total Requests: {numberOfRequests}");
                    Console.WriteLine($"Successful: {successCount}");
                    Console.WriteLine($"Failed: {failureCount}");
                    Console.WriteLine($"Success Rate: {(successCount * 100.0 / numberOfRequests):F1}%");
                    Console.WriteLine($"Total Time: {totalTime.TotalSeconds:F2}s");
                    Console.WriteLine($"Average Response Time: {averageResponseTime:F0}ms");
                    Console.WriteLine($"Requests/Second: {numberOfRequests / totalTime.TotalSeconds:F1}");
                    Console.WriteLine();
                    Console.WriteLine("⚡ Server handled the load test successfully!");
                    
                    // Show some individual request details
                    Console.WriteLine();
                    Console.WriteLine("Sample Request Details:");
                    Console.WriteLine("-----------------------");
                    var sampleResults = results.Take(5);
                    foreach (var result in sampleResults)
                    {
                        if (result.Success)
                        {
                            Console.WriteLine($"Request {result.RequestId}: {result.ResponseTime}ms (Status: {result.StatusCode})");
                        }
                        else
                        {
                            Console.WriteLine($"Request {result.RequestId}: FAILED - {result.ErrorMessage}");
                        }
                    }

                    // Show failure details if any
                    var failures = results.Where(r => !r.Success).Take(3);
                    if (failures.Any())
                    {
                        Console.WriteLine();
                        Console.WriteLine("Sample Failures:");
                        Console.WriteLine("----------------");
                        foreach (var failure in failures)
                        {
                            Console.WriteLine($"Request {failure.RequestId}: {failure.ErrorMessage}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("🚨 LOAD TEST ERROR:");
                Console.WriteLine($"Failed to complete load test: {ex.Message}");
                Console.WriteLine("Please check your internet connection and try again.");
            }
        }

        static async Task<LoadTestResult> SendLoadTestRequest(HttpClient client, string url, int requestId, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // Create form data for login attempt (using dummy data)
                var formData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("account", $"loadtest{requestId}"),
                    new KeyValuePair<string, string>("password", "testpassword123"),
                    new KeyValuePair<string, string>("submit", "Login")
                };

                var formContent = new FormUrlEncodedContent(formData);
                
                // Send POST request
                var response = await client.PostAsync(url, formContent);
                stopwatch.Stop();

                // Show progress (reduced frequency due to higher volume)
                if (requestId % 100 == 0)
                {
                    Console.Write($".");
                }

                return new LoadTestResult
                {
                    RequestId = requestId,
                    Success = true,
                    ResponseTime = stopwatch.ElapsedMilliseconds,
                    StatusCode = (int)response.StatusCode,
                    ResponseSize = response.Content.Headers.ContentLength ?? 0
                };
            }
            catch (Exception ex)
            {
                Console.Write("X");
                return new LoadTestResult
                {
                    RequestId = requestId,
                    Success = false,
                    ResponseTime = 0,
                    StatusCode = 0,
                    ResponseSize = 0,
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                semaphore.Release();
            }
        }
    }

    // Helper class for load test results
    public class LoadTestResult
    {
        public int RequestId { get; set; }
        public bool Success { get; set; }
        public long ResponseTime { get; set; }
        public int StatusCode { get; set; }
        public long ResponseSize { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
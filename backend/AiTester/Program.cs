using System.Text.Json;
using Backend.Infrastructure.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting AI Test...");

        // Setup mock config
        var inMemorySettings = new Dictionary<string, string?> {
            {"LLM:BaseUrl", "https://p004-w001-runai-p004.runai-inference.dc.uz/v1/"},
            {"LLM:Token", "3266715e-63b1-422f-9b77-23972976e2d7"},
            {"LLM:Model", "google/gemma-4-31B-it"}
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<GemmaChildProfileExtractor>();
        
        var httpClient = new HttpClient();

        var extractor = new GemmaChildProfileExtractor(httpClient, configuration, logger);

        string message = "Hi, my 3 year old son Alex was playing outside today. It was very windy and cold, and he started coughing a lot. His asthma seems triggered. Also, he usually goes to sleep at 8:30 PM.";

        Console.WriteLine($"\nSending test message to Gemma AI:\n\"{message}\"");
        Console.WriteLine("\nWaiting for AI to process...");

        var result = await extractor.ExtractAsync(message, "user123", CancellationToken.None);

        var outputJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine("\n--- AI Result ---");
        Console.WriteLine(outputJson);

        var outputPath = @"C:\Users\aliak\Desktop\AI_Test_Result.txt";
        await File.WriteAllTextAsync(outputPath, $"Message:\n{message}\n\nAI JSON Output:\n{outputJson}");
        Console.WriteLine($"\nResult saved to {outputPath}");
    }
}

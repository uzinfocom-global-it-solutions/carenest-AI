using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var apiKey = "3266715e-63b1-422f-9b77-23972976e2d7";
        var url = "https://p004-w001-runai-p004.runai-inference.dc.uz/v1/chat/completions";
        var _httpClient = new HttpClient();
        var requestBody = new
        {
            model = "google/gemma-4-31B-it",
            messages = new[]
            {
                new { role = "system", content = "You are an AI assistant for a parenting app. Classify the user's message into an intent. Return ONLY a valid JSON object without markdown blocks. Allowed intents: recommendation_request, weather_query, reminder, question, message. Format: {\"intent\": \"...\", \"childName\": \"...\", \"topic\": \"...\"}" },
                new { role = "user", content = "MESSAGE: Через 2 часа Noah идёт играть в футбол." }
            },
            temperature = 0.1
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        try {
            using var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();
            Console.WriteLine(responseJson);
        } catch (Exception ex) {
            Console.WriteLine(ex);
        }
    }
}
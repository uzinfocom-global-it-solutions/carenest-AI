using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.Ai;

public sealed class GemmaChildProfileExtractor : IChildProfileExtractor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GemmaChildProfileExtractor> _logger;
    private readonly string _modelName;

    public GemmaChildProfileExtractor(
        HttpClient httpClient,
        IOptions<LLMOptions> options,
        ILogger<GemmaChildProfileExtractor> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        var opts = options.Value;

        var baseUrl = opts.BaseUrl?.TrimEnd('/') + "/"
            ?? throw new InvalidOperationException("LLM:BaseUrl is not configured.");
        var token = opts.Token
            ?? throw new InvalidOperationException("LLM:Token is not configured.");
        _modelName = opts.Model
            ?? throw new InvalidOperationException("LLM:Model is not configured.");

        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<ChildExtractionResult> ExtractAsync(
        string text,
        string sourceUserId,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = @"You are an AI assistant that extracts child profile insights from user chat messages.
Your ONLY output must be a valid JSON object. Do not output markdown code blocks or any other text.
The JSON must follow this exact schema:
{
  ""insights"": [
    {
      ""description"": ""string"",
      ""noteType"": ""Routine | Health | Behavior | General"",
      ""source"": ""Ai"",
      ""confidence"": 0.0 to 1.0,
      ""sensitivity"": { ""field"": ""ColdSensitive | HeatSensitive | UvSensitive | RespiratorySensitive | PollenSensitive | AirQualitySensitive"", ""value"": true },
      ""routine"": { ""title"": ""string"", ""routineType"": ""Sleep | Meal | Medication | Sport | School | Other"", ""startTime"": ""HH:mm"", ""endTime"": ""HH:mm"", ""locationType"": ""Indoor | Outdoor"", ""activityIntensity"": ""Low | Medium | High"" }
    }
  ]
}
If no insights are found, return { ""insights"": [] }.
IMPORTANT: For time, use strictly HH:mm format (24-hour). If endTime or sensitivity or routine are not applicable, omit them entirely or set to null.";

        var requestBody = new
        {
            model = _modelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = text }
            },
            temperature = 0.1,
            max_tokens = 1000
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("chat/completions", requestBody, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseData = await response.Content.ReadFromJsonAsync<OpenAiResponse>(cancellationToken: cancellationToken);
            var content = responseData?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;

            if (content.StartsWith("```json"))
            {
                content = content.Substring(7);
                if (content.EndsWith("```")) content = content.Substring(0, content.Length - 3);
            }
            else if (content.StartsWith("```"))
            {
                content = content.Substring(3);
                if (content.EndsWith("```")) content = content.Substring(0, content.Length - 3);
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<GemmaResponseSchema>(content.Trim(), options);

            if (result?.Insights == null) return new ChildExtractionResult([]);

            var mappedInsights = result.Insights.Select(i => new ChildInsight(
                Description: i.Description ?? "Extracted insight",
                NoteType: Enum.TryParse<NoteTypeEnum>(i.NoteType, true, out var nt) ? nt : NoteTypeEnum.Observation,
                Source: NoteSourceEnum.Ai,
                Confidence: i.Confidence,
                Sensitivity: i.Sensitivity != null && i.Sensitivity.Value
                    ? new SensitivityChange(i.Sensitivity.Field, true)
                    : null,
                Routine: i.Routine != null
                    ? new RoutineProposal(
                        Title: i.Routine.Title ?? "Routine",
                        RoutineType: Enum.TryParse<RoutineTypeEnum>(i.Routine.RoutineType, true, out var rt) ? rt : RoutineTypeEnum.Custom,
                        StartTime: TimeOnly.TryParse(i.Routine.StartTime, out var st) ? st : TimeOnly.MinValue,
                        EndTime: TimeOnly.TryParse(i.Routine.EndTime, out var et) ? et : null,
                        LocationType: Enum.TryParse<LocationTypeEnum>(i.Routine.LocationType, true, out var lt) ? lt : LocationTypeEnum.Indoor,
                        ActivityIntensity: Enum.TryParse<ActivityIntensityEnum>(i.Routine.ActivityIntensity, true, out var ai) ? ai : ActivityIntensityEnum.Low)
                    : null
            )).ToList();

            return new ChildExtractionResult(mappedInsights);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract insights using Gemma AI.");
            return new ChildExtractionResult([]);
        }
    }

    private class OpenAiResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }

    private class Message
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private class GemmaResponseSchema
    {
        public List<GemmaInsight>? Insights { get; set; }
    }

    private class GemmaInsight
    {
        public string? Description { get; set; }
        public string? NoteType { get; set; }
        public double Confidence { get; set; }
        public GemmaSensitivity? Sensitivity { get; set; }
        public GemmaRoutine? Routine { get; set; }
    }

    private class GemmaSensitivity
    {
        public string Field { get; set; } = string.Empty;
        public bool Value { get; set; }
    }

    private class GemmaRoutine
    {
        public string? Title { get; set; }
        public string? RoutineType { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? LocationType { get; set; }
        public string? ActivityIntensity { get; set; }
    }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Backend.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.Ai;

internal sealed class LLMClient : IAiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LLMClient> _logger;
    private readonly string _modelName;
    private readonly double _defaultTemperature;

    public LLMClient(
        HttpClient httpClient,
        IOptions<LLMOptions> options,
        ILogger<LLMClient> logger)
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
        _defaultTemperature = opts.Temperature ?? 0.3;

        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            messages.Add(new ChatMessage("system", request.SystemPrompt));

        if (request.History is { Count: > 0 })
            messages.AddRange(request.History.Select(m => new ChatMessage(m.Role, m.Content)));

        messages.Add(new ChatMessage("user", request.Prompt));

        var body = new ChatCompletionRequest(
            Model: _modelName,
            Messages: messages,
            Temperature: request.Temperature ?? _defaultTemperature);

        try
        {
            var response = await _httpClient.PostAsJsonAsync("chat/completions", body, ct);
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: ct);
            var content = data?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;

            return new AiCompletionResult(
                Content: content,
                Model: data?.Model ?? _modelName,
                PromptTokens: data?.Usage?.PromptTokens,
                CompletionTokens: data?.Usage?.CompletionTokens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM chat completion failed for model {Model}.", _modelName);
            return new AiCompletionResult(Content: string.Empty, Model: _modelName);
        }
    }

    private sealed record ChatMessage(string Role, string Content)
    {
        [JsonPropertyName("role")] public string Role { get; } = Role;
        [JsonPropertyName("content")] public string Content { get; } = Content;
    }

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature);

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("choices")] public List<Choice>? Choices { get; set; }
        [JsonPropertyName("usage")] public Usage? Usage { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")] public ChoiceMessage? Message { get; set; }
    }

    private sealed class ChoiceMessage
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
    }

    private sealed class Usage
    {
        [JsonPropertyName("prompt_tokens")] public int? PromptTokens { get; set; }
        [JsonPropertyName("completion_tokens")] public int? CompletionTokens { get; set; }
    }
}

public sealed class LLMOptions
{
    public const string SectionName = "LLM";

    public string? BaseUrl { get; set; }
    public string? Token { get; set; }
    public string? Model { get; set; }
    public double? Temperature { get; set; }
}

namespace Backend.Application.Common.Interfaces;

public interface IAiClient
{
    Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default);
}

public record AiCompletionRequest(
    string Prompt,
    string? SystemPrompt = null,
    IReadOnlyList<AiMessage>? History = null,
    double? Temperature = null);

public record AiMessage(string Role, string Content);

public record AiCompletionResult(
    string Content,
    string Model,
    int? PromptTokens = null,
    int? CompletionTokens = null);

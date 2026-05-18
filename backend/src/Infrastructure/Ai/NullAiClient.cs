using Backend.Application.Common.Interfaces;

namespace Backend.Infrastructure.Ai;

internal sealed class NullAiClient : IAiClient
{
    public const string ModelName = "null-ai";

    public Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default)
    {
        var content = string.IsNullOrEmpty(request.Prompt)
            ? "[null-ai] empty prompt"
            : $"[null-ai] {request.Prompt}";

        return Task.FromResult(new AiCompletionResult(content, ModelName, 0, 0));
    }
}

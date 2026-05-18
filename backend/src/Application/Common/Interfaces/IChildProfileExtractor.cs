using Backend.Domain.Enums;

namespace Backend.Application.Common.Interfaces;

/// <summary>
/// Extracts structured child-profile insights (sensitivities, routines, observations)
/// from free-text input. Default impl is rule-based; can be swapped for an LLM-backed
/// implementation that uses <see cref="IAiClient"/> without changing callers.
/// </summary>
public interface IChildProfileExtractor
{
    Task<ChildExtractionResult> ExtractAsync(
        string text,
        string sourceUserId,
        CancellationToken cancellationToken = default);
}

public record ChildExtractionResult(IReadOnlyList<ChildInsight> Insights);

public record ChildInsight(
    string Description,
    NoteTypeEnum NoteType,
    NoteSourceEnum Source,
    double Confidence,
    SensitivityChange? Sensitivity,
    RoutineProposal? Routine);

public record SensitivityChange(string Field, bool Value);

public record RoutineProposal(
    string Title,
    RoutineTypeEnum RoutineType,
    TimeOnly StartTime,
    TimeOnly? EndTime,
    LocationTypeEnum LocationType,
    ActivityIntensityEnum ActivityIntensity);

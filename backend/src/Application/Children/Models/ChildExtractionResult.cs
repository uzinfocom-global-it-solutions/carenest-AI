namespace Backend.Application.Children.Models;

public record ChildExtractionAppliedResult(
    int ChildId,
    int InsightCount,
    int AutoAppliedCount,
    int PendingConfirmationCount,
    IReadOnlyList<AppliedInsight> Applied,
    IReadOnlyList<PendingInsight> Pending);

public record AppliedInsight(
    int NoteId,
    string Description,
    double Confidence,
    string? SensitivityFieldChanged,
    int? RoutineId);

public record PendingInsight(
    int NoteId,
    string Description,
    double Confidence,
    string? ProposedSensitivityField,
    string? ProposedRoutineTitle);

using Backend.Domain.Enums;

namespace Backend.Application.Children.Models;

public record ChildResult(
    int Id,
    int FamilyId,
    string DisplayName,
    int AgeYears,
    AgeGroupEnum AgeGroup,
    DateTimeOffset CreatedAt,
    string? Gender = null
);

public record ChildSensitivityDto(
    bool HeatSensitive,
    bool ColdSensitive,
    bool AirQualitySensitive,
    bool PollenSensitive,
    bool UvSensitive,
    bool SkinSensitive,
    bool ActivitySensitive,
    bool RespiratorySensitive
);

public record ChildNoteDto(
    string NoteType,
    string Note,
    string Source,
    string? Tags = null,
    double? ConfidenceScore = null,
    bool NeedsConfirmation = false,
    DateTimeOffset? ObservedAt = null
);

public record ChildNoteResult(
    int Id,
    int ChildId,
    string NoteType,
    string Note,
    string Source,
    DateTimeOffset CreatedAt
);

public record ChildRoutineDto(
    string Title,
    string RoutineType,
    TimeOnly StartTime,
    TimeOnly? EndTime,
    string RepeatPattern,
    string LocationType,
    string ActivityIntensity,
    bool WeatherSensitive
);

public record ChildRoutineResult(
    int Id,
    int ChildId,
    string Title,
    string RoutineType,
    TimeOnly StartTime,
    bool Active
);

public record ChildRoutineListItem(
    int Id,
    int ChildId,
    string Title,
    string RoutineType,
    TimeOnly StartTime,
    TimeOnly? EndTime,
    string RepeatPattern,
    string LocationType,
    bool WeatherSensitive,
    bool Active
);

using Backend.Domain.Enums;

namespace Backend.Application.Users.Models;

public record UserSettingResult(
    string UserId,
    InputModeEnum PreferredInputMode,
    OutputModeEnum PreferredOutputMode,
    bool VoiceEnabled,
    NotificationLevelEnum NotificationLevel,
    ProactiveModeEnum ProactiveMode,
    TimeOnly? QuietHoursStart,
    TimeOnly? QuietHoursEnd,
    string PreferredLanguage,
    string Timezone,
    DateTimeOffset UpdatedAt);

public record UpdateUserSettingDto(
    string? PreferredInputMode,
    string? PreferredOutputMode,
    bool? VoiceEnabled,
    string? NotificationLevel,
    string? ProactiveMode,
    TimeOnly? QuietHoursStart,
    TimeOnly? QuietHoursEnd,
    string? PreferredLanguage,
    string? Timezone);

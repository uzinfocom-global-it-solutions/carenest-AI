using Backend.Application.Common.Interfaces;
using Backend.Application.Users.Models;
using Backend.Application.Users.Queries.GetUserSettings;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Users.Commands.UpdateUserSettings;

public record UpdateUserSettingsCommand(
    string UserId,
    UpdateUserSettingDto Settings) : IRequest<UserSettingResult>;

public class UpdateUserSettingsCommandHandler : IRequestHandler<UpdateUserSettingsCommand, UserSettingResult>
{
    private readonly IApplicationDbContext _context;

    public UpdateUserSettingsCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<UserSettingResult> Handle(UpdateUserSettingsCommand request, CancellationToken cancellationToken)
    {
        var setting = await _context.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken);

        if (setting == null)
        {
            setting = new UserSetting { UserId = request.UserId };
            _context.UserSettings.Add(setting);
        }

        var dto = request.Settings;

        if (dto.PreferredInputMode != null)
        {
            if (!Enum.TryParse<InputModeEnum>(dto.PreferredInputMode, ignoreCase: true, out var v))
                throw new ValidationException([new("preferredInputMode", $"Invalid input mode '{dto.PreferredInputMode}'.")]);
            setting.PreferredInputMode = v;
        }

        if (dto.PreferredOutputMode != null)
        {
            if (!Enum.TryParse<OutputModeEnum>(dto.PreferredOutputMode, ignoreCase: true, out var v))
                throw new ValidationException([new("preferredOutputMode", $"Invalid output mode '{dto.PreferredOutputMode}'.")]);
            setting.PreferredOutputMode = v;
        }

        if (dto.NotificationLevel != null)
        {
            if (!Enum.TryParse<NotificationLevelEnum>(dto.NotificationLevel, ignoreCase: true, out var v))
                throw new ValidationException([new("notificationLevel", $"Invalid notification level '{dto.NotificationLevel}'.")]);
            setting.NotificationLevel = v;
        }

        if (dto.ProactiveMode != null)
        {
            if (!Enum.TryParse<ProactiveModeEnum>(dto.ProactiveMode, ignoreCase: true, out var v))
                throw new ValidationException([new("proactiveMode", $"Invalid proactive mode '{dto.ProactiveMode}'.")]);
            setting.ProactiveMode = v;
        }

        if (dto.VoiceEnabled.HasValue)
            setting.VoiceEnabled = dto.VoiceEnabled.Value;

        if (dto.QuietHoursStart.HasValue)
            setting.QuietHoursStart = dto.QuietHoursStart;

        if (dto.QuietHoursEnd.HasValue)
            setting.QuietHoursEnd = dto.QuietHoursEnd;

        if (dto.PreferredLanguage != null)
            setting.PreferredLanguage = dto.PreferredLanguage;

        if (dto.Timezone != null)
            setting.Timezone = dto.Timezone;

        setting.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return GetUserSettingsQueryHandler.Map(setting);
    }
}

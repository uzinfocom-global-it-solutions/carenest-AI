using Backend.Application.Common.Interfaces;
using Backend.Application.Users.Models;
using Backend.Domain.Entities;

namespace Backend.Application.Users.Queries.GetUserSettings;

public record GetUserSettingsQuery(string UserId) : IRequest<UserSettingResult>;

public class GetUserSettingsQueryHandler : IRequestHandler<GetUserSettingsQuery, UserSettingResult>
{
    private readonly IApplicationDbContext _context;

    public GetUserSettingsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<UserSettingResult> Handle(GetUserSettingsQuery request, CancellationToken cancellationToken)
    {
        var setting = await _context.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken);

        if (setting == null)
        {
            setting = new UserSetting { UserId = request.UserId };
            _context.UserSettings.Add(setting);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Map(setting);
    }

    internal static UserSettingResult Map(UserSetting s) => new(
        s.UserId,
        s.PreferredInputMode,
        s.PreferredOutputMode,
        s.VoiceEnabled,
        s.NotificationLevel,
        s.ProactiveMode,
        s.QuietHoursStart,
        s.QuietHoursEnd,
        s.PreferredLanguage,
        s.Timezone,
        s.UpdatedAt);
}

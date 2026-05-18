using Backend.Domain.Common;
using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class UserSetting : BaseEntity
{
    public required string UserId { get; set; }
    public InputModeEnum PreferredInputMode { get; set; } = InputModeEnum.Text;
    public OutputModeEnum PreferredOutputMode { get; set; } = OutputModeEnum.Text;
    public bool VoiceEnabled { get; set; } = false;
    public NotificationLevelEnum NotificationLevel { get; set; } = NotificationLevelEnum.Normal;
    public ProactiveModeEnum ProactiveMode { get; set; } = ProactiveModeEnum.Balanced;
    public TimeOnly? QuietHoursStart { get; set; }
    public TimeOnly? QuietHoursEnd { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public string Timezone { get; set; } = "UTC";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

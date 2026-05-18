using Backend.Domain.Common;

namespace Backend.Domain.Entities;

public class MedicationSchedule : BaseEntity
{
    public required int ChildId { get; set; }
    public required string MedicationName { get; set; }
    public required string Dosage { get; set; }
    public required TimeOnly ScheduleTime { get; set; }
    public required string Timezone { get; set; } // e.g. "Europe/Moscow"
    public string RepeatPattern { get; set; } = "daily"; // daily, weekdays, weekends, custom
    public string? CustomDays { get; set; } // JSON array of day names when RepeatPattern=custom
    public bool VoiceEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = true;
    public bool RequiresConfirmation { get; set; } = true;
    public bool EscalationEnabled { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastTriggeredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Child Child { get; set; } = null!;
}

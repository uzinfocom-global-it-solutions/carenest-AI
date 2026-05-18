using Backend.Domain.Entities;

namespace Backend.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<AuthSession> AuthSessions { get; }

    DbSet<Family> Families { get; }
    DbSet<FamilyMember> FamilyMembers { get; }
    DbSet<UserSetting> UserSettings { get; }

    DbSet<Chat> Chats { get; }
    DbSet<ChatMessage> ChatMessages { get; }

    DbSet<Child> Children { get; }
    DbSet<ChildSensitivity> ChildSensitivities { get; }
    DbSet<ChildNote> ChildNotes { get; }
    DbSet<ChildRoutine> ChildRoutines { get; }

    DbSet<CalendarEvent> CalendarEvents { get; }

    DbSet<WeatherSnapshot> WeatherSnapshots { get; }

    DbSet<Recommendation> Recommendations { get; }
    DbSet<RecommendationFeedback> RecommendationFeedbacks { get; }
    DbSet<NotificationDelivery> NotificationDeliveries { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<TodoList> TodoLists { get; }
    DbSet<TodoItem> TodoItems { get; }

    DbSet<VoiceAction> VoiceActions { get; }
    DbSet<MedicationSchedule> MedicationSchedules { get; }

    DbSet<FamilyItem> FamilyItems { get; }
    DbSet<AIMemory> AIMemories { get; }
    DbSet<LeaveHomeChecklist> LeaveHomeChecklists { get; }

    DbSet<HealthMonitoringSession> HealthMonitoringSessions { get; }

    DbSet<AiDecisionLog> AiDecisionLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

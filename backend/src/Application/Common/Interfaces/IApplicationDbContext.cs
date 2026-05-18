using Backend.Domain.Entities;

namespace Backend.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // Auth
    DbSet<AuthSession> AuthSessions { get; }

    // Family
    DbSet<Family> Families { get; }
    DbSet<FamilyMember> FamilyMembers { get; }
    DbSet<UserSetting> UserSettings { get; }

    // Chat
    DbSet<Chat> Chats { get; }
    DbSet<ChatMessage> ChatMessages { get; }

    // Children
    DbSet<Child> Children { get; }
    DbSet<ChildSensitivity> ChildSensitivities { get; }
    DbSet<ChildNote> ChildNotes { get; }
    DbSet<ChildRoutine> ChildRoutines { get; }

    // Calendar
    DbSet<CalendarEvent> CalendarEvents { get; }

    // Weather
    DbSet<WeatherSnapshot> WeatherSnapshots { get; }

    // Recommendations
    DbSet<Recommendation> Recommendations { get; }
    DbSet<RecommendationFeedback> RecommendationFeedbacks { get; }
    DbSet<NotificationDelivery> NotificationDeliveries { get; }

    // System
    DbSet<AuditLog> AuditLogs { get; }

    // Legacy (keep for existing code)
    DbSet<TodoList> TodoLists { get; }
    DbSet<TodoItem> TodoItems { get; }

    // Voice / Medication
    DbSet<VoiceAction> VoiceActions { get; }
    DbSet<MedicationSchedule> MedicationSchedules { get; }

    // Proactive AI Layer
    DbSet<FamilyItem> FamilyItems { get; }
    DbSet<AIMemory> AIMemories { get; }
    DbSet<LeaveHomeChecklist> LeaveHomeChecklists { get; }

    // Health Monitoring
    DbSet<HealthMonitoringSession> HealthMonitoringSessions { get; }

    // AI Decision Observability
    DbSet<AiDecisionLog> AiDecisionLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

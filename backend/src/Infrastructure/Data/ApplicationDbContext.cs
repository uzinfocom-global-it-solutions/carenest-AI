using System.Reflection;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Backend.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    // Auth
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();

    // Family
    public DbSet<Family> Families => Set<Family>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();

    // Chat
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    // Children
    public DbSet<Child> Children => Set<Child>();
    public DbSet<ChildSensitivity> ChildSensitivities => Set<ChildSensitivity>();
    public DbSet<ChildNote> ChildNotes => Set<ChildNote>();
    public DbSet<ChildRoutine> ChildRoutines => Set<ChildRoutine>();

    // Calendar
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

    // Weather
    public DbSet<WeatherSnapshot> WeatherSnapshots => Set<WeatherSnapshot>();

    // Recommendations
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<RecommendationFeedback> RecommendationFeedbacks => Set<RecommendationFeedback>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    // System
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Legacy
    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    // Voice / Medication
    public DbSet<VoiceAction> VoiceActions => Set<VoiceAction>();
    public DbSet<MedicationSchedule> MedicationSchedules => Set<MedicationSchedule>();

    // Proactive AI Layer
    public DbSet<FamilyItem> FamilyItems => Set<FamilyItem>();
    public DbSet<AIMemory> AIMemories => Set<AIMemory>();
    public DbSet<LeaveHomeChecklist> LeaveHomeChecklists => Set<LeaveHomeChecklist>();

    // Health Monitoring
    public DbSet<HealthMonitoringSession> HealthMonitoringSessions => Set<HealthMonitoringSession>();

    // AI Decision Observability
    public DbSet<AiDecisionLog> AiDecisionLogs => Set<AiDecisionLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // FamilyItem configuration
        builder.Entity<FamilyItem>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Name).HasMaxLength(200).IsRequired();
            b.Property(e => e.Category).HasConversion<string>().HasMaxLength(50);
            b.Property(e => e.Notes).HasMaxLength(1000);
            b.HasOne(e => e.Family).WithMany().HasForeignKey(e => e.FamilyId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(e => e.Child).WithMany().HasForeignKey(e => e.ChildId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(e => e.FamilyId);
            b.HasIndex(e => new { e.FamilyId, e.IsActive });
        });

        // AIMemory configuration
        builder.Entity<AIMemory>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Key).HasMaxLength(200).IsRequired();
            b.Property(e => e.Value).HasMaxLength(2000).IsRequired();
            b.Property(e => e.MemoryType).HasConversion<string>().HasMaxLength(50);
            b.HasOne(e => e.Family).WithMany().HasForeignKey(e => e.FamilyId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => e.FamilyId);
            b.HasIndex(e => new { e.FamilyId, e.Key }).IsUnique();
        });

        // LeaveHomeChecklist configuration
        builder.Entity<LeaveHomeChecklist>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.ItemsJson).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.TriggerEventTitle).HasMaxLength(200);
            b.HasOne(e => e.Family).WithMany().HasForeignKey(e => e.FamilyId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => e.FamilyId);
            b.HasIndex(e => new { e.FamilyId, e.Date });
        });

        // HealthMonitoringSession configuration
        builder.Entity<HealthMonitoringSession>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.IssueType).HasConversion<string>().HasMaxLength(50);
            b.Property(e => e.Severity).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.EscalationLevel).HasConversion<string>().HasMaxLength(20);
            b.Property(e => e.LifecyclePhase).HasConversion<string>().HasMaxLength(30);
            b.Property(e => e.InitialSymptomDescription).HasMaxLength(1000);
            b.Property(e => e.LastUserResponse).HasMaxLength(1000);
            b.Property(e => e.ResolutionSummary).HasMaxLength(2000);
            b.Property(e => e.MonitoringChainId).HasMaxLength(50);
            b.Property(e => e.ResponseAnalysisJson).HasMaxLength(3000);
            b.Property(e => e.MonitoringPlanJson).HasColumnType("jsonb");
            b.HasOne(e => e.Family).WithMany().HasForeignKey(e => e.FamilyId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(e => e.Child).WithMany().HasForeignKey(e => e.ChildId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(e => e.FamilyId);
            b.HasIndex(e => new { e.FamilyId, e.IsResolved });
            b.HasIndex(e => new { e.FamilyId, e.IssueType, e.IsResolved });
            b.HasIndex(e => e.NextFollowUpAt).HasFilter("\"IsResolved\" = false AND \"NextFollowUpAt\" IS NOT NULL");
        });

        // AiDecisionLog configuration
        builder.Entity<AiDecisionLog>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.DecisionType).HasMaxLength(50).IsRequired();
            b.Property(e => e.Message).HasMaxLength(2000).IsRequired();
            b.Property(e => e.Priority).HasMaxLength(20);
            b.Property(e => e.DecisionChainId).HasMaxLength(50);
            b.Property(e => e.IdempotencyKey).HasMaxLength(200);
            b.Property(e => e.SuppressedBy).HasMaxLength(500);
            b.Property(e => e.ContextSnapshotJson).HasColumnType("jsonb");
            b.Property(e => e.EscalationRationale).HasMaxLength(1000);
            b.Property(e => e.Metadata).HasMaxLength(2000);
            b.HasOne(e => e.Family).WithMany().HasForeignKey(e => e.FamilyId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => e.FamilyId);
            b.HasIndex(e => new { e.FamilyId, e.DecisionType, e.CreatedAt });
            b.HasIndex(e => e.DecisionChainId);
        });
    }
}

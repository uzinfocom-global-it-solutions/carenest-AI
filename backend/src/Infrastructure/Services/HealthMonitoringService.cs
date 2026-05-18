using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public sealed class HealthMonitoringService : IHealthMonitoringService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<HealthMonitoringService> _logger;

    public HealthMonitoringService(IApplicationDbContext db, ILogger<HealthMonitoringService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<HealthMonitoringSession> StartOrUpdateSessionAsync(
        int familyId, int? childId, MonitoringIssueType issueType,
        string symptomDescription, RiskAssessment risk, CancellationToken ct = default)
    {
        var existing = await FindExistingSessionAsync(familyId, childId, issueType, ct);

        if (existing is not null)
        {
            existing.Severity = risk.Severity;
            existing.RiskScore = risk.Score;
            existing.EscalationLevel = risk.EscalationLevel;
            existing.LastUpdatedAt = DateTimeOffset.UtcNow;
            existing.InitialSymptomDescription ??= symptomDescription;
            if (risk.Severity > existing.Severity)
                existing.Status = MonitoringStatus.Escalated;

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Updated monitoring session {Id} for family {FamilyId}: severity={Severity}, score={Score}",
                existing.Id, familyId, risk.Severity, risk.Score);
            return existing;
        }

        var plan = BuildMonitoringPlan(issueType, risk);
        var nextFollowUp = DateTimeOffset.UtcNow.AddMinutes(risk.NextFollowUpMinutes);

        var session = new HealthMonitoringSession
        {
            FamilyId = familyId,
            ChildId = childId,
            IssueType = issueType,
            Severity = risk.Severity,
            RiskScore = risk.Score,
            EscalationLevel = risk.EscalationLevel,
            Status = MonitoringStatus.Active,
            LifecyclePhase = MonitoringLifecyclePhase.Detected,
            MonitoringChainId = Guid.NewGuid().ToString("N")[..16],
            InitialSymptomDescription = symptomDescription,
            MonitoringPlanJson = plan,
            NextFollowUpAt = nextFollowUp,
        };

        _db.HealthMonitoringSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Started monitoring session {Id} for family {FamilyId}, issue={IssueType}, severity={Severity}",
            session.Id, familyId, issueType, risk.Severity);

        return session;
    }

    public async Task UpdateSessionRiskAsync(
        int sessionId, RiskAssessment risk, string? userResponse, CancellationToken ct = default)
    {
        var session = await _db.HealthMonitoringSessions.FindAsync([sessionId], ct);
        if (session is null) return;

        var escalated = risk.Severity > session.Severity;
        session.Severity = risk.Severity > session.Severity ? risk.Severity : session.Severity;
        session.RiskScore = risk.Score;
        session.EscalationLevel = risk.EscalationLevel;
        session.LastUpdatedAt = DateTimeOffset.UtcNow;
        session.LastUserResponse = userResponse;
        session.LastInteractionAt = DateTimeOffset.UtcNow;
        if (escalated) session.Status = MonitoringStatus.Escalated;

        await _db.SaveChangesAsync(ct);
    }

    public async Task ResolveSessionAsync(int sessionId, string summary, CancellationToken ct = default)
    {
        var session = await _db.HealthMonitoringSessions.FindAsync([sessionId], ct);
        if (session is null) return;

        session.IsResolved = true;
        session.ResolvedAt = DateTimeOffset.UtcNow;
        session.Status = MonitoringStatus.Resolved;
        session.ResolutionSummary = summary;
        session.NextFollowUpAt = null;
        session.LastUpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Monitoring session {Id} resolved: {Summary}", sessionId, summary);
    }

    public async Task MarkFollowUpSentAsync(
        int sessionId, DateTimeOffset nextFollowUpAt, CancellationToken ct = default)
    {
        var session = await _db.HealthMonitoringSessions.FindAsync([sessionId], ct);
        if (session is null) return;

        session.FollowUpCount++;
        session.NextFollowUpAt = nextFollowUpAt;
        session.LastUpdatedAt = DateTimeOffset.UtcNow;
        if (session.Status == MonitoringStatus.Active)
            session.Status = MonitoringStatus.Monitoring;

        await _db.SaveChangesAsync(ct);
    }

    public async Task IncrementMissedFollowUpAsync(int sessionId, CancellationToken ct = default)
    {
        var session = await _db.HealthMonitoringSessions.FindAsync([sessionId], ct);
        if (session is null) return;

        session.MissedFollowUps++;
        session.LastUpdatedAt = DateTimeOffset.UtcNow;

        if (session.MissedFollowUps >= 3 && session.EscalationLevel < MonitoringEscalationLevel.High)
            session.EscalationLevel = MonitoringEscalationLevel.High;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<HealthMonitoringSession>> GetActiveSessionsForFamilyAsync(
        int familyId, CancellationToken ct = default)
    {
        return await _db.HealthMonitoringSessions
            .Where(s => s.FamilyId == familyId && !s.IsResolved)
            .Include(s => s.Child)
            .OrderByDescending(s => s.RiskScore)
            .ToListAsync(ct);
    }

    public async Task<HealthMonitoringSession?> FindExistingSessionAsync(
        int familyId, int? childId, MonitoringIssueType issueType, CancellationToken ct = default)
    {
        return await _db.HealthMonitoringSessions
            .Where(s => s.FamilyId == familyId
                     && s.ChildId == childId
                     && s.IssueType == issueType
                     && !s.IsResolved
                     && s.StartedAt >= DateTimeOffset.UtcNow.AddHours(-24))
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpdateLifecyclePhaseAsync(
        int sessionId, MonitoringLifecyclePhase phase, CancellationToken ct = default)
    {
        var session = await _db.HealthMonitoringSessions.FindAsync([sessionId], ct);
        if (session is null) return;
        session.LifecyclePhase = phase;
        session.LastUpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("Session {Id} lifecycle → {Phase}", sessionId, phase);
    }

    public async Task UpdatePredictedRiskAsync(
        int sessionId, int predictedRiskScore, DateTimeOffset? predictedEscalationAt,
        CancellationToken ct = default)
    {
        var session = await _db.HealthMonitoringSessions.FindAsync([sessionId], ct);
        if (session is null) return;
        session.PredictedRiskScore = predictedRiskScore;
        session.PredictedEscalationAt = predictedEscalationAt;
        session.LastUpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateResponseAnalysisAsync(
        int sessionId, string userResponse, string analysisJson,
        CancellationToken ct = default)
    {
        var session = await _db.HealthMonitoringSessions.FindAsync([sessionId], ct);
        if (session is null) return;
        session.LastUserResponse = userResponse.Length > 1000 ? userResponse[..1000] : userResponse;
        session.ResponseAnalysisJson = analysisJson.Length > 3000 ? analysisJson[..3000] : analysisJson;
        session.LastInteractionAt = DateTimeOffset.UtcNow;
        session.LastUpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<HealthMonitoringSession>> GetSessionsAwaitingFollowUpAsync(
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.HealthMonitoringSessions
            .Include(s => s.Child)
            .Where(s => !s.IsResolved
                     && s.NextFollowUpAt != null
                     && s.NextFollowUpAt <= now)
            .OrderBy(s => s.NextFollowUpAt)
            .ToListAsync(ct);
    }

    private static string BuildMonitoringPlan(MonitoringIssueType issueType, RiskAssessment risk)
    {
        var steps = issueType switch
        {
            MonitoringIssueType.Fever => new[]
            {
                new { minutesFromStart = risk.NextFollowUpMinutes,      question = "Температура снизилась? Сколько сейчас градусов?" },
                new { minutesFromStart = risk.NextFollowUpMinutes * 3,  question = "Как самочувствие? Принимали жаропонижающее?" },
                new { minutesFromStart = risk.NextFollowUpMinutes * 6,  question = "Температура в норме? Нужна ли консультация врача?" },
            },
            MonitoringIssueType.Asthma => new[]
            {
                new { minutesFromStart = 15,  question = "Дыхание восстановилось? Использовали ингалятор?" },
                new { minutesFromStart = 45,  question = "Как чувствует себя? Есть хрипы или стеснение в груди?" },
                new { minutesFromStart = 120, question = "Ситуация улучшилась? Нужна ли врачебная помощь?" },
            },
            MonitoringIssueType.Vomiting => new[]
            {
                new { minutesFromStart = 20,  question = "Рвота прекратилась? Удалось ли выпить немного воды?" },
                new { minutesFromStart = 60,  question = "Как состояние? Есть ли признаки обезвоживания?" },
                new { minutesFromStart = 180, question = "Полностью лучше? Если нет — стоит обратиться к врачу." },
            },
            MonitoringIssueType.Cough => new[]
            {
                new { minutesFromStart = 30,  question = "Кашель стал реже? Нет ли затруднений с дыханием?" },
                new { minutesFromStart = 120, question = "Как самочувствие? Есть ли температура?" },
            },
            _ => new[]
            {
                new { minutesFromStart = risk.NextFollowUpMinutes,     question = "Как самочувствие? Есть ли улучшение?" },
                new { minutesFromStart = risk.NextFollowUpMinutes * 2, question = "Ситуация улучшилась? Нужна ли помощь?" },
            },
        };

        return JsonSerializer.Serialize(steps);
    }
}

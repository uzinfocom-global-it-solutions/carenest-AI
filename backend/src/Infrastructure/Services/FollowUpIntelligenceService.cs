using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

/// Semantic analysis engine for user responses to health monitoring follow-ups.
/// Extracts temperature readings, symptom trajectory signals, and criticality indicators.
/// Drives adaptive follow-up intervals and session lifecycle transitions.
public sealed class FollowUpIntelligenceService : IFollowUpIntelligenceService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<FollowUpIntelligenceService> _logger;

    private static readonly Regex TemperatureRegex = new(
        @"(?:температура|темп\.?|t°?)\s*[=:]?\s*(\d{2}(?:[.,]\d)?)|(\d{2}(?:[.,]\d)?)\s*°",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] ImprovementTokens =
    [
        "лучше", "улучшилось", "улучшился", "улучшилась", "прошло", "прошла", "нормально",
        "норма", "снизилась", "спала", "легче", "прошёл", "прошла", "выздоровел", "здоров",
        "passed", "better", "improved", "fine", "normal", "recovered", "feel good"
    ];

    private static readonly string[] DeteriorationTokens =
    [
        "хуже", "ухудшилось", "ухудшился", "не спала", "не снижается", "растёт", "поднялась",
        "увеличилась", "рвота", "снова", "опять", "вернулась", "держится", "не проходит",
        "worse", "worsened", "higher", "still", "not better", "no improvement", "getting worse"
    ];

    private static readonly string[] CriticalTokens =
    [
        "скорую", "скорая", "врача", "госпиталь", "больниц", "тяжело дышит", "не дышит",
        "судороги", "потерял сознание", "без сознания", "синюшн", "ambulance", "emergency",
        "hospital", "cannot breathe", "unconscious", "seizure", "turning blue"
    ];

    public FollowUpIntelligenceService(
        IApplicationDbContext db,
        ILogger<FollowUpIntelligenceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ResponseAnalysisResult> AnalyzeResponseAsync(
        int sessionId, string userResponse, CancellationToken ct = default)
    {
        var session = await _db.HealthMonitoringSessions
            .Include(s => s.Child)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null)
        {
            return new ResponseAnalysisResult(
                sessionId, SymptomChangeType.Unknown, null, 0, false, false, 30,
                "Session not found", null);
        }

        var text = userResponse.ToLowerInvariant();
        var change = ClassifySymptomChange(text);
        var temp = ExtractTemperature(userResponse);
        var isCritical = CriticalTokens.Any(t => text.Contains(t));

        int scoreModifier = change switch
        {
            SymptomChangeType.Improved  => -20,
            SymptomChangeType.Worsened  => +25,
            SymptomChangeType.Stable    => +5,
            _                           => +8,
        };

        if (temp.HasValue)
        {
            scoreModifier += temp.Value switch
            {
                > 40.0 => +30,
                > 39.5 => +20,
                > 39.0 => +10,
                > 38.5 => 0,
                < 37.5 => -20,
                < 38.0 => -10,
                _      => 0,
            };
        }

        if (isCritical) scoreModifier += 40;

        bool shouldEscalate =
            isCritical ||
            (change == SymptomChangeType.Worsened && session.Severity >= MonitoringSeverity.High);

        bool shouldResolve =
            change == SymptomChangeType.Improved &&
            session.Severity <= MonitoringSeverity.Medium &&
            session.FollowUpCount >= 2 &&
            !isCritical;

        int nextInterval = isCritical ? 5 :
            change == SymptomChangeType.Improved ? 60 :
            change == SymptomChangeType.Worsened ? 15 : 30;

        string summary = BuildAnalysisSummary(change, temp, isCritical, scoreModifier);

        var analysisJson = JsonSerializer.Serialize(new
        {
            change = change.ToString(),
            temperature = temp,
            isCritical,
            scoreModifier,
            shouldEscalate,
            shouldResolve,
            nextIntervalMinutes = nextInterval,
            analyzedAt = DateTimeOffset.UtcNow,
            rawResponse = userResponse.Length > 500 ? userResponse[..500] : userResponse,
        });

        _logger.LogInformation(
            "Follow-up response analyzed for session {SessionId}: change={Change}, temp={Temp:F1}, modifier={Modifier}, escalate={Escalate}",
            sessionId, change, temp, scoreModifier, shouldEscalate);

        return new ResponseAnalysisResult(
            sessionId, change, temp, scoreModifier,
            shouldEscalate, shouldResolve, nextInterval, summary, analysisJson);
    }

    public async Task<AdaptiveFollowUp?> GenerateNextFollowUpAsync(
        HealthMonitoringSession session, CancellationToken ct = default)
    {
        if (session.IsResolved) return null;

        var severity = session.Severity;
        var followUpIdx = session.FollowUpCount;

        // Derive adaptive interval from current severity
        int severityInterval = severity switch
        {
            MonitoringSeverity.Emergency => 5,
            MonitoringSeverity.Critical  => 10,
            MonitoringSeverity.High      => 20,
            MonitoringSeverity.Medium    => 30,
            _                            => 60,
        };

        // Pull next question from the monitoring plan JSON if available
        if (!string.IsNullOrEmpty(session.MonitoringPlanJson))
        {
            try
            {
                var plan = JsonSerializer.Deserialize<JsonElement[]>(session.MonitoringPlanJson);
                if (plan is not null && followUpIdx < plan.Length)
                {
                    var step = plan[followUpIdx];
                    if (step.TryGetProperty("question", out var q) && q.GetString() is { } question)
                    {
                        return new AdaptiveFollowUp(
                            question,
                            severityInterval,
                            severity,
                            severity >= MonitoringSeverity.Critical);
                    }
                }
            }
            catch { /* fall through to default */ }
        }

        return new AdaptiveFollowUp(
            DefaultQuestion(session.IssueType),
            severityInterval,
            severity,
            severity >= MonitoringSeverity.Critical);
    }

    public async Task<IReadOnlyList<FollowUpChainEntry>> GetFollowUpChainAsync(
        int sessionId, CancellationToken ct = default)
    {
        var session = await _db.HealthMonitoringSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null || string.IsNullOrEmpty(session.MonitoringPlanJson))
            return [];

        var chain = new List<FollowUpChainEntry>();
        try
        {
            var plan = JsonSerializer.Deserialize<JsonElement[]>(session.MonitoringPlanJson);
            if (plan is not null)
            {
                for (int i = 0; i < plan.Length; i++)
                {
                    string question = plan[i].TryGetProperty("question", out var q)
                        ? q.GetString() ?? ""
                        : "";
                    chain.Add(new FollowUpChainEntry(i, question, null, null, null));
                }
            }
        }
        catch { }

        return chain.AsReadOnly();
    }

    public async Task<HealthMonitoringSession?> FindSessionAwaitingResponseAsync(
        int familyId, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-2);
        return await _db.HealthMonitoringSessions
            .Where(s =>
                s.FamilyId == familyId &&
                !s.IsResolved &&
                s.NextFollowUpAt != null &&
                s.NextFollowUpAt < DateTimeOffset.UtcNow &&
                s.NextFollowUpAt > cutoff)
            .OrderByDescending(s => s.RiskScore)
            .FirstOrDefaultAsync(ct);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static SymptomChangeType ClassifySymptomChange(string lowerText)
    {
        if (CriticalTokens.Any(t => lowerText.Contains(t)))
            return SymptomChangeType.Worsened;

        bool improved     = ImprovementTokens.Any(t => lowerText.Contains(t));
        bool deteriorated = DeteriorationTokens.Any(t => lowerText.Contains(t));

        if (improved && !deteriorated) return SymptomChangeType.Improved;
        if (deteriorated)             return SymptomChangeType.Worsened;

        if (lowerText.Contains("так же") || lowerText.Contains("без изменений") ||
            lowerText.Contains("same")   || lowerText.Contains("no change"))
            return SymptomChangeType.Stable;

        return SymptomChangeType.Unknown;
    }

    private static double? ExtractTemperature(string text)
    {
        var match = TemperatureRegex.Match(text);
        if (!match.Success) return null;

        var raw = (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)
                  .Replace(',', '.');

        return double.TryParse(
            raw,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var val) ? val : null;
    }

    private static string BuildAnalysisSummary(
        SymptomChangeType change, double? temp, bool isCritical, int scoreModifier)
    {
        var parts = new List<string>
        {
            change switch
            {
                SymptomChangeType.Improved => "Состояние улучшилось",
                SymptomChangeType.Worsened => "Состояние ухудшилось",
                SymptomChangeType.Stable   => "Состояние стабильное",
                _                          => "Состояние неизвестно",
            }
        };
        if (temp.HasValue)  parts.Add($"температура {temp:F1}°C");
        if (isCritical)     parts.Add("🚨 требуется экстренная помощь");
        parts.Add($"корректировка риска: {(scoreModifier >= 0 ? "+" : "")}{scoreModifier}");
        return string.Join(", ", parts);
    }

    private static string DefaultQuestion(MonitoringIssueType issueType) => issueType switch
    {
        MonitoringIssueType.Fever               => "Как температура сейчас? Стало лучше?",
        MonitoringIssueType.Asthma              => "Дыхание восстановилось? Использовали ингалятор?",
        MonitoringIssueType.Vomiting            => "Рвота прекратилась? Удалось выпить немного воды?",
        MonitoringIssueType.Cough               => "Кашель стал реже? Есть затруднения с дыханием?",
        MonitoringIssueType.Allergy             => "Аллергическая реакция прошла? Принимали антигистаминные?",
        MonitoringIssueType.RespiratoryMonitoring => "Как дыхание сейчас? Нет стеснения в груди?",
        MonitoringIssueType.Hydration           => "Удалось выпить достаточно воды? Как самочувствие?",
        _                                       => "Как самочувствие сейчас? Есть ли улучшение?",
    };
}

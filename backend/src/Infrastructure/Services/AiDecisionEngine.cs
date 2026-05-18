using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public sealed class AiDecisionEngine : IAiDecisionEngine
{
    private readonly IAiClient _ai;
    private readonly IHealthRiskScoringService _riskScoring;
    private readonly IPredictiveHealthRiskService _predictive;
    private readonly IAIMemoryService _memory;
    private readonly ILogger<AiDecisionEngine> _logger;

    public AiDecisionEngine(
        IAiClient ai,
        IHealthRiskScoringService riskScoring,
        IPredictiveHealthRiskService predictive,
        IAIMemoryService memory,
        ILogger<AiDecisionEngine> logger)
    {
        _ai = ai;
        _riskScoring = riskScoring;
        _predictive = predictive;
        _memory = memory;
        _logger = logger;
    }

    public async Task<AiDecisionResult> AnalyzeAsync(
        FamilyProactiveContext context,
        IReadOnlyList<HealthMonitoringSession> activeSessions,
        CancellationToken ct = default)
    {
        var chainId = Guid.NewGuid().ToString("N")[..16];
        var decisions = new List<AiDecision>();
        var sessionUpdates = new List<SessionRiskUpdate>();
        var predictedRisks = new List<PredictedRisk>();

        var familyMemory = await _memory.GetFamilyMemoryAsync(context.FamilyId, ct);

        foreach (var session in activeSessions)
        {
            var child = context.Children.FirstOrDefault(c => c.ChildId == session.ChildId);

            var risk = _riskScoring.CalculateRisk(
                session.IssueType, child, context.Weather,
                session.InitialSymptomDescription,
                session.FollowUpCount, session.MissedFollowUps,
                session.Severity);

            var prediction = await _predictive.PredictRiskAsync(
                session, child, context.Weather, familyMemory, ct);
            predictedRisks.Add(prediction);

            var previousSeverity = session.Severity;
            var effectiveSeverity = risk.Severity > session.Severity ? risk.Severity : session.Severity;

            sessionUpdates.Add(new SessionRiskUpdate(
                session.Id,
                effectiveSeverity,
                risk.EscalationLevel,
                risk.Score,
                NextFollowUpTime(risk, session),
                false,
                null));

            var contextSnapshot = BuildContextSnapshot(context, session, risk, prediction);

            var childName = child?.Name ?? "ребёнок";

            if (effectiveSeverity >= MonitoringSeverity.Critical)
            {
                var urgentMsg = session.IssueType == MonitoringIssueType.Fever
                    ? $"⚠️ Температура у {childName} критическая! Необходима срочная консультация врача или скорая помощь."
                    : $"⚠️ Состояние {childName} требует срочной медицинской помощи! Риск {risk.Score}/100.";

                decisions.Add(new AiDecision(
                    AiDecisionType.EscalationAlert, urgentMsg,
                    NotificationPriority.Emergency, VoiceActionType.EmergencyAlert,
                    session.ChildId, session.Id, null,
                    DecisionMeta(session, chainId, contextSnapshot)));
            }
            else if (effectiveSeverity == MonitoringSeverity.High &&
                     session.EscalationLevel < MonitoringEscalationLevel.High)
            {
                decisions.Add(new AiDecision(
                    AiDecisionType.HealthAlert,
                    $"Состояние {childName} ухудшилось (риск {risk.Score}/100). Следите внимательно, при необходимости обратитесь к врачу.",
                    NotificationPriority.High, VoiceActionType.Custom,
                    session.ChildId, session.Id, null,
                    DecisionMeta(session, chainId, contextSnapshot)));
            }

            if (prediction.EscalationProbability > 0.65 &&
                effectiveSeverity <= MonitoringSeverity.Medium)
            {
                var predictiveMsg = BuildPredictiveAlertMessage(childName, session, prediction);
                decisions.Add(new AiDecision(
                    AiDecisionType.PredictiveRiskAlert, predictiveMsg,
                    NotificationPriority.High, VoiceActionType.ProactiveRecommendation,
                    session.ChildId, session.Id, null,
                    DecisionMeta(session, chainId, contextSnapshot)));
            }

            if (session.NextFollowUpAt.HasValue &&
                session.NextFollowUpAt <= DateTimeOffset.UtcNow.AddMinutes(2))
            {
                var followUpQuestion = GetNextFollowUpQuestion(session);
                if (followUpQuestion is not null)
                {
                    decisions.Add(new AiDecision(
                        AiDecisionType.FollowUpCheck, followUpQuestion,
                        effectiveSeverity >= MonitoringSeverity.High
                            ? NotificationPriority.High
                            : NotificationPriority.Normal,
                        VoiceActionType.FollowUpCheck, session.ChildId, session.Id,
                        new AiFollowUpSchedule(risk.NextFollowUpMinutes, followUpQuestion, effectiveSeverity),
                        DecisionMeta(session, chainId, contextSnapshot)));
                }
            }

            if (session.NextFollowUpAt.HasValue &&
                session.NextFollowUpAt < DateTimeOffset.UtcNow.AddMinutes(-10) &&
                session.MissedFollowUps > 0)
            {
                var overdueMsg = $"{childName} не ответил(а) на вопрос о самочувствии. Проверьте состояние ребёнка.";
                decisions.Add(new AiDecision(
                    AiDecisionType.HealthAlert, overdueMsg,
                    NotificationPriority.High, VoiceActionType.Custom,
                    session.ChildId, session.Id, null,
                    DecisionMeta(session, chainId, contextSnapshot)));
            }

            if (effectiveSeverity <= MonitoringSeverity.Low &&
                session.FollowUpCount >= 3 &&
                session.MissedFollowUps == 0)
            {
                sessionUpdates[^1] = sessionUpdates[^1] with
                {
                    ShouldResolve = true,
                    ResolutionSummary = $"Состояние {childName} стабилизировалось после {session.FollowUpCount} наблюдений. Риск снизился до {risk.Score}/100."
                };

                decisions.Add(new AiDecision(
                    AiDecisionType.MonitoringResolved,
                    $"Мониторинг {childName} завершён — состояние нормализовалось.",
                    NotificationPriority.Normal, VoiceActionType.ProactiveRecommendation,
                    session.ChildId, session.Id, null,
                    DecisionMeta(session, chainId, contextSnapshot)));
            }

            await _memory.RecordObservationAsync(
                context.FamilyId,
                $"symptom:{session.IssueType}:{session.ChildId}",
                $"severity={effectiveSeverity}, score={risk.Score}, followups={session.FollowUpCount}",
                AIMemoryType.SymptomHistory, ct);
        }

        if (context.Weather is { } w)
        {
            foreach (var child in context.Children)
            {
                if (w.AqiIndex > 150 &&
                    child.Sensitivities.Any(s =>
                        s.Contains("астм", StringComparison.OrdinalIgnoreCase) ||
                        s.Contains("asthma", StringComparison.OrdinalIgnoreCase) ||
                        s.Contains("дыхательн", StringComparison.OrdinalIgnoreCase) ||
                        s.Contains("respiratory", StringComparison.OrdinalIgnoreCase)))
                {
                    decisions.Add(new AiDecision(
                        AiDecisionType.AqiWarning,
                        $"AQI {w.AqiIndex} — опасный уровень для {child.Name}. Не выходите на улицу, закройте окна.",
                        NotificationPriority.High, VoiceActionType.ProactiveRecommendation,
                        child.ChildId, null, null,
                        new Dictionary<string, string>
                        {
                            ["childId"] = child.ChildId.ToString(),
                            ["aqi"] = w.AqiIndex?.ToString() ?? "",
                            ["chainId"] = chainId
                        }));
                }
                else if (w.AqiIndex > 100 &&
                         child.Sensitivities.Any(s =>
                             s.Contains("воздух", StringComparison.OrdinalIgnoreCase) ||
                             s.Contains("air quality", StringComparison.OrdinalIgnoreCase)))
                {
                    decisions.Add(new AiDecision(
                        AiDecisionType.AqiWarning,
                        $"Качество воздуха ухудшилось (AQI {w.AqiIndex}). Ограничьте время прогулки {child.Name} на улице.",
                        NotificationPriority.Normal, VoiceActionType.ProactiveRecommendation,
                        child.ChildId, null, null,
                        new Dictionary<string, string> { ["childId"] = child.ChildId.ToString(), ["chainId"] = chainId }));
                }

                if (w.TemperatureCelsius > 38 &&
                    child.Sensitivities.Any(s =>
                        s.Contains("жар", StringComparison.OrdinalIgnoreCase) ||
                        s.Contains("heat", StringComparison.OrdinalIgnoreCase)))
                {
                    decisions.Add(new AiDecision(
                        AiDecisionType.WeatherWarning,
                        $"Опасная жара ({w.TemperatureCelsius:0}°C)! {child.Name} чувствителен к жаре — держитесь в прохладе, давайте воду каждые 30 мин.",
                        NotificationPriority.High, VoiceActionType.ProactiveRecommendation,
                        child.ChildId, null, null,
                        new Dictionary<string, string> { ["childId"] = child.ChildId.ToString(), ["chainId"] = chainId }));
                }

                if (w.TemperatureCelsius < 0 &&
                    child.Sensitivities.Any(s =>
                        s.Contains("холод", StringComparison.OrdinalIgnoreCase) ||
                        s.Contains("cold", StringComparison.OrdinalIgnoreCase)))
                {
                    decisions.Add(new AiDecision(
                        AiDecisionType.WeatherWarning,
                        $"Сильный мороз ({w.TemperatureCelsius:0}°C). {child.Name} чувствителен к холоду — одевайте слоями, ограничьте время на улице.",
                        NotificationPriority.High, VoiceActionType.ProactiveRecommendation,
                        child.ChildId, null, null,
                        new Dictionary<string, string> { ["childId"] = child.ChildId.ToString(), ["chainId"] = chainId }));
                }
            }
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var med in context.TodayMedications)
        {
            var medTime = new DateTimeOffset(now.Date.Add(med.ScheduleTime.ToTimeSpan()), now.Offset);
            var minutesUntil = (medTime - now).TotalMinutes;
            if (minutesUntil is >= -5 and <= 10)
            {
                decisions.Add(new AiDecision(
                    AiDecisionType.MedicationReminder,
                    $"Время принять {med.MedicationName} ({med.Dosage}) для {med.ChildName}.",
                    NotificationPriority.High, VoiceActionType.MedicationReminder,
                    null, null, null,
                    new Dictionary<string, string>
                    {
                        ["scheduleId"] = med.ScheduleId.ToString(),
                        ["childName"] = med.ChildName,
                        ["chainId"] = chainId
                    }));
            }
        }

        if (context.Children.Count > 0)
        {
            string? proactiveMsg = null;
            try
            {
                var memCtx = await _memory.BuildMemoryContextAsync(context.FamilyId, ct);
                var prompt  = BuildProactivePrompt(context, activeSessions, memCtx);
                var result  = await _ai.CompleteAsync(new AiCompletionRequest(
                    Prompt: prompt,
                    SystemPrompt: ProactiveSystemPrompt,
                    Temperature: 0.5), ct);

                var candidate = result.Content.Trim();
                if (!string.IsNullOrWhiteSpace(candidate) && !candidate.StartsWith("[null-ai]"))
                    proactiveMsg = candidate;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Proactive LLM message generation failed — using deterministic fallback");
            }

            proactiveMsg ??= BuildDeterministicFallback(context);

            if (!string.IsNullOrWhiteSpace(proactiveMsg))
            {
                decisions.Add(new AiDecision(
                    AiDecisionType.ProactiveMessage, proactiveMsg,
                    NotificationPriority.Normal, VoiceActionType.ProactiveRecommendation,
                    null, null, null,
                    new Dictionary<string, string> { ["chainId"] = chainId, ["source"] = "context" }));
            }
        }

        _logger.LogInformation(
            "AiDecisionEngine chain={ChainId}: {Decisions} decisions, {Updates} session updates, {Predictions} predictions for family {FamilyId}",
            chainId, decisions.Count, sessionUpdates.Count, predictedRisks.Count, context.FamilyId);

        return new AiDecisionResult(
            decisions.AsReadOnly(),
            sessionUpdates.AsReadOnly(),
            predictedRisks.AsReadOnly(),
            chainId);
    }


    private static DateTimeOffset? NextFollowUpTime(RiskAssessment risk, HealthMonitoringSession session)
    {
        if (session.IsResolved) return null;
        return DateTimeOffset.UtcNow.AddMinutes(risk.NextFollowUpMinutes);
    }

    private static string? GetNextFollowUpQuestion(HealthMonitoringSession session)
    {
        if (string.IsNullOrEmpty(session.MonitoringPlanJson)) return null;
        try
        {
            var plan = JsonSerializer.Deserialize<JsonElement[]>(session.MonitoringPlanJson);
            if (plan is null || plan.Length == 0) return null;
            var idx = Math.Min(session.FollowUpCount, plan.Length - 1);
            return plan[idx].TryGetProperty("question", out var q) ? q.GetString() : null;
        }
        catch { return null; }
    }

    private static Dictionary<string, string> DecisionMeta(
        HealthMonitoringSession session, string chainId, string contextSnapshot)
        => new()
        {
            ["sessionId"] = session.Id.ToString(),
            ["issueType"] = session.IssueType.ToString(),
            ["severity"]  = session.Severity.ToString(),
            ["chainId"]   = chainId,
        };

    private static string BuildContextSnapshot(
        FamilyProactiveContext context,
        HealthMonitoringSession session,
        RiskAssessment risk,
        PredictedRisk prediction)
    {
        return JsonSerializer.Serialize(new
        {
            familyId = context.FamilyId,
            sessionId = session.Id,
            issueType = session.IssueType.ToString(),
            currentSeverity = session.Severity.ToString(),
            riskScore = risk.Score,
            riskFactors = risk.RiskFactors,
            escalationProbability = prediction.EscalationProbability,
            predictedSeverity = prediction.PredictedSeverity.ToString(),
            timeToEscalationMin = prediction.TimeToEscalationMinutes,
            aqi = context.Weather?.AqiIndex,
            tempC = context.Weather?.TemperatureCelsius,
            followUpCount = session.FollowUpCount,
            missedFollowUps = session.MissedFollowUps,
            analyzedAt = DateTimeOffset.UtcNow,
        });
    }

    private static string BuildPredictiveAlertMessage(
        string childName, HealthMonitoringSession session, PredictedRisk prediction)
    {
        var timeHint = prediction.TimeToEscalationMinutes switch
        {
            <= 30  => "в ближайшие 30 минут",
            <= 90  => "в течение часа",
            <= 180 => "в течение 3 часов",
            _      => "в ближайшее время"
        };

        return session.IssueType switch
        {
            MonitoringIssueType.Fever =>
                $"⚠️ Прогноз: риск ухудшения температуры у {childName} — {timeHint} (вероятность {prediction.EscalationProbability:P0}). Следите внимательно.",
            MonitoringIssueType.Asthma or MonitoringIssueType.RespiratoryMonitoring =>
                $"⚠️ Прогноз: возможное обострение дыхательных симптомов у {childName} — {timeHint}. Держите ингалятор под рукой.",
            _ =>
                $"⚠️ Прогноз: риск ухудшения состояния {childName} {timeHint} (вероятность {prediction.EscalationProbability:P0}). Наблюдайте внимательнее."
        };
    }

    private static string BuildProactivePrompt(
        FamilyProactiveContext context,
        IReadOnlyList<HealthMonitoringSession> sessions,
        string memoryContext)
    {
        var parts = new List<string>();
        parts.Add($"Время: {DateTimeOffset.UtcNow.ToLocalTime():HH:mm}.");

        if (context.Weather is { } w)
            parts.Add($"Погода: {w.ConditionSummary}, {w.TemperatureCelsius:0}°C" +
                      (w.AqiIndex.HasValue ? $", AQI {w.AqiIndex}" : "") + ".");

        foreach (var child in context.Children)
        {
            var sens = child.Sensitivities.Count > 0
                ? $" (чувствителен к: {string.Join(", ", child.Sensitivities)})"
                : "";
            parts.Add($"Ребёнок: {child.Name}, {child.AgeYears} лет{sens}.");
        }

        if (sessions.Count > 0)
            foreach (var s in sessions)
                parts.Add($"Активный мониторинг: {s.IssueType} (severity={s.Severity}, score={s.RiskScore}).");

        if (!string.IsNullOrEmpty(memoryContext))
            parts.Add(memoryContext);

        if (context.TodayEvents.Count > 0)
        {
            var ev = context.TodayEvents.First();
            parts.Add($"Событие сегодня: {ev.Title} в {ev.StartTime.ToLocalTime():HH:mm}.");
        }

        parts.Add("Дай один краткий проактивный совет или уточняющий вопрос по текущей ситуации.");
        return string.Join(" ", parts);
    }

    private static string? BuildDeterministicFallback(FamilyProactiveContext context)
    {
        if (context.Children.Count == 0) return null;

        var now = DateTimeOffset.UtcNow;
        var parts = new List<string>();

        var nextEvent = context.TodayEvents
            .Where(e => e.StartTime > now && e.StartTime <= now.AddHours(3))
            .OrderBy(e => e.StartTime)
            .FirstOrDefault();

        if (nextEvent is not null)
        {
            var minsUntil = (int)(nextEvent.StartTime - now).TotalMinutes;
            var timeStr = minsUntil < 60
                ? $"через {minsUntil} мин"
                : $"в {nextEvent.StartTime.ToLocalTime():HH:mm}";
            var who = nextEvent.ChildName != "Семья"
                ? $" для {nextEvent.ChildName}" : "";
            parts.Add($"Скоро событие{who}: «{nextEvent.Title}» — {timeStr}.");
        }

        var nextMed = context.TodayMedications
            .Select(m => (med: m, medTime: new DateTimeOffset(
                now.Date.Add(m.ScheduleTime.ToTimeSpan()), now.Offset)))
            .Where(x => x.medTime > now.AddMinutes(11) && x.medTime <= now.AddHours(2))
            .OrderBy(x => x.medTime)
            .FirstOrDefault();

        if (nextMed.med is not null)
        {
            var minsToMed = (int)(nextMed.medTime - now).TotalMinutes;
            parts.Add($"Через {minsToMed} мин: {nextMed.med.MedicationName} для {nextMed.med.ChildName}.");
        }

        if (context.Weather is { } w)
        {
            foreach (var child in context.Children)
            {
                var sensitivities = child.Sensitivities;

                if (w.AqiIndex > 100 && sensitivities.Any(s =>
                    s.Contains("воздух", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("дыхание", StringComparison.OrdinalIgnoreCase)))
                {
                    parts.Add($"AQI {w.AqiIndex} — {child.Name} чувствителен к воздуху, лучше остаться дома.");
                    break;
                }

                if (w.TemperatureCelsius > 32 && sensitivities.Any(s =>
                    s.Contains("жара", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("heat", StringComparison.OrdinalIgnoreCase)))
                {
                    parts.Add($"Жара {w.TemperatureCelsius:0}°C, {child.Name} чувствителен — давайте больше воды, избегайте солнца.");
                    break;
                }

                if (w.TemperatureCelsius < 3 && sensitivities.Any(s =>
                    s.Contains("холод", StringComparison.OrdinalIgnoreCase) ||
                    s.Contains("cold", StringComparison.OrdinalIgnoreCase)))
                {
                    parts.Add($"Мороз {w.TemperatureCelsius:0}°C, {child.Name} чувствителен к холоду — одевайте теплее.");
                    break;
                }
            }

            if (parts.Count == 0)
            {
                var allNames = context.Children.Count == 1
                    ? context.Children[0].Name
                    : string.Join(" и ", context.Children.Select(c => c.Name));

                if (w.AqiIndex > 150)
                    parts.Add($"Качество воздуха опасное (AQI {w.AqiIndex}). Ограничьте прогулки {allNames}.");
                else if (w.IsRaining || w.RainChancePercent > 60)
                    parts.Add($"Дождь сегодня — не забудьте зонт для {allNames}.");
                else if (w.TemperatureCelsius > 35)
                    parts.Add($"Сильная жара {w.TemperatureCelsius:0}°C — давайте {allNames} больше воды.");
                else if (w.TemperatureCelsius < 0)
                    parts.Add($"Мороз {w.TemperatureCelsius:0}°C — одевайте {allNames} тепло.");
            }
        }

        if (parts.Count > 0)
            return string.Join(" ", parts);

        var child0 = context.Children[0];
        var eventCount = context.TodayEvents.Count;
        var medCount   = context.TodayMedications.Count;

        if (eventCount > 0 || medCount > 0)
        {
            var summaryParts = new List<string>();
            if (eventCount > 0)
                summaryParts.Add($"{eventCount} событий в расписании");
            if (medCount > 0)
                summaryParts.Add($"{medCount} приёмов лекарств");
            return $"Сегодня у {child0.Name}: {string.Join(", ", summaryParts)}. Всё под контролем?";
        }

        var hour = now.ToLocalTime().Hour;
        return hour switch
        {
            >= 7 and < 10  => child0.AgeYears < 7
                ? $"Доброе утро! Как чувствует себя {child0.Name}? Готов(а) к новому дню?"
                : $"Доброе утро! Проверьте настроение {child0.Name} перед школой.",
            >= 10 and < 13 => $"Как себя чувствует {child0.Name} сегодня утром?",
            >= 13 and < 16 => $"Как прошёл день у {child0.Name}? Не забудьте про обед.",
            >= 16 and < 19 => $"Добрый вечер! Как прошёл день у {child0.Name}?",
            >= 19 and < 22 => $"Вечер — готовьте {child0.Name} ко сну по расписанию.",
            _ => null,
        };
    }

    private const string ProactiveSystemPrompt =
        """
        Ты — автономный ИИ-ассистент CareNestAI для семьи. Анализируй контекст и давай
        ОДИН краткий совет или уточняющий вопрос (1-2 предложения, только русский,
        без markdown, без приветствий). Фокусируйся на самом важном риске прямо сейчас.
        Учитывай память о предыдущих наблюдениях. НЕ повторяй советы из памяти.
        """;
}

using System.Text.Json;
using Backend.Application.Chats.Commands.ConfirmProposal;
using Backend.Application.Chats.Models;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Application.Recommendations.Commands.GenerateRecommendations;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Application.Chats.Commands.SendMessage;

public record SendMessageCommand(
    int ChatId,
    string UserId,
    string Content,
    string InputMode,
    string Locale = "en") : IRequest<ChatMessageResult>;

public class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, ChatMessageResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;
    private readonly IIntentParser _intentParser;
    private readonly ISender _mediator;
    private readonly IAuditService _audit;
    private readonly IVoiceActionService _voiceService;
    private readonly IHealthMonitoringService _healthMonitoring;
    private readonly IHealthRiskScoringService _riskScoring;
    private readonly IProactiveContextService _proactiveContext;
    private readonly IFollowUpIntelligenceService _followUpIntelligence;
    private readonly ISseConnectionManager _sse;
    private readonly ILogger<SendMessageCommandHandler> _logger;

    public SendMessageCommandHandler(
        IApplicationDbContext context,
        IFamilyAuthorization authorization,
        IIntentParser intentParser,
        ISender mediator,
        IAuditService audit,
        IVoiceActionService voiceService,
        IHealthMonitoringService healthMonitoring,
        IHealthRiskScoringService riskScoring,
        IProactiveContextService proactiveContext,
        IFollowUpIntelligenceService followUpIntelligence,
        ISseConnectionManager sse,
        ILogger<SendMessageCommandHandler> logger)
    {
        _context = context;
        _authorization = authorization;
        _intentParser = intentParser;
        _mediator = mediator;
        _audit = audit;
        _voiceService = voiceService;
        _healthMonitoring = healthMonitoring;
        _riskScoring = riskScoring;
        _proactiveContext = proactiveContext;
        _followUpIntelligence = followUpIntelligence;
        _sse = sse;
        _logger = logger;
    }

    public async Task<ChatMessageResult> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var chat = await _context.Chats.FindAsync([request.ChatId], cancellationToken)
            ?? throw new NotFoundException(nameof(Chat), request.ChatId);

        await _authorization.AssertMemberAsync(chat.FamilyId, request.UserId, cancellationToken);

        if (!Enum.TryParse<InputModeEnum>(request.InputMode, ignoreCase: true, out var mode))
            throw new ValidationException([new("inputMode", $"Invalid input mode '{request.InputMode}'.")]);

        // Load full family/children context and recent chat history BEFORE saving the current message,
        // so history contains only previous messages (not the one being processed now).
        var family = await _context.Families.FindAsync([chat.FamilyId], cancellationToken);

        var children = await _context.Children
            .Include(c => c.Sensitivity)
            .Include(c => c.Notes.OrderByDescending(n => n.CreatedAt).Take(15))
            .Include(c => c.Routines.Where(r => r.Active))
            .Where(c => c.FamilyId == chat.FamilyId)
            .ToListAsync(cancellationToken);

        // 40 messages = solid memory of the full conversation thread.
        var recentHistory = await _context.ChatMessages
            .Where(m => m.ChatId == request.ChatId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(40)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ConversationMessage(
                m.SenderType == SenderTypeEnum.User ? "user" : "assistant",
                m.Content))
            .ToListAsync(cancellationToken);

        // For very long chats, prepend a one-line summary so older context
        // isn't completely lost even if it falls outside the 40-message window.
        string? conversationSummary = null;
        var totalMessages = await _context.ChatMessages
            .CountAsync(m => m.ChatId == request.ChatId, cancellationToken);
        if (totalMessages > 40)
        {
            // Grab the first 10 messages (conversation seed) as a memory anchor.
            var seedRaw = await _context.ChatMessages
                .Where(m => m.ChatId == request.ChatId)
                .OrderBy(m => m.CreatedAt)
                .Take(10)
                .Select(m => m.Content)
                .ToListAsync(cancellationToken);
            var seed = seedRaw.Select(c => c.Length > 120 ? c[..120] + "…" : c);
            conversationSummary = $"Earlier in this conversation ({totalMessages - 40} messages ago): "
                + string.Join(" | ", seed);
        }

        var knownKids = children
            .Select(c => new KnownChild(c.Id, c.DisplayName, c.AgeYears))
            .ToList();

        var childrenDetail = children.Select(c =>
        {
            ChildSensitivityContext? sensitivity = null;
            if (c.Sensitivity is { } s)
            {
                sensitivity = new ChildSensitivityContext(
                    s.HeatSensitive, s.ColdSensitive, s.AirQualitySensitive,
                    s.PollenSensitive, s.UvSensitive, s.SkinSensitive,
                    s.ActivitySensitive, s.RespiratorySensitive);
            }

            var notes = c.Notes
                .Select(n => $"[{n.NoteType}] {n.Note}")
                .ToList();

            var routines = c.Routines
                .Where(r => r.Active)
                .Select(r => $"{r.Title} ({r.RoutineType}, {r.StartTime:hh\\:mm}-{(r.EndTime.HasValue ? r.EndTime.Value.ToString("hh\\:mm") : "?")}, {r.RepeatPattern})")
                .ToList();

            return new ChildContext(c.Id, c.DisplayName, c.AgeYears, c.AgeGroup.ToString(), sensitivity, notes, routines);
        }).ToList();

        // Latest weather snapshot from the most recently-updated location.
        // Gives the LLM real numbers so it can answer "what's the weather"
        // and reason about clothing/outdoor safety with actual data.
        var latestWeather = await _context.WeatherSnapshots
            .OrderByDescending(w => w.CollectedAt)
            .Take(1)
            .Select(w => new WeatherContext(
                w.LocationKey,
                w.WeatherCondition.ToString(),
                w.TemperatureC,
                w.FeelsLikeC,
                w.HumidityPercent,
                w.WindKmh,
                w.UvIndex,
                w.Aqi,
                w.PollenLevel,
                w.CollectedAt))
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        // Uzbekistan is UTC+5 (UZT). We use UZT midnight as the window start
        // so all events scheduled for today in Uzbekistan are always visible
        // to the AI, even if it's still yesterday in UTC.
        var uztOffset = TimeSpan.FromHours(5);
        var nowUzt = now.ToOffset(uztOffset);
        var todayStartUzt = new DateTimeOffset(nowUzt.Date, uztOffset); // midnight UZT
        var windowStart = todayStartUzt.ToUniversalTime();
        var upcomingRaw = await _context.CalendarEvents
            .Where(e => e.FamilyId == chat.FamilyId
                     && e.StartDatetime >= windowStart
                     && e.StartDatetime <= now.AddHours(48))
            .OrderBy(e => e.StartDatetime)
            .Take(20)
            .Select(e => new
            {
                e.Title,
                e.ChildId,
                e.StartDatetime,
                LocationType = e.LocationType.ToString(),
                e.WeatherSensitive,
            })
            .ToListAsync(cancellationToken);

        // Build a lookup so we can resolve ChildId → DisplayName without a second round-trip.
        var childNameLookup = children.ToDictionary(c => c.Id, c => c.DisplayName);

        var upcomingEventContexts = upcomingRaw
            .Select(e => new UpcomingEventContext(
                e.Title,
                e.ChildId.HasValue && childNameLookup.TryGetValue(e.ChildId.Value, out var cn) ? cn : "Family",
                e.StartDatetime,
                e.LocationType,
                e.WeatherSensitive))
            .ToList();

        // Recent recommendations so AI avoids repeating the same advice.
        var recentRecs = await _context.Recommendations
            .Where(r => r.FamilyId == chat.FamilyId
                     && r.CreatedAt >= now.AddDays(-3))
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .Select(r => $"[{r.RecommendationType}] {r.Title}: {r.Message}")
            .ToListAsync(cancellationToken);

        var intentContext = new IntentContext(
            chat.FamilyId,
            family?.Name ?? "Family",
            knownKids,
            childrenDetail,
            recentHistory,
            latestWeather,
            upcomingEventContexts,
            recentRecs,
            conversationSummary);

        var userMessage = new ChatMessage
        {
            ChatId = request.ChatId,
            UserId = request.UserId,
            SenderType = SenderTypeEnum.User,
            MessageType = MessageTypeEnum.Message,
            InputMode = mode,
            Content = request.Content,
        };

        _context.ChatMessages.Add(userMessage);
        await _context.SaveChangesAsync(cancellationToken);

        ParsedIntent? intent = null;
        string? aiResponse;
        var successful = true;
        var aiMessageType = MessageTypeEnum.Message;

        try
        {
            intent = await _intentParser.ParseAsync(request.Content, request.UserId, intentContext, request.Locale, cancellationToken);
            userMessage.ParsedIntent = BuildParsedIntentJson(intent);

            if (intent.Proposal is { } proposal)
            {
                aiMessageType = MessageTypeEnum.Proposal;
                aiResponse = intent.Reply ?? proposal.Summary + " — confirm to apply.";
            }
            else
            {
                aiMessageType = intent.Intent switch
                {
                    "question" => MessageTypeEnum.Question,
                    "recommendation_request" => MessageTypeEnum.Recommendation,
                    "reminder" => MessageTypeEnum.Reminder,
                    _ => MessageTypeEnum.Message
                };

                if (intent.TriggersRecommendation)
                {
                    // Fire-and-forget recommendation generation — failures must not fail the chat message
                    foreach (var child in children)
                    {
                        try { await _mediator.Send(new GenerateRecommendationsCommand(child.Id, request.UserId), cancellationToken); }
                        catch { /* recommendation failures are non-fatal */ }
                    }
                }

                aiResponse = intent.Reply ?? $"I understood your message about '{intent.Topic ?? request.Content}'.";
            }
        }
        catch
        {
            successful = false;
            aiResponse = "I wasn't able to process your request right now.";
        }

        var aiMessage = new ChatMessage
        {
            ChatId = request.ChatId,
            UserId = null,
            SenderType = SenderTypeEnum.Ai,
            MessageType = aiMessageType,
            InputMode = InputModeEnum.Text,
            Content = aiResponse,
            ParsedIntent = intent is null ? null : BuildParsedIntentJson(intent),
            AiResponse = aiResponse,
            Successful = successful,
        };

        _context.ChatMessages.Add(aiMessage);
        userMessage.AiResponse = aiResponse;
        userMessage.Successful = successful;

        chat.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(request.UserId, "chat.message_sent", "chat_message", userMessage.Id,
            new { chatId = request.ChatId, intent = intent?.Intent }, cancellationToken);

        // Analyze user response if it's a reply to a monitoring follow-up.
        // This runs fire-and-forget so it never delays the chat response.
        _ = Task.Run(async () =>
        {
            try
            {
                var awaitingSession = await _followUpIntelligence
                    .FindSessionAwaitingResponseAsync(chat.FamilyId, CancellationToken.None);

                if (awaitingSession is not null)
                {
                    var analysis = await _followUpIntelligence.AnalyzeResponseAsync(
                        awaitingSession.Id, request.Content, CancellationToken.None);

                    await _healthMonitoring.UpdateResponseAnalysisAsync(
                        awaitingSession.Id, request.Content,
                        analysis.ResponseAnalysisJson ?? "{}", CancellationToken.None);

                    // Apply score modifier to session risk
                    if (analysis.ScoreModifier != 0)
                    {
                        var newScore = Math.Clamp(awaitingSession.RiskScore + analysis.ScoreModifier, 0, 100);
                        var newSeverity = newScore switch
                        {
                            <= 30  => MonitoringSeverity.Low,
                            <= 50  => MonitoringSeverity.Medium,
                            <= 70  => MonitoringSeverity.High,
                            <= 85  => MonitoringSeverity.Critical,
                            _      => MonitoringSeverity.Emergency,
                        };
                        await _healthMonitoring.UpdateSessionRiskAsync(
                            awaitingSession.Id,
                            new RiskAssessment(newScore, newSeverity, MonitoringEscalationLevel.None, [], analysis.NextIntervalMinutes),
                            request.Content, CancellationToken.None);
                    }

                    if (analysis.ShouldResolve)
                    {
                        await _healthMonitoring.ResolveSessionAsync(
                            awaitingSession.Id,
                            $"Состояние улучшилось: {analysis.AnalysisSummary}", CancellationToken.None);
                    }
                    else if (analysis.ShouldEscalate)
                    {
                        await _healthMonitoring.UpdateLifecyclePhaseAsync(
                            awaitingSession.Id, MonitoringLifecyclePhase.Critical, CancellationToken.None);
                    }
                    else
                    {
                        // Schedule next follow-up at the adaptive interval
                        var nextAt = DateTimeOffset.UtcNow.AddMinutes(analysis.NextIntervalMinutes);
                        await _healthMonitoring.MarkFollowUpSentAsync(
                            awaitingSession.Id, nextAt, CancellationToken.None);

                        await _healthMonitoring.UpdateLifecyclePhaseAsync(
                            awaitingSession.Id,
                            analysis.SymptomChange == SymptomChangeType.Improved
                                ? MonitoringLifecyclePhase.Stabilizing
                                : MonitoringLifecyclePhase.Monitoring,
                            CancellationToken.None);
                    }

                    // Emit realtime SSE so Flutter updates the monitoring view
                    var memberIds = await _context.FamilyMembers
                        .Where(m => m.FamilyId == chat.FamilyId && m.Status == FamilyMemberStatusEnum.Active)
                        .Select(m => m.UserId)
                        .ToListAsync(CancellationToken.None);

                    var ssePayload = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        familyId   = chat.FamilyId,
                        sessionId  = awaitingSession.Id,
                        change     = analysis.SymptomChange.ToString(),
                        scoreModifier = analysis.ScoreModifier,
                        shouldEscalate = analysis.ShouldEscalate,
                        shouldResolve  = analysis.ShouldResolve,
                    });

                    foreach (var uid in memberIds)
                        await _sse.PublishAsync(uid, new SseEvent("monitoring_session_updated", ssePayload), CancellationToken.None);

                    _logger.LogInformation(
                        "Follow-up response analyzed for session {SessionId}: change={Change}, modifier={Mod}",
                        awaitingSession.Id, analysis.SymptomChange, analysis.ScoreModifier);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Follow-up response analysis failed silently");
            }
        }, CancellationToken.None);

        // Start or update a health monitoring session when a symptom is detected.
        if (intent?.DetectedIssueType is { } issueTypeStr && successful)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (!Enum.TryParse<MonitoringIssueType>(issueTypeStr, ignoreCase: true, out var issueType))
                        return;

                    var proactiveCtx = await _proactiveContext.BuildContextAsync(chat.FamilyId, CancellationToken.None);
                    var child = proactiveCtx.Children
                        .FirstOrDefault(c => intent.ChildName != null &&
                            c.Name.Equals(intent.ChildName, StringComparison.OrdinalIgnoreCase));

                    var risk = _riskScoring.CalculateRisk(
                        issueType, child, proactiveCtx.Weather,
                        request.Content, 0, 0);

                    await _healthMonitoring.StartOrUpdateSessionAsync(
                        chat.FamilyId, child?.ChildId, issueType,
                        request.Content, risk);
                }
                catch { /* monitoring session startup must never fail the chat response */ }
            }, CancellationToken.None);
        }

        // Schedule autonomous follow-up when AI detects a health symptom.
        if (intent?.FollowUp is { } followUp && followUp.InMinutes > 0)
        {
            var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(followUp.InMinutes);
            var memberIds = await _context.FamilyMembers
                .Where(m => m.FamilyId == chat.FamilyId && m.Status == FamilyMemberStatusEnum.Active)
                .Select(m => m.UserId)
                .ToListAsync(cancellationToken);

            // Find any active monitoring session to include in metadata for dispatcher
            var activeSession = await _context.HealthMonitoringSessions
                .Where(s => s.FamilyId == chat.FamilyId && !s.IsResolved)
                .OrderByDescending(s => s.RiskScore)
                .Select(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                chatId     = request.ChatId,
                familyId   = chat.FamilyId,
                isFollowUp = true,
                sessionId  = activeSession > 0 ? (int?)activeSession : null,
            });

            var idempotencyBase = $"chat:{request.ChatId}:followup:{scheduledAt:yyyyMMddHHmm}";

            foreach (var memberId in memberIds)
            {
                try
                {
                    await _voiceService.CreateAsync(
                        chat.FamilyId, memberId,
                        VoiceActionType.FollowUpCheck,
                        followUp.Question,
                        NotificationPriority.Normal,
                        requiresConfirmation: false,
                        escalationEnabled: false,
                        scheduledAt: scheduledAt,
                        metadata: metadata,
                        idempotencyKey: $"{idempotencyBase}:{memberId}",
                        ct: cancellationToken);
                }
                catch { /* follow-up failure must not fail the chat response */ }
            }
        }

        return new ChatMessageResult(
            aiMessage.Id, request.ChatId, null, SenderTypeEnum.Ai, aiMessageType,
            aiResponse, aiResponse, aiMessage.ParsedIntent, successful, aiMessage.CreatedAt);
    }

    /// <summary>
    /// Canonical JSON the client reads to render proposal cards. We always
    /// rebuild it from the parsed intent — if the LLM returned a malformed or
    /// proposal-less payload, the rule-based fallback's structured fields still
    /// reach the UI.
    /// </summary>
    private static string BuildParsedIntentJson(ParsedIntent intent)
    {
        object? proposal = null;
        if (intent.Proposal is { } p)
        {
            // Inline the params object so the client sees a real nested object,
            // not a JSON-encoded string.
            using var paramsDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(p.ParametersJson) ? "{}" : p.ParametersJson);
            proposal = new
            {
                type = p.Type,
                summary = p.Summary,
                @params = JsonSerializer.Deserialize<JsonElement>(paramsDoc.RootElement.GetRawText()),
            };
        }

        return JsonSerializer.Serialize(new
        {
            intent = intent.Intent,
            childName = intent.ChildName,
            topic = intent.Topic,
            proposal,
        });
    }
}

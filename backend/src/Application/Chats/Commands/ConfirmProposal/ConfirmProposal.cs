using System.Globalization;
using System.Text.Json;
using Backend.Application.Common.Exceptions;
using Backend.Application.Calendar.Commands.CreateCalendarEvent;
using Microsoft.Extensions.Logging;
using Backend.Application.Calendar.Models;
using Backend.Application.Chats.Models;
using Backend.Application.Children.Commands.AddChildNote;
using Backend.Application.Children.Commands.AddChildRoutine;
using Backend.Application.Children.Commands.CreateChild;
using Backend.Application.Children.Commands.UpdateSensitivity;
using Backend.Application.Children.Models;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Chats.Commands.ConfirmProposal;

public record ConfirmProposalCommand(
    int ChatId,
    int MessageId,
    string UserId,
    string? Edits) : IRequest<ChatMessageResult>;

public class ConfirmProposalCommandHandler : IRequestHandler<ConfirmProposalCommand, ChatMessageResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;
    private readonly ISender _mediator;
    private readonly IAuditService _audit;
    private readonly IVoiceActionService _voiceService;
    private readonly ISseConnectionManager _sse;
    private readonly ILogger<ConfirmProposalCommandHandler> _logger;

    public ConfirmProposalCommandHandler(
        IApplicationDbContext context,
        IFamilyAuthorization authorization,
        ISender mediator,
        IAuditService audit,
        IVoiceActionService voiceService,
        ISseConnectionManager sse,
        ILogger<ConfirmProposalCommandHandler> logger)
    {
        _context = context;
        _authorization = authorization;
        _mediator = mediator;
        _audit = audit;
        _voiceService = voiceService;
        _sse = sse;
        _logger = logger;
    }

    public async Task<ChatMessageResult> Handle(ConfirmProposalCommand request, CancellationToken ct)
    {
        var chat = await _context.Chats.FindAsync([request.ChatId], ct)
            ?? throw new NotFoundException(nameof(Chat), request.ChatId);
        await _authorization.AssertMemberAsync(chat.FamilyId, request.UserId, ct);

        var message = await _context.ChatMessages.FindAsync([request.MessageId], ct)
            ?? throw new NotFoundException(nameof(ChatMessage), request.MessageId);

        if (message.ChatId != chat.Id)
            throw new ForbiddenAccessException();
        if (message.MessageType != MessageTypeEnum.Proposal)
            throw new ValidationException([new("messageType", "Message is not a proposal.")]);
        if (string.IsNullOrWhiteSpace(message.ParsedIntent))
            throw new ValidationException([new("parsedIntent", "Proposal payload is missing.")]);

        var (proposalType, paramsJson) = ExtractProposal(message.ParsedIntent, request.Edits);
        if (proposalType is null)
            throw new ValidationException([new("proposal", "Could not read proposal from message.")]);

        var (resultText, sseEventType, ssePayload) = await ExecuteProposal(chat.FamilyId, request.UserId, proposalType, paramsJson, ct);

        var aiReply = new ChatMessage
        {
            ChatId = chat.Id,
            UserId = null,
            SenderType = SenderTypeEnum.Ai,
            MessageType = MessageTypeEnum.Confirmation,
            InputMode = InputModeEnum.Text,
            Content = resultText,
            AiResponse = resultText,
            Successful = true,
        };
        _context.ChatMessages.Add(aiReply);

        chat.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(ct);

        if (sseEventType is not null && ssePayload is not null)
        {
            var memberIds = await _context.FamilyMembers
                .Where(m => m.FamilyId == chat.FamilyId && m.Status == FamilyMemberStatusEnum.Active)
                .Select(m => m.UserId)
                .ToListAsync(ct);

            _logger.LogInformation(
                "[ConfirmProposal] Publishing SSE event={EventType} to {Count} members (family={FamilyId})",
                sseEventType, memberIds.Count, chat.FamilyId);

            var sseEvent = new SseEvent(sseEventType, ssePayload);
            var publishedCount = 0;
            foreach (var uid in memberIds)
            {
                try
                {
                    await _sse.PublishAsync(uid, sseEvent, ct);
                    publishedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[ConfirmProposal] SSE publish failed for user={UserId}", uid);
                }
            }

            _logger.LogInformation(
                "[ConfirmProposal] SSE event={EventType} delivered to {Published}/{Total} members",
                sseEventType, publishedCount, memberIds.Count);
        }
        else
        {
            _logger.LogDebug(
                "[ConfirmProposal] No SSE event for proposal type — sseEventType={EventType}",
                sseEventType ?? "(null)");
        }

        await _audit.LogAsync(request.UserId, "chat.proposal_confirmed", "chat_message",
            request.MessageId, new { proposalType }, ct);

        return new ChatMessageResult(
            aiReply.Id, chat.Id, null, SenderTypeEnum.Ai, aiReply.MessageType,
            resultText, resultText, null, true, aiReply.CreatedAt);
    }

    private static (string? type, string paramsJson) ExtractProposal(string parsedIntent, string? edits)
    {
        try
        {
            using var doc = JsonDocument.Parse(parsedIntent);
            if (!doc.RootElement.TryGetProperty("proposal", out var proposal)) return (null, "{}");
            var type = proposal.TryGetProperty("type", out var t) ? t.GetString() : null;
            var paramsJson = proposal.TryGetProperty("params", out var p) ? p.GetRawText() : "{}";
            if (!string.IsNullOrWhiteSpace(edits))
            {
                paramsJson = edits;
            }
            return (type, paramsJson);
        }
        catch
        {
            return (null, "{}");
        }
    }

    private async Task<(string resultText, string? sseEventType, string? ssePayload)> ExecuteProposal(
        int familyId, string userId, string type, string paramsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(paramsJson);
        var p = doc.RootElement;

        switch (type)
        {
            case "set_reminder":
            {
                var text = GetString(p, "text") ?? throw new ValidationException([new("text", "Required.")]);
                var scheduledAt = GetDate(p, "scheduledAt") ?? throw new ValidationException([new("scheduledAt", "Required.")]);

                if (scheduledAt <= DateTimeOffset.UtcNow)
                    throw new ValidationException([new("scheduledAt", "Reminder time must be in the future.")]);

                var memberIds = await _context.FamilyMembers
                    .Where(m => m.FamilyId == familyId && m.Status == FamilyMemberStatusEnum.Active)
                    .Select(m => m.UserId)
                    .ToListAsync(ct);

                foreach (var memberId in memberIds)
                {
                    await _voiceService.CreateAsync(
                        familyId, memberId,
                        VoiceActionType.Custom,
                        text,
                        NotificationPriority.High,
                        requiresConfirmation: false,
                        escalationEnabled: false,
                        scheduledAt: scheduledAt,
                        metadata: null,
                        idempotencyKey: null,
                        ct);
                }

                var localTime = scheduledAt.ToLocalTime();
                var minutesFromNow = (int)(scheduledAt - DateTimeOffset.UtcNow).TotalMinutes;
                var timeLabel = minutesFromNow < 60
                    ? $"через {minutesFromNow} мин. ({localTime:HH:mm})"
                    : $"в {localTime:HH:mm}";
                return ($"Напомню {timeLabel}: {text}", null, null);
            }
            case "add_child":
            {
                var name = GetString(p, "displayName") ?? throw new ValidationException([new("displayName", "Required.")]);
                var age = GetInt(p, "ageYears") ?? 0;
                var result = await _mediator.Send(new CreateChildCommand(familyId, name, age, userId), ct);
                var payload = JsonSerializer.Serialize(new { familyId, childId = result.Id, childName = result.DisplayName });
                return ($"Added {result.DisplayName} ({result.AgeYears}y) to your family.", "child_created", payload);
            }
            case "add_event":
            {
                var title = GetString(p, "title") ?? throw new ValidationException([new("title", "Required.")]);
                var start = GetDate(p, "startDatetime") ?? throw new ValidationException([new("startDatetime", "Required.")]);
                var end = GetDate(p, "endDatetime") ?? start.AddHours(1);
                var childId = await ResolveChildIdAsync(familyId, GetString(p, "childName"), ct);
                var dto = new CalendarEventDto(
                    childId,
                    title,
                    GetString(p, "description"),
                    start,
                    end,
                    GetString(p, "locationLabel"),
                    GetString(p, "locationKey"),
                    null, null,
                    "Indoor",
                    "Low",
                    GetBool(p, "weatherSensitive") ?? false);
                var result = await _mediator.Send(new CreateCalendarEventCommand(familyId, dto, userId), ct);
                var payload = JsonSerializer.Serialize(new { familyId, eventId = result.Id, title = result.Title });
                return ($"Added \"{result.Title}\" to your calendar for {result.StartDatetime.LocalDateTime:dddd HH:mm}.", "event_created", payload);
            }
            case "add_routine":
            {
                var childId = await ResolveChildIdAsync(familyId, GetString(p, "childName"), ct)
                    ?? throw new ValidationException([new("childName", "Unknown child.")]);
                var dto = new ChildRoutineDto(
                    GetString(p, "title") ?? "Routine",
                    GetString(p, "routineType") ?? "Custom",
                    ParseTime(GetString(p, "startTime")) ?? new TimeOnly(8, 0),
                    ParseTime(GetString(p, "endTime")),
                    GetString(p, "repeatPattern") ?? "Daily",
                    GetString(p, "locationType") ?? "Indoor",
                    GetString(p, "activityIntensity") ?? "Low",
                    GetBool(p, "weatherSensitive") ?? false);
                var result = await _mediator.Send(new AddChildRoutineCommand(childId, dto, userId), ct);
                return ($"Added routine \"{result.Title}\" starting at {result.StartTime:HH\\:mm}.", null, null);
            }
            case "add_note":
            {
                var childId = await ResolveChildIdAsync(familyId, GetString(p, "childName"), ct)
                    ?? throw new ValidationException([new("childName", "Unknown child.")]);
                var note = GetString(p, "note") ?? throw new ValidationException([new("note", "Required.")]);
                var dto = new ChildNoteDto(
                    GetString(p, "noteType") ?? "Observation",
                    note,
                    "Ai",
                    NeedsConfirmation: false);
                var result = await _mediator.Send(new AddChildNoteCommand(childId, dto, userId), ct);
                return ($"Saved note ({result.NoteType.ToLower(CultureInfo.InvariantCulture)}) to the child profile.", null, null);
            }
            case "update_sensitivity":
            {
                var childId = await ResolveChildIdAsync(familyId, GetString(p, "childName"), ct)
                    ?? throw new ValidationException([new("childName", "Unknown child.")]);
                if (!p.TryGetProperty("sensitivities", out var s))
                    throw new ValidationException([new("sensitivities", "Required.")]);
                var dto = new ChildSensitivityDto(
                    GetBool(s, "heatSensitive") ?? false,
                    GetBool(s, "coldSensitive") ?? false,
                    GetBool(s, "airQualitySensitive") ?? false,
                    GetBool(s, "pollenSensitive") ?? false,
                    GetBool(s, "uvSensitive") ?? false,
                    GetBool(s, "skinSensitive") ?? false,
                    GetBool(s, "activitySensitive") ?? false,
                    GetBool(s, "respiratorySensitive") ?? false);
                await _mediator.Send(new UpdateSensitivityCommand(childId, dto, userId), ct);
                return ("Updated sensitivities for the child.", null, null);
            }
            default:
                throw new ValidationException([new("proposal.type", $"Unknown proposal type '{type}'.")]);
        }
    }

    private async Task<int?> ResolveChildIdAsync(int familyId, string? childName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(childName)) return null;
        var match = await _context.Children
            .Where(c => c.FamilyId == familyId
                     && c.DisplayName.ToLower() == childName.ToLower())
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);
        return match;
    }

    private static string? GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? GetInt(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var s)) return s;
        return null;
    }

    private static bool? GetBool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
            ? v.GetBoolean()
            : null;

    private static DateTimeOffset? GetDate(JsonElement e, string name)
    {
        var s = GetString(e, name);
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt)
            ? dt
            : null;
    }

    private static TimeOnly? ParseTime(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return TimeOnly.TryParse(s, CultureInfo.InvariantCulture, out var t) ? t : null;
    }
}

using Backend.Application.Children.Models;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Children.Commands.ExtractFromChatMessage;

public record ExtractFromChatMessageCommand(
    int ChildId,
    int ChatMessageId,
    string RequestingUserId) : IRequest<ChildExtractionAppliedResult>;

public class ExtractFromChatMessageCommandHandler
    : IRequestHandler<ExtractFromChatMessageCommand, ChildExtractionAppliedResult>
{
    private const double AutoApplyThreshold = 0.85;

    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;
    private readonly IChildProfileExtractor _extractor;
    private readonly IAuditService _audit;

    public ExtractFromChatMessageCommandHandler(
        IApplicationDbContext context,
        IFamilyAuthorization authorization,
        IChildProfileExtractor extractor,
        IAuditService audit)
    {
        _context = context;
        _authorization = authorization;
        _extractor = extractor;
        _audit = audit;
    }

    public async Task<ChildExtractionAppliedResult> Handle(
        ExtractFromChatMessageCommand request,
        CancellationToken cancellationToken)
    {
        var child = await _context.Children.FindAsync([request.ChildId], cancellationToken)
            ?? throw new NotFoundException(nameof(Child), request.ChildId);

        await _authorization.AssertMemberAsync(child.FamilyId, request.RequestingUserId, cancellationToken);

        var message = await _context.ChatMessages.FindAsync([request.ChatMessageId], cancellationToken)
            ?? throw new NotFoundException(nameof(ChatMessage), request.ChatMessageId);

        var chat = await _context.Chats.FindAsync([message.ChatId], cancellationToken)
            ?? throw new NotFoundException(nameof(Chat), message.ChatId);

        if (chat.FamilyId != child.FamilyId)
            throw new ValidationException([new("chatMessageId",
                $"Chat message {request.ChatMessageId} does not belong to child's family.")]);

        var extraction = await _extractor.ExtractAsync(message.Content, request.RequestingUserId, cancellationToken);

        var applied = new List<AppliedInsight>();
        var pending = new List<PendingInsight>();

        ChildSensitivity? sensitivity = null;

        foreach (var insight in extraction.Insights)
        {
            var willAutoApply = insight.Confidence >= AutoApplyThreshold;

            var note = new ChildNote
            {
                ChildId = child.Id,
                CreatedByUserId = request.RequestingUserId,
                NoteType = insight.NoteType,
                Note = insight.Description,
                Source = insight.Source,
                ConfidenceScore = insight.Confidence,
                NeedsConfirmation = !willAutoApply,
                ConfirmedAt = willAutoApply ? DateTimeOffset.UtcNow : null,
            };

            _context.ChildNotes.Add(note);

            int? createdRoutineId = null;
            string? sensitivityFieldChanged = null;

            if (willAutoApply)
            {
                if (insight.Sensitivity != null)
                {
                    sensitivity ??= await LoadOrCreateSensitivityAsync(child.Id, cancellationToken);
                    if (TryApplySensitivity(sensitivity, insight.Sensitivity))
                        sensitivityFieldChanged = insight.Sensitivity.Field;
                }

                if (insight.Routine != null)
                {
                    var routine = new ChildRoutine
                    {
                        ChildId = child.Id,
                        Title = insight.Routine.Title,
                        RoutineType = insight.Routine.RoutineType,
                        StartTime = insight.Routine.StartTime,
                        EndTime = insight.Routine.EndTime,
                        LocationType = insight.Routine.LocationType,
                        ActivityIntensity = insight.Routine.ActivityIntensity,
                    };
                    _context.ChildRoutines.Add(routine);
                    await _context.SaveChangesAsync(cancellationToken); // need routine.Id below
                    createdRoutineId = routine.Id;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (willAutoApply)
            {
                applied.Add(new AppliedInsight(
                    note.Id, insight.Description, insight.Confidence,
                    sensitivityFieldChanged, createdRoutineId));
            }
            else
            {
                pending.Add(new PendingInsight(
                    note.Id, insight.Description, insight.Confidence,
                    insight.Sensitivity?.Field,
                    insight.Routine?.Title));
            }
        }

        await _audit.LogAsync(request.RequestingUserId, "ai.extraction_run", "child", child.Id,
            new
            {
                chatMessageId = request.ChatMessageId,
                totalInsights = extraction.Insights.Count,
                autoApplied = applied.Count,
                pending = pending.Count
            }, cancellationToken);

        return new ChildExtractionAppliedResult(
            child.Id, extraction.Insights.Count, applied.Count, pending.Count, applied, pending);
    }

    private async Task<ChildSensitivity> LoadOrCreateSensitivityAsync(int childId, CancellationToken ct)
    {
        var existing = await _context.ChildSensitivities
            .FirstOrDefaultAsync(s => s.ChildId == childId, ct);

        if (existing != null)
            return existing;

        var fresh = new ChildSensitivity { ChildId = childId };
        _context.ChildSensitivities.Add(fresh);
        return fresh;
    }

    private static bool TryApplySensitivity(ChildSensitivity sensitivity, SensitivityChange change)
    {
        switch (change.Field)
        {
            case nameof(ChildSensitivity.HeatSensitive):
                if (sensitivity.HeatSensitive == change.Value) return false;
                sensitivity.HeatSensitive = change.Value; break;
            case nameof(ChildSensitivity.ColdSensitive):
                if (sensitivity.ColdSensitive == change.Value) return false;
                sensitivity.ColdSensitive = change.Value; break;
            case nameof(ChildSensitivity.AirQualitySensitive):
                if (sensitivity.AirQualitySensitive == change.Value) return false;
                sensitivity.AirQualitySensitive = change.Value; break;
            case nameof(ChildSensitivity.PollenSensitive):
                if (sensitivity.PollenSensitive == change.Value) return false;
                sensitivity.PollenSensitive = change.Value; break;
            case nameof(ChildSensitivity.UvSensitive):
                if (sensitivity.UvSensitive == change.Value) return false;
                sensitivity.UvSensitive = change.Value; break;
            case nameof(ChildSensitivity.SkinSensitive):
                if (sensitivity.SkinSensitive == change.Value) return false;
                sensitivity.SkinSensitive = change.Value; break;
            case nameof(ChildSensitivity.ActivitySensitive):
                if (sensitivity.ActivitySensitive == change.Value) return false;
                sensitivity.ActivitySensitive = change.Value; break;
            case nameof(ChildSensitivity.RespiratorySensitive):
                if (sensitivity.RespiratorySensitive == change.Value) return false;
                sensitivity.RespiratorySensitive = change.Value; break;
            default:
                return false;
        }

        sensitivity.UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }
}

using Backend.Application.Children.Models;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Children.Commands.AddChildNote;

public record AddChildNoteCommand(
    int ChildId,
    ChildNoteDto Note,
    string RequestingUserId) : IRequest<ChildNoteResult>;

public class AddChildNoteCommandHandler : IRequestHandler<AddChildNoteCommand, ChildNoteResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;
    private readonly IAuditService _audit;

    public AddChildNoteCommandHandler(
        IApplicationDbContext context,
        IFamilyAuthorization authorization,
        IAuditService audit)
    {
        _context = context;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task<ChildNoteResult> Handle(AddChildNoteCommand request, CancellationToken cancellationToken)
    {
        var child = await _context.Children.FindAsync([request.ChildId], cancellationToken)
            ?? throw new NotFoundException(nameof(Child), request.ChildId);

        await _authorization.AssertMemberAsync(child.FamilyId, request.RequestingUserId, cancellationToken);

        var dto = request.Note;

        if (!Enum.TryParse<NoteTypeEnum>(dto.NoteType, ignoreCase: true, out var noteType))
            throw new ValidationException([new("noteType", $"Invalid note type '{dto.NoteType}'.")]);

        if (!Enum.TryParse<NoteSourceEnum>(dto.Source, ignoreCase: true, out var source))
            throw new ValidationException([new("source", $"Invalid source '{dto.Source}'.")]);

        var note = new ChildNote
        {
            ChildId = request.ChildId,
            CreatedByUserId = request.RequestingUserId,
            NoteType = noteType,
            Note = dto.Note,
            Tags = dto.Tags,
            Source = source,
            ConfidenceScore = dto.ConfidenceScore,
            NeedsConfirmation = dto.NeedsConfirmation,
            ObservedAt = dto.ObservedAt,
        };

        _context.ChildNotes.Add(note);
        await _context.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(request.RequestingUserId, "child.note_added", "child_note", note.Id,
            new { noteType = dto.NoteType }, cancellationToken);

        return new ChildNoteResult(
            note.Id, note.ChildId, note.NoteType.ToString(),
            note.Note, note.Source.ToString(), note.CreatedAt);
    }
}

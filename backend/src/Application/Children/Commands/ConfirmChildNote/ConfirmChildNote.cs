using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Domain.Entities;

namespace Backend.Application.Children.Commands.ConfirmChildNote;

public record ConfirmChildNoteCommand(int NoteId, string RequestingUserId) : IRequest;

public class ConfirmChildNoteCommandHandler : IRequestHandler<ConfirmChildNoteCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;
    private readonly IAuditService _audit;

    public ConfirmChildNoteCommandHandler(
        IApplicationDbContext context,
        IFamilyAuthorization authorization,
        IAuditService audit)
    {
        _context = context;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task Handle(ConfirmChildNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _context.ChildNotes.FindAsync([request.NoteId], cancellationToken)
            ?? throw new NotFoundException(nameof(ChildNote), request.NoteId);

        var child = await _context.Children.FindAsync([note.ChildId], cancellationToken)
            ?? throw new NotFoundException(nameof(Child), note.ChildId);

        await _authorization.AssertMemberAsync(child.FamilyId, request.RequestingUserId, cancellationToken);

        if (note.ConfirmedAt.HasValue)
            return; // Already confirmed — idempotent.

        note.ConfirmedAt = DateTimeOffset.UtcNow;
        note.NeedsConfirmation = false;
        await _context.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(request.RequestingUserId, "child.note_confirmed", "child_note",
            note.Id, new { childId = note.ChildId }, cancellationToken);
    }
}

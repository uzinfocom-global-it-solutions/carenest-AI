using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Families.Commands.AcceptInvitation;

public record AcceptInvitationCommand(
    int FamilyId,
    string UserId) : IRequest;

public class AcceptInvitationCommandHandler : IRequestHandler<AcceptInvitationCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _audit;

    public AcceptInvitationCommandHandler(IApplicationDbContext context, IAuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        var member = await _context.FamilyMembers
            .FirstOrDefaultAsync(
                m => m.FamilyId == request.FamilyId && m.UserId == request.UserId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(FamilyMember), $"family={request.FamilyId} user={request.UserId}");

        if (member.Status != FamilyMemberStatusEnum.Invited)
            throw new ConflictException($"Membership status is '{member.Status}', not Invited.");

        member.Status = FamilyMemberStatusEnum.Active;
        await _context.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(request.UserId, "family.invitation_accepted", "family_member",
            member.Id, new { request.FamilyId }, cancellationToken);
    }
}

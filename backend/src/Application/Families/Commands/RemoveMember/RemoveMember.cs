using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Families.Commands.RemoveMember;

public record RemoveMemberCommand(
    int FamilyId,
    string TargetUserId,
    string RequestingUserId) : IRequest;

public class RemoveMemberCommandHandler : IRequestHandler<RemoveMemberCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;
    private readonly IAuditService _audit;

    public RemoveMemberCommandHandler(
        IApplicationDbContext context,
        IFamilyAuthorization authorization,
        IAuditService audit)
    {
        _context = context;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task Handle(RemoveMemberCommand request, CancellationToken cancellationToken)
    {
        await _authorization.AssertParentAsync(request.FamilyId, request.RequestingUserId, cancellationToken);

        var member = await _context.FamilyMembers
            .FirstOrDefaultAsync(
                m => m.FamilyId == request.FamilyId && m.UserId == request.TargetUserId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(FamilyMember), $"family={request.FamilyId} user={request.TargetUserId}");

        if (member.Status == FamilyMemberStatusEnum.Disabled)
            throw new ConflictException("Membership is already disabled.");

        if (member.Role == RoleEnum.Parent)
        {
            var otherActiveParents = await _context.FamilyMembers
                .CountAsync(m => m.FamilyId == request.FamilyId
                              && m.UserId != request.TargetUserId
                              && m.Role == RoleEnum.Parent
                              && m.Status == FamilyMemberStatusEnum.Active,
                cancellationToken);

            if (otherActiveParents == 0)
                throw new ConflictException("Cannot remove the last active parent of the family.");
        }

        // Soft-disable: keep the row for audit/history; FamilyAuthorization filters on Status==Active.
        member.Status = FamilyMemberStatusEnum.Disabled;
        await _context.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(request.RequestingUserId, "family.member_removed", "family_member",
            member.Id, new { request.FamilyId, request.TargetUserId }, cancellationToken);
    }
}

using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Families.Commands.UpdateMemberRole;

public record UpdateMemberRoleCommand(
    int FamilyId,
    string TargetUserId,
    string Role,
    string RequestingUserId) : IRequest;

public class UpdateMemberRoleCommandHandler : IRequestHandler<UpdateMemberRoleCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;
    private readonly IAuditService _audit;

    public UpdateMemberRoleCommandHandler(
        IApplicationDbContext context,
        IFamilyAuthorization authorization,
        IAuditService audit)
    {
        _context = context;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task Handle(UpdateMemberRoleCommand request, CancellationToken cancellationToken)
    {
        await _authorization.AssertParentAsync(request.FamilyId, request.RequestingUserId, cancellationToken);

        if (!Enum.TryParse<RoleEnum>(request.Role, ignoreCase: true, out var newRole))
            throw new ValidationException([new("role", $"Invalid role '{request.Role}'.")]);

        var member = await _context.FamilyMembers
            .FirstOrDefaultAsync(
                m => m.FamilyId == request.FamilyId && m.UserId == request.TargetUserId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(FamilyMember), $"family={request.FamilyId} user={request.TargetUserId}");

        var previousRole = member.Role;

        if (previousRole == RoleEnum.Parent && newRole != RoleEnum.Parent)
        {
            // Block demoting the last active parent — there must always be at least one.
            var otherActiveParents = await _context.FamilyMembers
                .CountAsync(m => m.FamilyId == request.FamilyId
                              && m.UserId != request.TargetUserId
                              && m.Role == RoleEnum.Parent
                              && m.Status == FamilyMemberStatusEnum.Active,
                cancellationToken);

            if (otherActiveParents == 0)
                throw new ConflictException("Cannot demote the last active parent of the family.");
        }

        member.Role = newRole;
        await _context.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(request.RequestingUserId, "family.role_changed", "family_member",
            member.Id, new
            {
                request.FamilyId,
                request.TargetUserId,
                previousRole = previousRole.ToString(),
                newRole = newRole.ToString(),
            }, cancellationToken);
    }
}

using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Families.Commands.AddFamilyMember;

public record AddFamilyMemberCommand(
    int FamilyId,
    string UserId,
    string Role,
    string RequestingUserId) : IRequest;

public class AddFamilyMemberCommandHandler : IRequestHandler<AddFamilyMemberCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IFamilyAuthorization _authorization;
    private readonly IAuditService _audit;

    public AddFamilyMemberCommandHandler(
        IApplicationDbContext context,
        IFamilyAuthorization authorization,
        IAuditService audit)
    {
        _context = context;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task Handle(AddFamilyMemberCommand request, CancellationToken cancellationToken)
    {
        await _authorization.AssertParentAsync(request.FamilyId, request.RequestingUserId, cancellationToken);

        if (!Enum.TryParse<RoleEnum>(request.Role, ignoreCase: true, out var parsedRole))
            throw new ValidationException([new("role", $"Invalid role '{request.Role}'.")]);

        var existing = await _context.FamilyMembers
            .AnyAsync(m => m.FamilyId == request.FamilyId && m.UserId == request.UserId, cancellationToken);

        if (existing)
            throw new ConflictException($"User '{request.UserId}' is already a member of this family.");

        _context.FamilyMembers.Add(new FamilyMember
        {
            FamilyId = request.FamilyId,
            UserId = request.UserId,
            Role = parsedRole,
            Status = FamilyMemberStatusEnum.Invited,
        });

        await _context.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(request.RequestingUserId, "family.member_added", "family_member",
            request.FamilyId, new { request.UserId, request.Role }, cancellationToken);
    }
}

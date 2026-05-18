using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;

namespace Backend.Application.Common.Security;

public interface IFamilyAuthorization
{
    Task<bool> IsMemberAsync(int familyId, string userId, CancellationToken ct = default);
    Task AssertMemberAsync(int familyId, string userId, CancellationToken ct = default);
    Task AssertParentAsync(int familyId, string userId, CancellationToken ct = default);
}

internal sealed class FamilyAuthorization : IFamilyAuthorization
{
    private readonly IApplicationDbContext _context;

    public FamilyAuthorization(IApplicationDbContext context) => _context = context;

    public Task<bool> IsMemberAsync(int familyId, string userId, CancellationToken ct = default) =>
        _context.FamilyMembers.AnyAsync(
            m => m.FamilyId == familyId
              && m.UserId == userId
              && m.Status == FamilyMemberStatusEnum.Active,
            ct);

    public async Task AssertMemberAsync(int familyId, string userId, CancellationToken ct = default)
    {
        if (!await IsMemberAsync(familyId, userId, ct))
            throw new ForbiddenAccessException();
    }

    public async Task AssertParentAsync(int familyId, string userId, CancellationToken ct = default)
    {
        var isParent = await _context.FamilyMembers.AnyAsync(
            m => m.FamilyId == familyId
              && m.UserId == userId
              && m.Status == FamilyMemberStatusEnum.Active
              && m.Role == RoleEnum.Parent,
            ct);

        if (!isParent)
            throw new ForbiddenAccessException();
    }
}

using Backend.Domain.Common;
using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class FamilyMember : BaseEntity
{
    public required int FamilyId { get; set; }
    public required string UserId { get; set; }
    public RoleEnum Role { get; set; } = RoleEnum.Parent;
    public FamilyMemberStatusEnum Status { get; set; } = FamilyMemberStatusEnum.Active;
    public DateTimeOffset? AccessExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Family Family { get; set; } = null!;
}

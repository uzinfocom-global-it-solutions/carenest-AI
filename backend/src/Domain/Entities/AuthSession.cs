using Backend.Domain.Common;

namespace Backend.Domain.Entities;

public class AuthSession : BaseEntity
{
    public required string UserId { get; set; }
    public string? AccessTokenHash { get; set; }
    public required string RefreshTokenHash { get; set; }
    public string? DeviceId { get; set; }
    public required DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsActive => RevokedAt == null && DateTimeOffset.UtcNow < ExpiresAt;
}

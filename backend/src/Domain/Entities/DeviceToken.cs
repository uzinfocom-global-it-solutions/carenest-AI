using Backend.Domain.Common;

namespace Backend.Domain.Entities;

public class DeviceToken : BaseEntity
{
    public required string UserId { get; set; }
    public required string Token { get; set; }
    public required string Platform { get; set; } // android, ios, web
    public string? DeviceName { get; set; }
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;
}

using Backend.Domain.Common;

namespace Backend.Domain.Entities;

public class AuditLog : BaseEntity
{
    public required string UserId { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public int? EntityId { get; set; }
    public string? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

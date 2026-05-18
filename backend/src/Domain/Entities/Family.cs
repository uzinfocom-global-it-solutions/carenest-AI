using Backend.Domain.Common;

namespace Backend.Domain.Entities;

public class Family : BaseEntity
{
    public required string Name { get; set; }
    public string? DefaultLocationKey { get; set; }
    public double? DefaultLatitude { get; set; }
    public double? DefaultLongitude { get; set; }
    public required string CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<FamilyMember> Members { get; set; } = [];
    public ICollection<Child> Children { get; set; } = [];
    public ICollection<Chat> Chats { get; set; } = [];
    public ICollection<CalendarEvent> Events { get; set; } = [];
}

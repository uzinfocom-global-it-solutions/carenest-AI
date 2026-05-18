using Backend.Domain.Common;
using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class ChildNote : BaseEntity
{
    public required int ChildId { get; set; }
    public required string CreatedByUserId { get; set; }
    public required NoteTypeEnum NoteType { get; set; }
    public required string Note { get; set; }
    public string? Tags { get; set; }
    public required NoteSourceEnum Source { get; set; }
    public double? ConfidenceScore { get; set; }
    public bool NeedsConfirmation { get; set; } = false;
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? ObservedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Child Child { get; set; } = null!;
}

using Backend.Domain.Common;

namespace Backend.Domain.Entities;

public class ChildSensitivity : BaseEntity
{
    public required int ChildId { get; set; }
    public bool HeatSensitive { get; set; } = false;
    public bool ColdSensitive { get; set; } = false;
    public bool AirQualitySensitive { get; set; } = false;
    public bool PollenSensitive { get; set; } = false;
    public bool UvSensitive { get; set; } = false;
    public bool SkinSensitive { get; set; } = false;
    public bool ActivitySensitive { get; set; } = false;
    public bool RespiratorySensitive { get; set; } = false;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Child Child { get; set; } = null!;
}

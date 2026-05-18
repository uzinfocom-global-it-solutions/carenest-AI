namespace Backend.Infrastructure.Firebase;

public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public string? ServiceAccountPath { get; set; }
    public string? ServiceAccountJson { get; set; }
    public string? ProjectId { get; set; }
}

namespace Backend.Application.Families.Models;

public record FamilyResult(
    int Id,
    string Name,
    string CreatedByUserId,
    string? DefaultLocationKey,
    double? DefaultLatitude,
    double? DefaultLongitude,
    DateTimeOffset CreatedAt
);

public record FamilyMemberResult(
    int Id,
    int FamilyId,
    string UserId,
    string Role,
    string Status,
    DateTimeOffset? AccessExpiresAt,
    DateTimeOffset CreatedAt
);

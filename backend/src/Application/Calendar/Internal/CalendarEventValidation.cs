using Backend.Application.Calendar.Models;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;

namespace Backend.Application.Calendar.Internal;

internal static class CalendarEventValidation
{
    public static async Task<(LocationTypeEnum LocationType, ActivityIntensityEnum Intensity)> ValidateAsync(
        IApplicationDbContext context,
        CalendarEventDto dto,
        int familyId,
        CancellationToken cancellationToken)
    {
        if (dto.EndDatetime <= dto.StartDatetime)
            throw new ValidationException([new("endDatetime", "End time must be after start time.")]);

        if (!Enum.TryParse<LocationTypeEnum>(dto.LocationType, ignoreCase: true, out var locationType))
            throw new ValidationException([new("locationType", $"Invalid location type '{dto.LocationType}'.")]);

        if (!Enum.TryParse<ActivityIntensityEnum>(dto.ActivityIntensity, ignoreCase: true, out var intensity))
            throw new ValidationException([new("activityIntensity", $"Invalid activity intensity '{dto.ActivityIntensity}'.")]);

        if (dto.ChildId.HasValue)
        {
            var childInFamily = await context.Children
                .AnyAsync(c => c.Id == dto.ChildId.Value && c.FamilyId == familyId, cancellationToken);

            if (!childInFamily)
                throw new ValidationException([new("childId",
                    $"Child {dto.ChildId} does not belong to family {familyId}.")]);
        }

        return (locationType, intensity);
    }
}

using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Recommendations.Internal;

internal static class RecommendationRuleEngine
{
    public static IEnumerable<Recommendation> EvaluateWeatherRules(Child child, ChildSensitivity s, WeatherSnapshot w)
    {
        var now = DateTimeOffset.UtcNow;
        var expiry = now.AddHours(6);

        if (w.UvIndex >= 6 && s.UvSensitive)
        {
            yield return Build(child, w.Id, RecommendationTypeEnum.WeatherAlert, "high",
                "High UV Alert",
                $"UV index is {w.UvIndex:F1}. Apply sunscreen and limit direct sun exposure for {child.DisplayName}.",
                "uv_sensitive + uv_index >= 6", expiry);
        }

        if (w.TemperatureC <= 5 && s.ColdSensitive)
        {
            yield return Build(child, w.Id, RecommendationTypeEnum.Clothing, "high",
                "Dress Warmly",
                $"Temperature is {w.TemperatureC:F0}°C. Dress {child.DisplayName} in warm layers.",
                "cold_sensitive + temperature <= 5°C", expiry);
        }

        if (w.TemperatureC >= 30 && s.HeatSensitive)
        {
            yield return Build(child, w.Id, RecommendationTypeEnum.Hydration, "high",
                "Stay Hydrated",
                $"It's {w.TemperatureC:F0}°C outside. Make sure {child.DisplayName} drinks plenty of water.",
                "heat_sensitive + temperature >= 30°C", expiry);
        }

        if (w.WeatherCondition == WeatherConditionEnum.Rainy)
        {
            yield return Build(child, w.Id, RecommendationTypeEnum.CarryItem, "normal",
                "Rain Expected",
                $"Rain is expected. Pack a raincoat or umbrella for {child.DisplayName}.",
                "weather_condition = rainy", expiry);
        }

        if (w.WeatherCondition == WeatherConditionEnum.Snowy)
        {
            yield return Build(child, w.Id, RecommendationTypeEnum.Clothing, "normal",
                "Snow Expected",
                $"Snow is expected. Wear winter boots and warm outerwear for {child.DisplayName}.",
                "weather_condition = snowy", expiry);
        }

        if (w.WindKmh >= 30 || w.WeatherCondition == WeatherConditionEnum.Storm)
        {
            yield return Build(child, w.Id, RecommendationTypeEnum.WeatherAlert, "high",
                "Severe Wind / Storm",
                $"High winds or storm conditions expected. Consider keeping {child.DisplayName} indoors.",
                "wind >= 30 or storm", expiry);
        }

        if (!string.IsNullOrEmpty(w.Aqi) && s.AirQualitySensitive
            && int.TryParse(w.Aqi, out var aqi) && aqi > 100)
        {
            yield return Build(child, w.Id, RecommendationTypeEnum.HealthCaution, "high",
                "Poor Air Quality",
                $"AQI is {aqi}. Limit outdoor activity and consider a mask for {child.DisplayName}.",
                "air_quality_sensitive + aqi > 100", expiry);
        }

        if (!string.IsNullOrEmpty(w.PollenLevel) && s.PollenSensitive
            && w.PollenLevel.Equals("high", StringComparison.OrdinalIgnoreCase))
        {
            yield return Build(child, w.Id, RecommendationTypeEnum.HealthCaution, "normal",
                "High Pollen Level",
                $"Pollen levels are high. Monitor {child.DisplayName} for allergy symptoms.",
                "pollen_sensitive + pollen_level = high", expiry);
        }
    }

    public static IEnumerable<Recommendation> EvaluateRoutineRules(Child child, ChildSensitivity s)
    {
        var now = DateTimeOffset.UtcNow;
        var currentTime = TimeOnly.FromDateTime(now.LocalDateTime);

        foreach (var routine in child.Routines)
        {
            if (routine.RoutineType == RoutineTypeEnum.Sleep)
            {
                var diff = routine.StartTime - currentTime;
                if (diff.TotalMinutes is > 0 and <= 30)
                {
                    yield return Build(child, null, RecommendationTypeEnum.Bedtime, "normal",
                        "Bedtime Approaching",
                        $"{child.DisplayName}'s bedtime is in ~{(int)diff.TotalMinutes} minutes. Start winding down.",
                        "bedtime_routine within 30 minutes",
                        now.AddMinutes(45));
                }
            }
        }
    }

    public static IEnumerable<Recommendation> EvaluateEventRules(Child child, ChildSensitivity s, CalendarEvent calEvent, WeatherSnapshot? w)
    {
        var expiry = calEvent.StartDatetime.AddHours(2);

        if (calEvent.LocationType == LocationTypeEnum.Outdoor && w != null)
        {
            if (w.WeatherCondition == WeatherConditionEnum.Rainy)
            {
                yield return Build(child, w.Id, RecommendationTypeEnum.CarryItem, "normal",
                    $"Prepare for '{calEvent.Title}'",
                    $"Rain expected during '{calEvent.Title}'. Pack waterproof gear for {child.DisplayName}.",
                    "outdoor_event + rainy weather",
                    expiry, calEvent.Id);
            }

            if (w.UvIndex >= 6 && s.UvSensitive)
            {
                yield return Build(child, w.Id, RecommendationTypeEnum.WeatherAlert, "high",
                    $"UV Risk for '{calEvent.Title}'",
                    $"High UV during outdoor event '{calEvent.Title}'. Apply sunscreen before leaving.",
                    "outdoor_event + uv_sensitive + high_uv",
                    expiry, calEvent.Id);
            }
        }
    }

    private static Recommendation Build(
        Child child, int? weatherSnapshotId,
        RecommendationTypeEnum type, string priority,
        string title, string message, string reason,
        DateTimeOffset expiresAt, int? calendarEventId = null) =>
        new()
        {
            FamilyId = child.FamilyId,
            ChildId = child.Id,
            WeatherSnapshotId = weatherSnapshotId,
            CalendarEventId = calendarEventId,
            RecommendationType = type,
            Priority = priority,
            Title = title,
            Message = message,
            Reason = reason,
            Status = RecommendationStatusEnum.Pending,
            GeneratedBy = "recommendation_engine",
            DeliveryType = DeliveryTypeEnum.InApp,
            ExpiresAt = expiresAt,
        };
}

using System.Text.RegularExpressions;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;

namespace Backend.Infrastructure.Ai;

/// <summary>
/// Deterministic placeholder extractor — keyword/regex matching.
/// Swap for an LLM-backed implementation by registering a different IChildProfileExtractor in DI.
/// </summary>
internal sealed class RuleBasedChildProfileExtractor : IChildProfileExtractor
{
    private static readonly Regex SleepTimeRegex = new(
        @"(?:sleep|sleeps|bedtime).*?(?:at\s+)?(\d{1,2})(?::(\d{2}))?\s*(am|pm)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<ChildExtractionResult> ExtractAsync(
        string text,
        string sourceUserId,
        CancellationToken cancellationToken = default)
    {
        var insights = new List<ChildInsight>();
        var lower = text.ToLowerInvariant();

        // Sensitivity matches
        TryAddSensitivity(insights, lower,
            keywords: ["cold", "gets sick when cold", "cold-sensitive", "cold weather"],
            field: nameof(Domain.Entities.ChildSensitivity.ColdSensitive),
            description: "Cold-sensitive observation detected.",
            confidence: 0.8);

        TryAddSensitivity(insights, lower,
            keywords: ["hot weather", "overheats", "heat-sensitive", "heat sensitive"],
            field: nameof(Domain.Entities.ChildSensitivity.HeatSensitive),
            description: "Heat-sensitive observation detected.",
            confidence: 0.8);

        TryAddSensitivity(insights, lower,
            keywords: ["uv", "sunburn", "sunburns easily", "skin sensitive to sun"],
            field: nameof(Domain.Entities.ChildSensitivity.UvSensitive),
            description: "UV-sensitive observation detected.",
            confidence: 0.85);

        TryAddSensitivity(insights, lower,
            keywords: ["asthma", "wheez", "wheezes", "respiratory", "shortness of breath"],
            field: nameof(Domain.Entities.ChildSensitivity.RespiratorySensitive),
            description: "Respiratory sensitivity detected.",
            confidence: 0.9);

        TryAddSensitivity(insights, lower,
            keywords: ["pollen", "hay fever", "allergic to grass", "spring allergies"],
            field: nameof(Domain.Entities.ChildSensitivity.PollenSensitive),
            description: "Pollen sensitivity detected.",
            confidence: 0.85);

        TryAddSensitivity(insights, lower,
            keywords: ["air quality", "smog", "polluted", "aqi"],
            field: nameof(Domain.Entities.ChildSensitivity.AirQualitySensitive),
            description: "Air-quality sensitivity detected.",
            confidence: 0.8);

        TryAddSensitivity(insights, lower,
            keywords: ["windy", "cold windy", "wind makes"],
            field: nameof(Domain.Entities.ChildSensitivity.RespiratorySensitive),
            description: "Wind/cold trigger detected — flagged as respiratory.",
            confidence: 0.65);

        // Routine: sleep time
        var sleepMatch = SleepTimeRegex.Match(text);
        if (sleepMatch.Success && TryParseTime(sleepMatch, out var sleepTime))
        {
            insights.Add(new ChildInsight(
                Description: $"Bedtime routine detected at {sleepTime:HH\\:mm}.",
                NoteType: NoteTypeEnum.Routine,
                Source: NoteSourceEnum.Ai,
                Confidence: 0.85,
                Sensitivity: null,
                Routine: new RoutineProposal(
                    Title: "Bedtime",
                    RoutineType: RoutineTypeEnum.Sleep,
                    StartTime: sleepTime,
                    EndTime: null,
                    LocationType: LocationTypeEnum.Indoor,
                    ActivityIntensity: ActivityIntensityEnum.Low)));
        }

        return Task.FromResult(new ChildExtractionResult(insights));
    }

    private static void TryAddSensitivity(
        List<ChildInsight> sink,
        string loweredText,
        string[] keywords,
        string field,
        string description,
        double confidence)
    {
        if (!keywords.Any(loweredText.Contains))
            return;

        // De-duplicate same-field insights — keep the highest-confidence match.
        var existing = sink.FindIndex(i => i.Sensitivity?.Field == field);
        if (existing >= 0)
        {
            if (sink[existing].Confidence >= confidence)
                return;
            sink.RemoveAt(existing);
        }

        sink.Add(new ChildInsight(
            Description: description,
            NoteType: NoteTypeEnum.Health,
            Source: NoteSourceEnum.Ai,
            Confidence: confidence,
            Sensitivity: new SensitivityChange(field, true),
            Routine: null));
    }

    private static bool TryParseTime(Match match, out TimeOnly time)
    {
        time = default;

        if (!int.TryParse(match.Groups[1].Value, out var hour))
            return false;

        var minutes = match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out var m) ? m : 0;
        var ampm = match.Groups[3].Value.ToLowerInvariant();

        if (ampm == "pm" && hour < 12) hour += 12;
        else if (ampm == "am" && hour == 12) hour = 0;

        if (hour is < 0 or > 23 || minutes is < 0 or > 59)
            return false;

        time = new TimeOnly(hour, minutes);
        return true;
    }
}

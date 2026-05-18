namespace Backend.Domain.Enums;

public enum FamilyItemCategory
{
    Medication = 0,
    School = 1,
    Sport = 2,
    Weather = 3,
    Hydration = 4,
    General = 5
}

public enum AIMemoryType
{
    IgnoredReminder = 0,
    FrequentlyForgotten = 1,
    BehaviorPattern = 2,
    EffectiveReminder = 3,
    SymptomHistory = 4,
    WeatherReaction = 5,
    Custom = 6
}

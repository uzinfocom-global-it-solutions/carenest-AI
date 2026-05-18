namespace Backend.Domain.Enums;

public enum WeatherConditionEnum { Sunny, Cloudy, Rainy, Snowy, Windy, Storm }

public enum RecommendationTypeEnum
{
    Clothing,
    Hydration,
    OutdoorActivity,
    CarryItem,
    WeatherAlert,
    Bedtime,
    HealthCaution,
    Reminder
}

public enum RecommendationStatusEnum { Pending, Delivered, Dismissed, Expired }

public enum FeedbackTypeEnum { Useful, NotUseful, TooFrequent, WrongTiming }

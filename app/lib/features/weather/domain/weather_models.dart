class WeatherModel {
  const WeatherModel({
    required this.locationKey,
    required this.temperatureC,
    required this.condition,
    this.feelsLikeC,
    this.humidityPercent,
    this.windKmh,
    this.uvIndex,
    this.aqi,
    this.pollenLevel,
  });

  final String locationKey;
  final double temperatureC;
  final String
  condition; // "Sunny" | "Cloudy" | "Rainy" | "Snowy" | "Storm" | "Windy"
  final double? feelsLikeC;
  final int? humidityPercent;
  final double? windKmh;
  final double? uvIndex;
  final String? aqi;
  final String? pollenLevel;

  factory WeatherModel.fromJson(Map<String, dynamic> json) => WeatherModel(
    locationKey: json['locationKey'] as String? ?? '',
    temperatureC: (json['temperatureC'] as num?)?.toDouble() ?? 0,
    condition: json['weatherCondition'] as String? ?? 'Cloudy',
    feelsLikeC: (json['feelsLikeC'] as num?)?.toDouble(),
    humidityPercent: json['humidityPercent'] as int?,
    windKmh: (json['windKmh'] as num?)?.toDouble(),
    uvIndex: (json['uvIndex'] as num?)?.toDouble(),
    aqi: json['aqi'] as String?,
    pollenLevel: json['pollenLevel'] as String?,
  );

  String get tempLabel => '${temperatureC.round()}°';
}

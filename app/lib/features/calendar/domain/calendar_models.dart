class CalendarEventModel {
  const CalendarEventModel({
    required this.id,
    required this.familyId,
    required this.title,
    required this.startDatetime,
    required this.endDatetime,
    required this.locationType,
    required this.activityIntensity,
    required this.weatherSensitive,
    this.childId,
    this.description,
    this.locationLabel,
  });

  final int id;
  final int familyId;
  final int? childId;
  final String title;
  final String? description;
  final DateTime startDatetime;
  final DateTime endDatetime;
  final String? locationLabel;
  final String locationType; // "Indoor" | "Outdoor" | "Remote"
  final String activityIntensity; // "Low" | "Medium" | "High"
  final bool weatherSensitive;

  bool get isOutdoor => locationType.toLowerCase() == 'outdoor';

  String get timeLabel {
    final h = startDatetime.hour.toString().padLeft(2, '0');
    final m = startDatetime.minute.toString().padLeft(2, '0');
    return '$h:$m';
  }

  factory CalendarEventModel.fromJson(Map<String, dynamic> json) =>
      CalendarEventModel(
        id: json['id'] as int,
        familyId: json['familyId'] as int,
        childId: json['childId'] as int?,
        title: json['title'] as String? ?? '',
        description: json['description'] as String?,
        startDatetime: DateTime.parse(json['startDatetime'] as String).toLocal(),
        endDatetime: DateTime.parse(json['endDatetime'] as String).toLocal(),
        locationLabel: json['locationLabel'] as String?,
        locationType: json['locationType'] as String? ?? 'Indoor',
        activityIntensity: json['activityIntensity'] as String? ?? 'Low',
        weatherSensitive: json['weatherSensitive'] as bool? ?? false,
      );
}

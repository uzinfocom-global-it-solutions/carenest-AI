class ChildRoutineModel {
  const ChildRoutineModel({
    required this.id,
    required this.childId,
    required this.title,
    required this.routineType,
    required this.startTime,
    this.endTime,
    required this.repeatPattern,
    required this.locationType,
    required this.weatherSensitive,
    required this.active,
  });

  final int id;
  final int childId;
  final String title;
  final String routineType;
  final String startTime; // "HH:mm:ss"
  final String? endTime;
  final String repeatPattern;
  final String locationType;
  final bool weatherSensitive;
  final bool active;

  factory ChildRoutineModel.fromJson(Map<String, dynamic> json) {
    String timeStr(dynamic v) => (v as String?) ?? '00:00:00';
    return ChildRoutineModel(
      id: json['id'] as int,
      childId: json['childId'] as int,
      title: json['title'] as String? ?? '',
      routineType: json['routineType'] as String? ?? '',
      startTime: timeStr(json['startTime']),
      endTime: json['endTime'] as String?,
      repeatPattern: json['repeatPattern'] as String? ?? 'Daily',
      locationType: json['locationType'] as String? ?? 'Indoor',
      weatherSensitive: json['weatherSensitive'] as bool? ?? false,
      active: json['active'] as bool? ?? true,
    );
  }

  String get timeLabel {
    final parts = startTime.split(':');
    if (parts.length < 2) return startTime;
    return '${parts[0].padLeft(2, '0')}:${parts[1].padLeft(2, '0')}';
  }

  bool get isOutdoor => locationType.toLowerCase() == 'outdoor';

  bool isActiveOnDay(DateTime day) {
    switch (repeatPattern.toLowerCase()) {
      case 'daily':
        return true;
      case 'weekdays':
        return day.weekday <= 5;
      case 'weekends':
        return day.weekday >= 6;
      case 'none':
        return false;
      default:
        return true;
    }
  }
}

class ChildNoteModel {
  const ChildNoteModel({
    required this.id,
    required this.childId,
    required this.noteType,
    required this.note,
    required this.source,
    required this.createdAt,
  });

  final int id;
  final int childId;
  final String noteType;
  final String note;
  final String source;
  final DateTime createdAt;

  factory ChildNoteModel.fromJson(Map<String, dynamic> json) => ChildNoteModel(
    id: json['id'] as int,
    childId: json['childId'] as int,
    noteType: json['noteType'] as String? ?? 'Observation',
    note: json['note'] as String? ?? '',
    source: json['source'] as String? ?? '',
    createdAt: DateTime.tryParse(json['createdAt'] as String? ?? '') ?? DateTime.now(),
  );
}

class ChildSensitivityModel {
  const ChildSensitivityModel({
    this.heatSensitive = false,
    this.coldSensitive = false,
    this.airQualitySensitive = false,
    this.pollenSensitive = false,
    this.uvSensitive = false,
    this.skinSensitive = false,
    this.activitySensitive = false,
    this.respiratorySensitive = false,
  });

  final bool heatSensitive;
  final bool coldSensitive;
  final bool airQualitySensitive;
  final bool pollenSensitive;
  final bool uvSensitive;
  final bool skinSensitive;
  final bool activitySensitive;
  final bool respiratorySensitive;

  bool get hasAnySensitivity =>
      heatSensitive || coldSensitive || airQualitySensitive ||
      pollenSensitive || uvSensitive || skinSensitive ||
      activitySensitive || respiratorySensitive;

  List<String> get activeLabels {
    final labels = <String>[];
    if (heatSensitive)        labels.add('Heat');
    if (coldSensitive)        labels.add('Cold');
    if (airQualitySensitive)  labels.add('Air quality');
    if (pollenSensitive)      labels.add('Pollen');
    if (uvSensitive)          labels.add('UV / Sun');
    if (skinSensitive)        labels.add('Skin');
    if (activitySensitive)    labels.add('Activity');
    if (respiratorySensitive) labels.add('Respiratory');
    return labels;
  }

  factory ChildSensitivityModel.fromJson(Map<String, dynamic> json) =>
      ChildSensitivityModel(
        heatSensitive:        json['heatSensitive'] as bool? ?? false,
        coldSensitive:        json['coldSensitive'] as bool? ?? false,
        airQualitySensitive:  json['airQualitySensitive'] as bool? ?? false,
        pollenSensitive:      json['pollenSensitive'] as bool? ?? false,
        uvSensitive:          json['uvSensitive'] as bool? ?? false,
        skinSensitive:        json['skinSensitive'] as bool? ?? false,
        activitySensitive:    json['activitySensitive'] as bool? ?? false,
        respiratorySensitive: json['respiratorySensitive'] as bool? ?? false,
      );

  Map<String, dynamic> toJson() => {
    'heatSensitive':        heatSensitive,
    'coldSensitive':        coldSensitive,
    'airQualitySensitive':  airQualitySensitive,
    'pollenSensitive':      pollenSensitive,
    'uvSensitive':          uvSensitive,
    'skinSensitive':        skinSensitive,
    'activitySensitive':    activitySensitive,
    'respiratorySensitive': respiratorySensitive,
  };
}

class ChildModel {
  const ChildModel({
    required this.id,
    required this.familyId,
    required this.displayName,
    required this.ageYears,
    required this.ageGroup,
    this.gender,
  });

  final int id;
  final int familyId;
  final String displayName;
  final int ageYears;
  final String ageGroup;
  final String? gender;

  factory ChildModel.fromJson(Map<String, dynamic> json) => ChildModel(
    id: json['id'] as int,
    familyId: json['familyId'] as int,
    displayName: json['displayName'] as String? ?? '',
    ageYears: json['ageYears'] as int? ?? 0,
    ageGroup: json['ageGroup'] as String? ?? '',
    gender: json['gender'] as String?,
  );

  String get ageLabel {
    final suffix = ageYears == 1 ? 'year' : 'years';
    final g = gender != null && gender!.isNotEmpty ? ' · $gender' : '';
    return '$ageYears $suffix · ${ageGroup.toLowerCase()}$g';
  }
}

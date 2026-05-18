class FamilyInfo {
  const FamilyInfo({required this.id, required this.name});
  final int id;
  final String name;

  factory FamilyInfo.fromJson(Map<String, dynamic> json) =>
      FamilyInfo(id: json['id'] as int, name: json['name'] as String? ?? '');
}

class ChildInfo {
  const ChildInfo({
    required this.id,
    required this.name,
    required this.ageYears,
  });
  final int id;
  final String name;
  final int ageYears;

  factory ChildInfo.fromJson(Map<String, dynamic> json) => ChildInfo(
    id: json['id'] as int,
    name: json['displayName'] as String? ?? json['name'] as String? ?? '',
    ageYears: json['ageYears'] as int,
  );

  Map<String, dynamic> toJson() => {
    'id': id,
    'displayName': name,
    'ageYears': ageYears,
  };
}

class RecommendationModel {
  const RecommendationModel({
    required this.id,
    required this.childId,
    required this.type,
    required this.title,
    required this.message,
    this.reason,
  });

  final int id;
  final int childId;
  final String type;
  final String title;
  final String message;
  final String? reason;

  factory RecommendationModel.fromJson(Map<String, dynamic> json) =>
      RecommendationModel(
        id: json['id'] as int,
        childId: json['childId'] as int,
        type: (json['type'] as String?) ?? '',
        title: json['title'] as String? ?? '',
        message: json['message'] as String? ?? '',
        reason: json['reason'] as String?,
      );
}

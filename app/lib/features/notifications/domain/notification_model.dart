class NotificationModel {
  const NotificationModel({
    required this.id,
    required this.recommendationId,
    required this.title,
    required this.message,
    required this.type,
    required this.priority,
    required this.childId,
    required this.status,
    this.deliveredAt,
    this.openedAt,
    required this.createdAt,
  });

  final int id;
  final int recommendationId;
  final String title;
  final String message;
  final String type;
  final String priority;
  final int childId;
  final String status;
  final DateTime? deliveredAt;
  final DateTime? openedAt;
  final DateTime createdAt;

  bool get isUnread => openedAt == null;

  factory NotificationModel.fromJson(Map<String, dynamic> json) =>
      NotificationModel(
        id: json['id'] as int,
        recommendationId: json['recommendationId'] as int,
        title: json['title'] as String? ?? '',
        message: json['message'] as String? ?? '',
        type: json['type'] as String? ?? '',
        priority: json['priority'] as String? ?? 'Normal',
        childId: json['childId'] as int? ?? 0,
        status: json['status'] as String? ?? 'Pending',
        deliveredAt: _parse(json['deliveredAt']),
        openedAt: _parse(json['openedAt']),
        createdAt: _parse(json['createdAt']) ?? DateTime.now(),
      );

  static DateTime? _parse(dynamic v) {
    if (v is! String) return null;
    return DateTime.tryParse(v);
  }
}

import 'package:app/core/constants/api_constants.dart';
import 'package:app/shared/api/api_client.dart';
import 'package:app/features/notifications/domain/notification_model.dart';

class NotificationsService {
  NotificationsService({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  Future<List<NotificationModel>> list() async {
    final result = await _apiClient.get(ApiConstants.notifications);
    if (result is! List) return [];
    return result
        .map((e) => NotificationModel.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<void> markRead(int id) async {
    await _apiClient.post(ApiConstants.notificationRead(id), const {});
  }

  Future<void> markAllRead() async {
    await _apiClient.post(ApiConstants.notificationsReadAll, const {});
  }
}

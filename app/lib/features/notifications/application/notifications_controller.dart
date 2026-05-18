import 'package:flutter/foundation.dart';
import 'package:app/features/notifications/data/notifications_service.dart';
import 'package:app/features/notifications/domain/notification_model.dart';

class NotificationsController extends ChangeNotifier {
  NotificationsController({required NotificationsService service})
      : _service = service;

  final NotificationsService _service;

  bool isLoading = false;
  String? error;
  List<NotificationModel> items = [];

  int get unreadCount => items.where((n) => n.isUnread).length;

  Future<void> load() async {
    isLoading = true;
    error = null;
    notifyListeners();
    try {
      items = await _service.list();
    } catch (e) {
      error = e.toString();
      items = [];
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  Future<void> markRead(int id) async {
    final idx = items.indexWhere((n) => n.id == id);
    if (idx < 0) return;
    final current = items[idx];
    if (!current.isUnread) return;
    // Optimistic update — replace in place so the unread badge drops instantly.
    items[idx] = NotificationModel(
      id: current.id,
      recommendationId: current.recommendationId,
      title: current.title,
      message: current.message,
      type: current.type,
      priority: current.priority,
      childId: current.childId,
      status: 'Opened',
      deliveredAt: current.deliveredAt,
      openedAt: DateTime.now(),
      createdAt: current.createdAt,
    );
    notifyListeners();
    try {
      await _service.markRead(id);
    } catch (_) {
      // best-effort — leave optimistic state to avoid UI flapping
    }
  }

  Future<void> markAllRead() async {
    final now = DateTime.now();
    items = items
        .map((n) => n.isUnread
            ? NotificationModel(
                id: n.id,
                recommendationId: n.recommendationId,
                title: n.title,
                message: n.message,
                type: n.type,
                priority: n.priority,
                childId: n.childId,
                status: 'Opened',
                deliveredAt: n.deliveredAt,
                openedAt: now,
                createdAt: n.createdAt,
              )
            : n)
        .toList();
    notifyListeners();
    try {
      await _service.markAllRead();
    } catch (_) {}
  }

  void clear() {
    items = [];
    error = null;
    notifyListeners();
  }
}

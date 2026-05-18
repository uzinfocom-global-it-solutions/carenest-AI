import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import '../../voice/data/voice_playback_orchestrator.dart';
import 'local_notification_service.dart';
import 'notification_deep_link_handler.dart';

class PushNotificationHandler {
  PushNotificationHandler._();

  static Future<void> handleSseMessage(Map<String, dynamic> data) async {
    debugPrint('[Push][SSE] type=${data['type']} priority=${data['priority']}');

    if (!await _notificationsEnabled()) return;

    final type = data['type'] as String? ?? '';
    final priority = _parsePriority(data['priority'] as String?);

    if (type == 'voice_action') {
      await _handleVoiceAction(data, priority);
    } else {
      await _showGenericNotification(data, priority);
    }
  }

  static Future<void> _handleVoiceAction(
    Map<String, dynamic> data,
    NotificationPriorityLevel priority,
  ) async {
    final actionId = int.tryParse(data['actionId']?.toString() ?? '0') ?? 0;
    final text = data['text'] as String? ?? '';
    final requiresConfirmation = data['requiresConfirmation']?.toString() == 'true';

    await LocalNotificationService.instance.showVoiceAction(
      actionId: actionId,
      text: text,
      requiresConfirmation: requiresConfirmation,
      priority: priority,
    );

    if (text.isNotEmpty && await _voiceEnabled()) {
      final ttsPriority = priority == NotificationPriorityLevel.emergency
          ? 100
          : priority == NotificationPriorityLevel.high
              ? 50
              : 10;
      await VoicePlaybackOrchestrator.instance.enqueue(
        text,
        priority: ttsPriority,
        locale: 'ru-RU',
        interruptIfHigher: true,
      );
    }
  }

  static Future<void> _showGenericNotification(
    Map<String, dynamic> data,
    NotificationPriorityLevel priority,
  ) async {
    final title = data['title'] as String? ?? 'CareNestAI';
    final body = data['body'] as String? ?? '';
    if (body.isEmpty) return;

    await LocalNotificationService.instance.show(
      id: data.hashCode,
      title: title,
      body: body,
      priority: priority,
      payload: jsonEncode(data),
    );
  }

  static NotificationPriorityLevel _parsePriority(String? raw) =>
      switch (raw?.toLowerCase()) {
        'emergency' => NotificationPriorityLevel.emergency,
        'high' => NotificationPriorityLevel.high,
        'low' => NotificationPriorityLevel.low,
        _ => NotificationPriorityLevel.normal,
      };

  static Future<bool> _notificationsEnabled() async {
    try {
      const s = FlutterSecureStorage();
      final v = await s.read(key: 'notifications_enabled');
      return v != 'false';
    } catch (_) {
      return true;
    }
  }

  static Future<bool> _voiceEnabled() async {
    try {
      const s = FlutterSecureStorage();
      final v = await s.read(key: 'voice_with_notifications');
      return v != 'false';
    } catch (_) {
      return true;
    }
  }
}

class NotificationTapRouter {
  static final _listeners = <void Function(Map<String, dynamic>)>[];
  static void Function(String route)? _navigateTo;
  static String? _pendingRoute;

  static void setNavigator(void Function(String route) fn) {
    _navigateTo = fn;
    if (_pendingRoute != null) {
      final route = _pendingRoute!;
      _pendingRoute = null;
      fn(route);
    }
  }

  static void listen(void Function(Map<String, dynamic>) cb) => _listeners.add(cb);

  static void dispatch(Map<String, dynamic> data) {
    final type = data['type'] as String? ?? '';
    final hasChatMessageId = data.containsKey('chatMessageId');

    if (type == 'voice_action' || type == 'chat_message' || hasChatMessageId) {
      if (_navigateTo != null) {
        _navigateTo!('/chat');
      } else {
        _pendingRoute = '/chat';
      }
    }

    for (final l in _listeners) {
      l(data);
    }
  }

  static void handleTap(String? payload) {
    if (payload == null) return;
    try {
      final data = jsonDecode(payload) as Map<String, dynamic>;
      final chatMessageId = data['chatMessageId'];
      if (chatMessageId != null) {
        NotificationDeepLinkHandler.instance
            .setPendingChatMessage(int.tryParse(chatMessageId.toString()));
      }
      dispatch(data);
    } catch (_) {}
  }
}

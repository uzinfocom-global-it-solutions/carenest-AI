import 'dart:convert';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import '../../voice/data/voice_playback_orchestrator.dart';
import 'local_notification_service.dart';
import 'notification_deep_link_handler.dart';

class PushNotificationHandler {
  PushNotificationHandler._();

  static Future<void> handleForegroundMessage(RemoteMessage message) async {
    debugPrint('[FCM][FG] ${message.messageId} type=${message.data['type']}');

    if (!await _notificationsEnabled()) return;

    final type = message.data['type'] ?? '';
    final priority = _parsePriority(message.data['priority']);

    if (type == 'voice_action') {
      await _handleVoiceAction(message, priority);
    } else {
      await _showGenericNotification(message, priority);
    }
  }

  static Future<void> handleBackgroundMessage(RemoteMessage message) async {
    debugPrint('[FCM][BG] ${message.messageId}');

    final type = message.data['type'] ?? '';
    final priority = _parsePriority(message.data['priority']);

    if (type == 'voice_action') {
      await _handleVoiceAction(message, priority);
    }
  }

  static void handleNotificationTap(RemoteMessage message) {
    debugPrint('[FCM][TAP] ${message.messageId}');

    // Store pending deep-link so screens can scroll to a specific message.
    final chatMessageId = message.data['chatMessageId'];
    if (chatMessageId != null) {
      NotificationDeepLinkHandler.instance.setPendingChatMessage(
        int.tryParse(chatMessageId.toString()),
      );
    }

    // Route to notification center — navigation is handled by the app shell
    NotificationTapRouter.dispatch(message.data);
  }

  static Future<void> _handleVoiceAction(
    RemoteMessage message,
    NotificationPriorityLevel priority,
  ) async {
    final data = message.data;
    final actionId = int.tryParse(data['actionId'] ?? '0') ?? 0;
    final text = data['text'] ?? message.notification?.body ?? '';
    final requiresConfirmation = data['requiresConfirmation'] == 'true';

    // Show local notification with quick-action buttons
    await LocalNotificationService.instance.showVoiceAction(
      actionId: actionId,
      text: text,
      requiresConfirmation: requiresConfirmation,
      priority: priority,
    );

    // Auto TTS playback — routed through orchestrator (priority queue, no overlap)
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
    RemoteMessage message,
    NotificationPriorityLevel priority,
  ) async {
    final notification = message.notification;
    if (notification == null) return;

    await LocalNotificationService.instance.show(
      id: message.hashCode,
      title: notification.title ?? 'CareNestAI',
      body: notification.body ?? '',
      priority: priority,
      payload: jsonEncode(message.data),
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
  // Queues a route when the navigator isn't wired yet (app terminated state).
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

    // Any notification tied to a voice action or chat message opens AI chat.
    if (type == 'voice_action' || type == 'chat_message' || hasChatMessageId) {
      if (_navigateTo != null) {
        _navigateTo!('/chat');
      } else {
        // App launched from terminated state — router not ready yet.
        // setNavigator() will flush this when the widget tree initializes.
        _pendingRoute = '/chat';
      }
    }

    for (final l in _listeners) {
      l(data);
    }
  }
}

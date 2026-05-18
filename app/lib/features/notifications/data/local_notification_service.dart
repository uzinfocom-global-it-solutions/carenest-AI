import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';

class LocalNotificationService {
  LocalNotificationService._();
  static final LocalNotificationService instance = LocalNotificationService._();

  final _plugin = FlutterLocalNotificationsPlugin();

  static const _defaultChannelId = 'default_channel';
  static const _highChannelId = 'high_priority_channel';
  static const _emergencyChannelId = 'emergency_channel';
  static const _medicationChannelId = 'medication_channel';

  Future<void> initialize() async {
    const android = AndroidInitializationSettings('@mipmap/ic_launcher');
    const ios = DarwinInitializationSettings(
      requestAlertPermission: false,
      requestBadgePermission: false,
      requestSoundPermission: false,
    );

    await _plugin.initialize(
      const InitializationSettings(android: android, iOS: ios),
      onDidReceiveNotificationResponse: _onNotificationResponse,
      onDidReceiveBackgroundNotificationResponse: _onBackgroundNotificationResponse,
    );

    await _createChannels();
  }

  Future<void> _createChannels() async {
    const channels = [
      AndroidNotificationChannel(
        _defaultChannelId,
        'Уведомления',
        description: 'Стандартные уведомления CareNestAI',
        importance: Importance.defaultImportance,
      ),
      AndroidNotificationChannel(
        _highChannelId,
        'Важные уведомления',
        description: 'Напоминания и важные оповещения',
        importance: Importance.high,
        enableVibration: true,
        playSound: true,
      ),
      AndroidNotificationChannel(
        _emergencyChannelId,
        'Экстренные уведомления',
        description: 'Критически важные оповещения',
        importance: Importance.max,
        enableVibration: true,
        playSound: true,
        showBadge: true,
      ),
      AndroidNotificationChannel(
        _medicationChannelId,
        'Лекарства',
        description: 'Напоминания о приёме лекарств',
        importance: Importance.high,
        enableVibration: true,
        playSound: true,
        showBadge: true,
      ),
    ];

    final androidPlugin = _plugin
        .resolvePlatformSpecificImplementation<AndroidFlutterLocalNotificationsPlugin>();

    for (final channel in channels) {
      await androidPlugin?.createNotificationChannel(channel);
    }
  }

  Future<void> show({
    required int id,
    required String title,
    required String body,
    required NotificationPriorityLevel priority,
    String? payload,
    List<AndroidNotificationAction>? actions,
  }) async {
    final channelId = _channelForPriority(priority);

    final androidDetails = AndroidNotificationDetails(
      channelId,
      _channelNameForPriority(priority),
      importance: _importanceForPriority(priority),
      priority: _priorityForPriority(priority),
      showWhen: true,
      enableVibration: true,
      playSound: true,
      actions: actions,
      styleInformation: BigTextStyleInformation(body),
    );

    const iosDetails = DarwinNotificationDetails(
      presentAlert: true,
      presentBadge: true,
      presentSound: true,
    );

    await _plugin.show(
      id,
      title,
      body,
      NotificationDetails(android: androidDetails, iOS: iosDetails),
      payload: payload,
    );
  }

  Future<void> showVoiceAction({
    required int actionId,
    required String text,
    required bool requiresConfirmation,
    required NotificationPriorityLevel priority,
  }) async {
    final actions = requiresConfirmation
        ? [
            const AndroidNotificationAction('confirm', 'Подтвердить ✅', showsUserInterface: true),
            const AndroidNotificationAction('remind_later', 'Напомнить позже ⏰'),
            const AndroidNotificationAction('skip', 'Пропустить ❌'),
          ]
        : [
            const AndroidNotificationAction('skip', 'Закрыть'),
          ];

    await show(
      id: actionId,
      title: priority == NotificationPriorityLevel.emergency
          ? '🚨 Экстренное уведомление'
          : '🤖 CareNestAI',
      body: text,
      priority: priority,
      payload: 'voice_action:$actionId',
      actions: actions,
    );
  }

  Future<void> cancel(int id) => _plugin.cancel(id);
  Future<void> cancelAll() => _plugin.cancelAll();

  static void _onNotificationResponse(NotificationResponse response) {
    debugPrint('[LocalNotif] action=${response.actionId} payload=${response.payload}');
    NotificationActionRouter.handle(response.actionId, response.payload);
  }

  @pragma('vm:entry-point')
  static void _onBackgroundNotificationResponse(NotificationResponse response) {
    NotificationActionRouter.handle(response.actionId, response.payload);
  }

  String _channelForPriority(NotificationPriorityLevel p) => switch (p) {
        NotificationPriorityLevel.emergency => _emergencyChannelId,
        NotificationPriorityLevel.high => _highChannelId,
        NotificationPriorityLevel.medication => _medicationChannelId,
        _ => _defaultChannelId,
      };

  String _channelNameForPriority(NotificationPriorityLevel p) => switch (p) {
        NotificationPriorityLevel.emergency => 'Экстренные уведомления',
        NotificationPriorityLevel.high => 'Важные уведомления',
        NotificationPriorityLevel.medication => 'Лекарства',
        _ => 'Уведомления',
      };

  Importance _importanceForPriority(NotificationPriorityLevel p) => switch (p) {
        NotificationPriorityLevel.emergency => Importance.max,
        NotificationPriorityLevel.high => Importance.high,
        NotificationPriorityLevel.medication => Importance.high,
        _ => Importance.defaultImportance,
      };

  Priority _priorityForPriority(NotificationPriorityLevel p) => switch (p) {
        NotificationPriorityLevel.emergency => Priority.max,
        NotificationPriorityLevel.high => Priority.high,
        _ => Priority.defaultPriority,
      };
}

enum NotificationPriorityLevel { low, normal, high, emergency, medication }

class NotificationActionRouter {
  static void handle(String? actionId, String? payload) {
    debugPrint('[NotifAction] action=$actionId payload=$payload');
    _actionStream.add(NotificationAction(actionId: actionId, payload: payload));
  }

  static final NotificationActionStream _actionStream = NotificationActionStream();
  static NotificationActionStream get stream => _actionStream;
}

class NotificationActionStream {
  final _listeners = <void Function(NotificationAction)>[];

  void add(NotificationAction action) {
    for (final l in _listeners) {
      l(action);
    }
  }

  void listen(void Function(NotificationAction) listener) {
    _listeners.add(listener);
  }
}

class NotificationAction {
  final String? actionId;
  final String? payload;
  const NotificationAction({this.actionId, this.payload});
}

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import '../../../shared/api/api_client.dart';
import 'local_notification_service.dart';
import 'push_notification_handler.dart';

/// Top-level background message handler — must be a top-level function.
@pragma('vm:entry-point')
Future<void> firebaseBackgroundHandler(RemoteMessage message) async {
  await Firebase.initializeApp(); // Required: background isolate has no app context
  // flutter_local_notifications must be initialized in the background isolate
  // before any show() call, otherwise it silently fails on Android.
  await LocalNotificationService.instance.initialize();
  await PushNotificationHandler.handleBackgroundMessage(message);
}

class FirebaseService {
  FirebaseService._();
  static final FirebaseService instance = FirebaseService._();

  final _messaging = FirebaseMessaging.instance;
  String? _currentToken;

  Future<void> initialize(ApiClient apiClient) async {
    await LocalNotificationService.instance.initialize();

    // Request permissions
    final settings = await _messaging.requestPermission(
      alert: true,
      badge: true,
      sound: true,
      provisional: false,
      announcement: true,
      criticalAlert: true,
    );

    debugPrint('[FCM] Permission: ${settings.authorizationStatus}');

    if (settings.authorizationStatus == AuthorizationStatus.denied) return;

    // Register background handler
    FirebaseMessaging.onBackgroundMessage(firebaseBackgroundHandler);

    // Foreground message handler
    FirebaseMessaging.onMessage.listen(
      (msg) => PushNotificationHandler.handleForegroundMessage(msg),
    );

    // Notification tap when app in background/foreground
    FirebaseMessaging.onMessageOpenedApp.listen(
      (msg) => PushNotificationHandler.handleNotificationTap(msg),
    );

    // Android 13+ — ask notification permission via permission_handler if needed
    await _messaging.setForegroundNotificationPresentationOptions(
      alert: true,
      badge: true,
      sound: true,
    );

    // Handle terminated-state initial message
    final initial = await _messaging.getInitialMessage();
    if (initial != null) {
      PushNotificationHandler.handleNotificationTap(initial);
    }

    // Get token and register with backend
    await _refreshAndRegisterToken(apiClient);

    // Listen for token refresh
    _messaging.onTokenRefresh.listen((newToken) {
      _currentToken = newToken;
      _registerTokenWithBackend(apiClient, newToken);
    });
  }

  Future<void> _refreshAndRegisterToken(ApiClient apiClient) async {
    try {
      final token = await _messaging.getToken();
      if (token != null) {
        _currentToken = token;
        await _registerTokenWithBackend(apiClient, token);
      }
    } catch (e) {
      debugPrint('[FCM] Token error: $e');
    }
  }

  /// Call this after the user has authenticated so the FCM token
  /// gets registered even if initialize() ran before login completed.
  Future<void> ensureTokenRegistered(ApiClient apiClient) async {
    await _refreshAndRegisterToken(apiClient);
  }

  Future<void> _registerTokenWithBackend(ApiClient apiClient, String token) async {
    try {
      await apiClient.post('/api/v1/devices/register', {
        'token': token,
        'platform': defaultTargetPlatform.name.toLowerCase(),
        'deviceName': 'Flutter App',
      });
      debugPrint('[FCM] Token registered with backend');
    } catch (e) {
      debugPrint('[FCM] Token registration failed: $e');
    }
  }

  String? get currentToken => _currentToken;
}

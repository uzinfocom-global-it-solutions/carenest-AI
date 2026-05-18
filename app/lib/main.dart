import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'core/config/app_config.dart';
import 'features/auth/application/auth_controller.dart';
import 'features/auth/data/auth_repository_impl.dart';
import 'features/auth/data/token_storage.dart';
import 'features/onboarding/application/onboarding_controller.dart';
import 'features/onboarding/data/onboarding_service.dart';
import 'features/children/application/children_controller.dart';
import 'features/children/data/children_service.dart';
import 'features/weather/application/weather_controller.dart';
import 'features/weather/data/weather_service.dart';
import 'features/weather/data/location_service.dart';
import 'features/calendar/application/calendar_controller.dart';
import 'features/calendar/data/calendar_service.dart';
import 'features/chats/application/chat_controller.dart';
import 'features/chats/data/chat_service.dart';
import 'features/recommendations/application/recommendations_controller.dart';
import 'features/recommendations/data/recommendations_service.dart';
import 'features/notifications/application/notifications_controller.dart';
import 'features/notifications/application/voice_notification_controller.dart';
import 'features/notifications/data/notifications_service.dart';
import 'features/notifications/data/firebase_service.dart';
import 'features/notifications/data/sse_client.dart';
import 'features/settings/application/app_settings_controller.dart';
import 'features/voice/data/voice_service.dart';
import 'features/voice/data/voice_playback_orchestrator.dart';
import 'shared/api/api_client.dart';
import 'shared/lifecycle/app_lifecycle_observer.dart';
import 'package:go_router/go_router.dart';
import 'shared/navigation/app_router.dart';
import 'shared/theme/app_theme.dart';
import 'features/notifications/data/push_notification_handler.dart';
import 'features/monitoring/application/monitoring_controller.dart';
import 'features/monitoring/data/monitoring_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await AppConfig.load();
  await Firebase.initializeApp();
  runApp(const CareNestAiApp());
}

class CareNestAiApp extends StatefulWidget {
  const CareNestAiApp({super.key});

  @override
  State<CareNestAiApp> createState() => _CareNestAiAppState();
}

class _CareNestAiAppState extends State<CareNestAiApp> {
  late final TokenStorage _tokenStorage;
  late final ApiClient _apiClient;
  late final AuthController _authController;
  late final OnboardingController _onboardingController;
  late final ChildrenController _childrenController;
  late final WeatherController _weatherController;
  late final CalendarController _calendarController;
  late final ChatController _chatController;
  late final RecommendationsController _recommendationsController;
  late final NotificationsController _notificationsController;
  late final VoiceNotificationController _voiceNotificationController;
  late final SseClient _sseClient;
  late final AppLifecycleObserver _lifecycleObserver;
  late final AppSettingsController _appSettingsController;
  late final VoiceService _voiceService;
  late final MonitoringController _monitoringController;
  late final GoRouter _router;

  bool _familyChecked = false;
  bool _bootstrappedForFamily = false;
  int? _activeFamilyId;

  // SSE events that arrive before _activeFamilyId is initialised are buffered
  // here and replayed immediately after family context is ready.
  final _pendingSseEvents = <SseMessage>[];

  @override
  void initState() {
    super.initState();
    _tokenStorage = TokenStorage();
    _apiClient = ApiClient(
      tokenProvider: _tokenStorage.getAccessToken,
      // On 401, try to rotate the refresh token transparently — gives the
      // 15-minute access token an unlimited extension via the 30-day refresh
      // token, so the user isn't kicked out mid-conversation.
      refreshToken: () async {
        final refresh = await _tokenStorage.getRefreshToken();
        if (refresh == null) return null;
        try {
          final repo = AuthRepositoryImpl(apiClient: _apiClient);
          final tokens = await repo.refresh(refresh);
          // Keep existing display name / email — refresh response doesn't carry them.
          final displayName = await _tokenStorage.getDisplayName();
          final email = await _tokenStorage.getEmail();
          await _tokenStorage.saveTokens(
            userId: tokens.userId,
            accessToken: tokens.accessToken,
            refreshToken: tokens.refreshToken,
            displayName: displayName,
            email: email,
          );
          return tokens.accessToken;
        } catch (_) {
          return null; // refresh expired/revoked — onUnauthorized will fire
        }
      },
      onUnauthorized: () async {
        // The server rejected the cached token (e.g. tokens from a previous
        // dev DB / restart). Wipe local creds so the router bounces to /login.
        await _authController.forceLogout();
      },
    );

    _authController = AuthController(
      repository: AuthRepositoryImpl(apiClient: _apiClient),
      tokenStorage: _tokenStorage,
    );
    _onboardingController = OnboardingController(
      service: OnboardingService(apiClient: _apiClient),
      storage: _tokenStorage,
      auth: _authController,
    );
    _childrenController = ChildrenController(
      service: ChildrenService(apiClient: _apiClient),
    );
    _weatherController = WeatherController(
      service: WeatherService(apiClient: _apiClient),
    );
    _calendarController = CalendarController(
      service: CalendarService(apiClient: _apiClient),
    );
    _chatController = ChatController(
      service: ChatService(apiClient: _apiClient),
    );
    _recommendationsController = RecommendationsController(
      service: RecommendationsService(apiClient: _apiClient),
    );
    _notificationsController = NotificationsController(
      service: NotificationsService(apiClient: _apiClient),
    );
    _voiceNotificationController = VoiceNotificationController(_apiClient);
    _monitoringController = MonitoringController(
      service: MonitoringService(api: _apiClient),
    );
    _sseClient = SseClient(tokenProvider: _tokenStorage.getAccessToken)
      ..events.listen(_onSseEvent);

    _lifecycleObserver = AppLifecycleObserver(sseClient: _sseClient)
      ..addResumeCallback(_voiceNotificationController.loadPending)
      ..addResumeCallback(_notificationsController.load);
    WidgetsBinding.instance.addObserver(_lifecycleObserver);

    _appSettingsController = AppSettingsController()..load();
    _voiceService = VoiceService();

    // Initialize Firebase after the first frame so BuildContext is ready
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      await FirebaseService.instance.initialize(_apiClient);
    });

    _router = buildRouter(_authController);

    // Wire push notification taps to the GoRouter so that tapping a
    // notification always opens /chat, even when the app was terminated.
    NotificationTapRouter.setNavigator((route) => _router.go(route));

    _authController.addListener(_onAuthChanged);
    _authController.tryAutoLogin().then((_) => _resolveFamilyAndBootstrap());
  }

  void _onSseEvent(SseMessage msg) {
    // Buffer events that arrive before the family context is ready.
    // They will be replayed in _replaySseBuffer() once _activeFamilyId is set.
    if (_activeFamilyId == null) {
      debugPrint('[SSE] Buffered (familyId not ready): ${msg.eventType}');
      _pendingSseEvents.add(msg);
      return;
    }
    _dispatchSseEvent(msg);
  }

  void _dispatchSseEvent(SseMessage msg) {
    debugPrint('[SSE] Dispatching: ${msg.eventType}');
    _voiceNotificationController.onSseEvent(msg.eventType, msg.json ?? {});

    switch (msg.eventType) {
      case 'chat_message_created':
        _chatController.onChatSseEvent(msg.eventType, msg.json ?? {});
      case 'child_created':
      case 'child_updated':
        final fid = _activeFamilyId;
        if (fid != null) {
          debugPrint('[SSE] child event → reloading children family=$fid');
          _childrenController.loadForFamily(fid);
        }
      case 'event_created':
      case 'event_updated':
        final fid = _activeFamilyId;
        if (fid != null) {
          debugPrint('[SSE] calendar event → reloading week family=$fid');
          _calendarController.loadWeek(fid);
        }
      case 'weather_updated':
        final json = msg.json;
        if (json != null) {
          final key = json['locationKey'] as String? ?? 'home';
          final lat = (json['latitude'] as num?)?.toDouble();
          final lon = (json['longitude'] as num?)?.toDouble();
          debugPrint('[SSE] weather_updated → silent reload key=$key');
          _weatherController.load(key, lat: lat, lon: lon);
        }
      case 'monitoring_session_started':
      case 'monitoring_session_updated':
      case 'monitoring_escalated':
      case 'risk_score_updated':
      case 'predictive_risk_detected':
      case 'monitoring_plan_updated':
      case 'followup_chain_updated':
      case 'ai_decision_created':
      case 'risk_escalated':
      case 'followup_escalated':
      case 'family_context_updated':
        _monitoringController.onSseEvent(msg.eventType, msg.json ?? {});
    }
  }

  void _replaySseBuffer() {
    if (_pendingSseEvents.isEmpty) return;
    final events = List<SseMessage>.from(_pendingSseEvents);
    _pendingSseEvents.clear();
    debugPrint('[SSE] Replaying ${events.length} buffered events');
    for (final msg in events) {
      _dispatchSseEvent(msg);
    }
  }

  @override
  void dispose() {
    _authController.removeListener(_onAuthChanged);
    _lifecycleObserver.dispose();
    _sseClient.dispose();
    _voiceNotificationController.dispose();
    _voiceService.dispose();
    VoicePlaybackOrchestrator.instance.dispose();
    super.dispose();
  }

  void _onAuthChanged() {
    if (!_authController.isAuthenticated) {
      _familyChecked = false;
      _bootstrappedForFamily = false;
      _activeFamilyId = null;
      _pendingSseEvents.clear();
      _weatherController.stopPeriodicRefresh();
      _sseClient.disconnect();
      _chatController.clear();
      _childrenController.clear();
      _recommendationsController.clear();
      _notificationsController.clear();
      _monitoringController.clear();
      return;
    }
    _resolveFamilyAndBootstrap();
  }

  Future<void> _resolveFamilyAndBootstrap() async {
    if (!_authController.isAuthenticated) return;

    // 1) Ask the backend whether this user already has a family, so we route to
    //    home directly instead of asking returning users to "create one".
    //    Skipped for returning users — tryAutoLogin() already resolved family
    //    from secure storage, so there's no need to block on a network call.
    if (!_familyChecked) {
      _familyChecked = true;
      if (_authController.familyResolved) {
        // Returning user: family already confirmed from local cache — skip the
        // round-trip to /families/mine so the router reaches /home instantly.
        debugPrint('[Bootstrap] Family resolved from cache — skipping getMyFamily()');
      } else {
        // New login or no cached familyId: must ask the backend.
        try {
          final family = await _onboardingController.service.getMyFamily();
          await _authController.markFamilyResolved(family?.id);
        } catch (_) {
          _familyChecked = false;
        }
      }
    }

    if (!_authController.hasFamily) return;
    if (_bootstrappedForFamily) return;
    _bootstrappedForFamily = true;

    _sseClient.connect();
    FirebaseService.instance.ensureTokenRegistered(_apiClient);

    final familyId = await _tokenStorage.getFamilyId();
    if (familyId == null) return;
    _activeFamilyId = familyId;
    debugPrint('[App] familyId initialised: $familyId — replaying SSE buffer');
    _replaySseBuffer();

    // GPS with 3-second timeout so a slow fix doesn't block the whole bootstrap.
    final locationFuture = LocationService()
        .getLocation()
        .timeout(const Duration(seconds: 3), onTimeout: () => null)
        .catchError((_) => null);

    // Resolve existing chat ID from storage while other requests are in-flight.
    final existingChatIdFuture = _tokenStorage.getActiveChatId();

    // All independent data loads in parallel — cuts boot time significantly.
    await Future.wait([
      _onboardingController.loadFromStorage(),
      _childrenController.loadForFamily(familyId),
      _childrenController.loadRoutinesForFamily(familyId),
      _calendarController.loadWeek(familyId),
    ]);

    // Weather: fire-and-forget — never block navigation on a weather fetch.
    // The controller notifies listeners when it completes.
    final location = await locationFuture;
    if (location != null) {
      _weatherController.load(
        location.locationKey,
        lat: location.latitude,
        lon: location.longitude,
      );
    } else {
      _weatherController.load('home', lat: 41.2995, lon: 69.2401);
    }
    // Keep weather fresh with a background timer (every 30 min).
    _weatherController.startPeriodicRefresh();

    // Chat history + secondary data in parallel, don't await weather (non-blocking).
    final existingChatId = await existingChatIdFuture;
    if (existingChatId != null) {
      await _chatController.loadHistory(existingChatId);
    }
    if (_chatController.activeChatId == null) {
      await _chatController.initializeChat(familyId);
      if (_chatController.activeChatId != null) {
        await _tokenStorage.saveActiveChatId(_chatController.activeChatId!);
      }
    }
    _chatController.startPolling();

    // Secondary data — don't block navigation on these.
    Future.wait([
      _recommendationsController.loadForChildren(_childrenController.children),
      _notificationsController.load(),
      _voiceNotificationController.loadPending(),
      _monitoringController.loadForFamily(familyId),
    ]);
  }

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider.value(value: _authController),
        ChangeNotifierProvider.value(value: _onboardingController),
        ChangeNotifierProvider.value(value: _childrenController),
        ChangeNotifierProvider.value(value: _weatherController),
        ChangeNotifierProvider.value(value: _calendarController),
        ChangeNotifierProvider.value(value: _chatController),
        ChangeNotifierProvider.value(value: _recommendationsController),
        ChangeNotifierProvider.value(value: _notificationsController),
        ChangeNotifierProvider.value(value: _voiceNotificationController),
        ChangeNotifierProvider.value(value: VoicePlaybackOrchestrator.instance),
        ChangeNotifierProvider.value(value: _appSettingsController),
        ChangeNotifierProvider.value(value: _voiceService),
        ChangeNotifierProvider.value(value: _monitoringController),
      ],
      child: MaterialApp.router(
        title: 'CareNestAI',
        debugShowCheckedModeBanner: false,
        theme: AppTheme.light,
        routerConfig: _router,
      ),
    );
  }
}

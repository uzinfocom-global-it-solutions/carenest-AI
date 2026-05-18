import 'package:flutter/widgets.dart';
import '../../features/notifications/data/sse_client.dart';

/// Observes app lifecycle and reconnects SSE when the app comes to foreground.
/// Also triggers data refresh callbacks registered by feature controllers.
class AppLifecycleObserver extends WidgetsBindingObserver {
  AppLifecycleObserver({required this.sseClient});

  final SseClient sseClient;
  final _resumeCallbacks = <Future<void> Function()>[];

  void addResumeCallback(Future<void> Function() cb) {
    _resumeCallbacks.add(cb);
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    switch (state) {
      case AppLifecycleState.resumed:
        _onResume();
      case AppLifecycleState.paused:
        // Keep SSE alive in background — don't disconnect
        break;
      case AppLifecycleState.detached:
        sseClient.disconnect();
      default:
        break;
    }
  }

  void _onResume() {
    // Reconnect SSE if it dropped while backgrounded
    if (!sseClient.connected) {
      sseClient.connect();
    }

    // Refresh feature data
    for (final cb in _resumeCallbacks) {
      cb().catchError((_) {});
    }
  }

  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
  }
}

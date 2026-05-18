import 'package:flutter/widgets.dart';
import '../../features/notifications/data/sse_client.dart';

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
        break;
      case AppLifecycleState.detached:
        sseClient.disconnect();
      default:
        break;
    }
  }

  void _onResume() {
    if (!sseClient.connected) {
      sseClient.connect();
    }

    for (final cb in _resumeCallbacks) {
      cb().catchError((_) {});
    }
  }

  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
  }
}

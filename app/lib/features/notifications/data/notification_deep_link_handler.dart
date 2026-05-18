import 'package:flutter/foundation.dart';

/// Stores pending deep-link data from push notification taps so that the
/// target screen (e.g. ChatScreen) can read it after navigation completes.
class NotificationDeepLinkHandler {
  NotificationDeepLinkHandler._();
  static final instance = NotificationDeepLinkHandler._();

  int? _pendingScrollToMessageId;

  /// The chat message ID the screen should scroll to on next open.
  int? get pendingScrollToMessageId => _pendingScrollToMessageId;

  /// Store a pending scroll target coming from a notification tap.
  void setPendingChatMessage(int? messageId) {
    _pendingScrollToMessageId = messageId;
    debugPrint('[DeepLink] pending chat message id=$messageId');
  }

  /// Consume and clear the pending scroll target.
  void clearPendingScroll() => _pendingScrollToMessageId = null;
}

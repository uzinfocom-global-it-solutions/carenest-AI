import 'package:flutter/foundation.dart';

class NotificationDeepLinkHandler {
  NotificationDeepLinkHandler._();
  static final instance = NotificationDeepLinkHandler._();

  int? _pendingScrollToMessageId;

  int? get pendingScrollToMessageId => _pendingScrollToMessageId;

  void setPendingChatMessage(int? messageId) {
    _pendingScrollToMessageId = messageId;
    debugPrint('[DeepLink] pending chat message id=$messageId');
  }

  void clearPendingScroll() => _pendingScrollToMessageId = null;
}

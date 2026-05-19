import 'dart:async';
import 'package:flutter/foundation.dart';
import 'package:app/features/chats/data/chat_service.dart';
import 'package:app/features/chats/domain/chat_models.dart';

class ChatController extends ChangeNotifier {
  ChatController({required ChatService service}) : _service = service;

  Timer? _pollTimer;

  final ChatService _service;

  bool isLoading = false;
  String? error;

  int? activeChatId;
  List<ChatMessageModel> messages = [];
  bool isSending = false;

  void startPolling({Duration interval = const Duration(minutes: 5)}) {
    _pollTimer?.cancel();
    _pollTimer = Timer.periodic(interval, (_) => _silentRefresh());
  }

  void stopPolling() {
    _pollTimer?.cancel();
    _pollTimer = null;
  }

  Future<void> _silentRefresh() async {
    if (activeChatId == null || isSending) return;
    try {
      final fresh = await _service.getHistory(activeChatId!);
      fresh.sort((a, b) => a.id.compareTo(b.id));
      if (fresh.length > messages.length ||
          (fresh.isNotEmpty &&
              messages.isNotEmpty &&
              fresh.last.id != messages.last.id)) {
        messages = fresh;
        notifyListeners();
      }
    } catch (_) {}
  }

  Future<void> refreshOnResume() => _silentRefresh();

  Future<void> initializeChat(
    int familyId, {
    String chatType = 'Family',
  }) async {
    isLoading = true;
    error = null;
    notifyListeners();
    try {
      final chat = await _service.createChat(familyId, chatType: chatType);
      activeChatId = chat.id;
      messages = [];
    } catch (e) {
      error = e.toString();
      activeChatId = null;
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  Future<void> loadHistory(int chatId) async {
    isLoading = true;
    error = null;
    notifyListeners();
    try {
      activeChatId = chatId;
      messages = await _service.getHistory(chatId);
      messages.sort((a, b) => a.id.compareTo(b.id));
    } catch (e) {
      error = e.toString();
      activeChatId = null;
      messages = [];
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  bool isConfirming = false;
  int? confirmingMessageId;

  Future<bool> confirmProposal(int messageId, {String? edits}) async {
    if (activeChatId == null) return false;
    isConfirming = true;
    confirmingMessageId = messageId;
    error = null;
    notifyListeners();
    try {
      final reply = await _service.confirmProposal(
        activeChatId!,
        messageId,
        edits: edits,
      );
      messages.add(reply);
      return true;
    } catch (e) {
      error = e.toString();
      return false;
    } finally {
      isConfirming = false;
      confirmingMessageId = null;
      notifyListeners();
    }
  }

  Future<void> sendMessage(String text, {String inputMode = 'Text', String locale = 'en'}) async {
    if (activeChatId == null || text.trim().isEmpty) return;

    final tempId = DateTime.now().millisecondsSinceEpoch;
    final tempMsg = ChatMessageModel(
      id: tempId,
      chatId: activeChatId!,
      senderType: 'User',
      messageType: 'Message',
      content: text,
    );

    messages.add(tempMsg);
    isSending = true;
    error = null;
    notifyListeners();

    try {
      final responseMsg = await _service.sendMessage(
        activeChatId!,
        text,
        inputMode: inputMode,
        locale: locale,
      );
      messages.add(responseMsg);
    } catch (e) {
      messages.removeWhere((m) => m.id == tempId);
      error = e.toString();
    } finally {
      isSending = false;
      notifyListeners();
    }
  }

  void onChatSseEvent(String eventType, Map<String, dynamic> data) {
    if (eventType == 'chat_message_created') {
      final chatId = data['chatId'] as int?;
      if (chatId != null && chatId == activeChatId) {
        // Backend sends a partial payload (chatId, messageId, content only),
        // so fetch the full message list rather than trying to build a model inline.
        _silentRefresh();
      }
    }
  }

  void clear() {
    stopPolling();
    activeChatId = null;
    messages = [];
    error = null;
    notifyListeners();
  }

  @override
  void dispose() {
    stopPolling();
    super.dispose();
  }
}

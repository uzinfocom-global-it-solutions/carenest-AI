import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:app/shared/api/api_client.dart';

class AIFeedItem {
  final int id;
  final int chatId;
  final String content;
  final String eventType;
  final String? correlationId;
  final String status;
  final DateTime createdAt;

  const AIFeedItem({
    required this.id,
    required this.chatId,
    required this.content,
    required this.eventType,
    this.correlationId,
    required this.status,
    required this.createdAt,
  });

  factory AIFeedItem.fromJson(Map<String, dynamic> json) {
    String eventType = 'ProactiveRecommendation';
    String? correlationId;
    String status = 'Pending';

    final rawIntent = json['parsedIntent'] as String?;
    if (rawIntent != null && rawIntent.isNotEmpty) {
      try {
        final intent = jsonDecode(rawIntent) as Map<String, dynamic>;
        eventType = intent['eventType'] as String? ?? eventType;
        correlationId = intent['correlationId'] as String?;
        status = intent['status'] as String? ?? status;
      } catch (_) {}
    }

    final rawContent = json['aiResponse'] as String? ?? json['content'] as String? ?? '';
    final content = rawContent.startsWith('[proactive] ')
        ? rawContent.substring('[proactive] '.length)
        : rawContent;

    return AIFeedItem(
      id: json['id'] as int,
      chatId: json['chatId'] as int,
      content: content,
      eventType: eventType,
      correlationId: correlationId,
      status: status,
      createdAt: json['createdAt'] != null
          ? DateTime.tryParse(json['createdAt'] as String) ?? DateTime.now()
          : DateTime.now(),
    );
  }
}

class AIFeedController extends ChangeNotifier {
  AIFeedController({required ApiClient api}) : _api = api;

  final ApiClient _api;

  List<AIFeedItem> _items = [];
  bool _loading = false;
  String? _error;

  List<AIFeedItem> get items => List.unmodifiable(_items);
  bool get loading => _loading;
  String? get error => _error;

  Future<void> load() async {
    _loading = true;
    _error = null;
    notifyListeners();
    try {
      final result = await _api.get('/api/v1/ai-feed');
      if (result is List) {
        _items = result
            .map((e) => AIFeedItem.fromJson(e as Map<String, dynamic>))
            .toList();
        _items.sort((a, b) => b.createdAt.compareTo(a.createdAt));
      }
    } catch (e) {
      _error = e.toString();
      debugPrint('[AIFeedController] load error: $e');
    } finally {
      _loading = false;
      notifyListeners();
    }
  }

  void onSseEvent(String eventType, Map<String, dynamic> data) {
    if (eventType == 'chat_message_created') {
      load();
    }
  }
}

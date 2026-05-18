import 'dart:convert';

class ChatModel {
  const ChatModel({
    required this.id,
    required this.familyId,
    required this.createdByUserId,
    required this.chatType,
    this.title,
  });

  final int id;
  final int familyId;
  final String createdByUserId;
  final String chatType;
  final String? title;

  factory ChatModel.fromJson(Map<String, dynamic> json) => ChatModel(
    id: json['id'] as int,
    familyId: json['familyId'] as int,
    createdByUserId: json['createdByUserId'] as String,
    chatType: json['chatType'] as String,
    title: json['title'] as String?,
  );
}

class ChatProposal {
  const ChatProposal({
    required this.type,
    required this.summary,
    required this.params,
  });

  final String type;
  final String summary;
  final Map<String, dynamic> params;

  static ChatProposal? tryParse(String? parsedIntentJson) {
    if (parsedIntentJson == null || parsedIntentJson.isEmpty) return null;
    try {
      final decoded = jsonDecode(parsedIntentJson);
      if (decoded is! Map<String, dynamic>) return null;
      final p = decoded['proposal'];
      if (p is! Map<String, dynamic>) return null;
      final type = p['type'];
      final summary = p['summary'];
      if (type is! String || summary is! String) return null;
      final params = (p['params'] is Map<String, dynamic>)
          ? p['params'] as Map<String, dynamic>
          : <String, dynamic>{};
      return ChatProposal(type: type, summary: summary, params: params);
    } catch (_) {
      return null;
    }
  }
}

class ChatMessageModel {
  const ChatMessageModel({
    required this.id,
    required this.chatId,
    this.userId,
    required this.senderType,
    required this.messageType,
    required this.content,
    this.aiResponse,
    this.parsedIntent,
    this.successful = true,
    this.createdAt,
  });

  final int id;
  final int chatId;
  final String? userId;
  final String senderType; // "Ai" | "User" | "System"
  final String
  messageType; // "Message" | "Question" | "Recommendation" | "Reminder" | "Proposal" | "Confirmation"
  final String content;
  final String? aiResponse;
  final String? parsedIntent;
  final bool successful;
  final DateTime? createdAt;

  ChatProposal? get proposal => ChatProposal.tryParse(parsedIntent);
  bool get isProposal => messageType == 'Proposal' && proposal != null;

  bool get isProactive {
    if (senderType != 'Ai' && senderType != 'System') return false;
    try {
      final intent = jsonDecode(parsedIntent ?? '{}') as Map<String, dynamic>;
      return intent['isProactive'] == true;
    } catch (_) {
      return false;
    }
  }

  String? get proactiveEventType {
    try {
      final intent = jsonDecode(parsedIntent ?? '{}') as Map<String, dynamic>;
      return intent['eventType'] as String?;
    } catch (_) {
      return null;
    }
  }

  String? get correlationId {
    try {
      final intent = jsonDecode(parsedIntent ?? '{}') as Map<String, dynamic>;
      return intent['correlationId'] as String?;
    } catch (_) {
      return null;
    }
  }


  factory ChatMessageModel.fromJson(Map<String, dynamic> json) =>
      ChatMessageModel(
        id: json['id'] as int,
        chatId: json['chatId'] as int,
        userId: json['userId'] as String?,
        senderType: json['senderType'] as String,
        messageType: json['messageType'] as String,
        content: json['content'] as String? ?? '',
        aiResponse: json['aiResponse'] as String?,
        parsedIntent: json['parsedIntent'] as String?,
        successful: json['successful'] as bool? ?? true,
        createdAt: json['createdAt'] != null
            ? DateTime.tryParse(json['createdAt'] as String)
            : null,
      );
}

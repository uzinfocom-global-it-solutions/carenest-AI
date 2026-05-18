import 'package:app/core/constants/api_constants.dart';
import 'package:app/shared/api/api_client.dart';
import 'package:app/features/chats/domain/chat_models.dart';

class ChatService {
  ChatService({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  Future<ChatModel> createChat(
    int familyId, {
    String chatType = 'Family',
    String? title,
  }) async {
    final result = await _apiClient.post(ApiConstants.createChat, {
      'familyId': familyId,
      'chatType': chatType,
      'title': title,
    });
    return ChatModel.fromJson(result as Map<String, dynamic>);
  }

  Future<ChatMessageModel> sendMessage(
    int chatId,
    String content, {
    String inputMode = 'Text',
    String locale = 'en',
  }) async {
    final result = await _apiClient.post(
      ApiConstants.sendMessage(chatId),
      {'content': content, 'inputMode': inputMode, 'locale': locale},
      ApiClient.aiTimeout,
    );
    return ChatMessageModel.fromJson(result as Map<String, dynamic>);
  }

  Future<ChatMessageModel> confirmProposal(
    int chatId,
    int messageId, {
    String? edits,
  }) async {
    final result = await _apiClient.post(
      ApiConstants.confirmProposal(chatId, messageId),
      {'edits': edits},
    );
    return ChatMessageModel.fromJson(result as Map<String, dynamic>);
  }

  Future<List<ChatMessageModel>> getHistory(
    int chatId, {
    int limit = 50,
  }) async {
    final result = await _apiClient.get(
      '${ApiConstants.getHistory(chatId)}?limit=$limit',
    );
    if (result is! List) return [];
    return result
        .map((e) => ChatMessageModel.fromJson(e as Map<String, dynamic>))
        .toList();
  }
}

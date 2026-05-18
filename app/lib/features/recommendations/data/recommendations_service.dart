import 'package:app/core/constants/api_constants.dart';
import 'package:app/shared/api/api_client.dart';
import 'package:app/features/onboarding/domain/onboarding_models.dart';

class RecommendationsService {
  RecommendationsService({required ApiClient apiClient})
    : _apiClient = apiClient;

  final ApiClient _apiClient;

  Future<List<RecommendationModel>> generateForChild(int childId) async {
    final result = await _apiClient.post(
      ApiConstants.generateRecs(childId),
      {},
    );
    if (result is! List) return [];
    return result
        .map((e) => RecommendationModel.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<void> submitFeedback(int recId, String feedbackType) async {
    await _apiClient.post(ApiConstants.submitFeedback(recId), {
      'feedbackType': feedbackType,
    });
  }
}

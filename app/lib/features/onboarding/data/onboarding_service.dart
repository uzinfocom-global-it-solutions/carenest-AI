import 'package:app/core/constants/api_constants.dart';
import 'package:app/shared/api/api_client.dart';
import 'package:app/features/onboarding/domain/onboarding_models.dart';

class OnboardingService {
  OnboardingService({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  Future<FamilyInfo> createFamily(String name) async {
    final json =
        await _apiClient.post(ApiConstants.createFamily, {'name': name})
            as Map<String, dynamic>;
    return FamilyInfo.fromJson(json);
  }

  /// Returns the user's active family, or null if they don't belong to one yet.
  Future<FamilyInfo?> getMyFamily() async {
    try {
      final json = await _apiClient.get(ApiConstants.myFamily);
      if (json is! Map<String, dynamic>) return null;
      return FamilyInfo.fromJson(json);
    } on ApiException catch (e) {
      if (e.statusCode == 404) return null;
      rethrow;
    }
  }

  Future<ChildInfo> createChild(int familyId, String name, int ageYears) async {
    final json =
        await _apiClient.post(ApiConstants.createChild, {
              'familyId': familyId,
              'displayName': name,
              'ageYears': ageYears,
            })
            as Map<String, dynamic>;
    return ChildInfo.fromJson(json);
  }

  Future<void> updateSensitivity(
    int childId, {
    required bool heat,
    required bool cold,
    required bool airQuality,
    required bool pollen,
    required bool uv,
    required bool skin,
    required bool activity,
    required bool respiratory,
  }) async {
    await _apiClient.put(ApiConstants.childSensitivity(childId), {
      'heatSensitive': heat,
      'coldSensitive': cold,
      'airQualitySensitive': airQuality,
      'pollenSensitive': pollen,
      'uvSensitive': uv,
      'skinSensitive': skin,
      'activitySensitive': activity,
      'respiratorySensitive': respiratory,
    });
  }

  Future<List<RecommendationModel>> generateRecommendations(int childId) async {
    final result = await _apiClient.post(
      ApiConstants.generateRecs(childId),
      {},
    );
    if (result is! List) return [];
    return result
        .map((e) => RecommendationModel.fromJson(e as Map<String, dynamic>))
        .toList();
  }
}

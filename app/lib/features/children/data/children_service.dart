import 'package:app/core/constants/api_constants.dart';
import 'package:app/shared/api/api_client.dart';
import 'package:app/features/children/domain/child_models.dart';

class ChildrenService {
  ChildrenService({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  Future<List<ChildModel>> listByFamily(int familyId) async {
    final result = await _apiClient.get(ApiConstants.listChildrenByFamily(familyId));
    if (result is! List) return [];
    return result.map((e) => ChildModel.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<ChildModel> getChild(int childId) async {
    final result = await _apiClient.get(ApiConstants.getChild(childId));
    return ChildModel.fromJson(result as Map<String, dynamic>);
  }

  Future<ChildSensitivityModel> getSensitivity(int childId) async {
    final result = await _apiClient.get(ApiConstants.childSensitivity(childId));
    return ChildSensitivityModel.fromJson(result as Map<String, dynamic>);
  }

  Future<void> updateSensitivity(int childId, ChildSensitivityModel s) async {
    await _apiClient.put(ApiConstants.childSensitivity(childId), s.toJson());
  }

  Future<ChildModel> updateChild(
    int childId, {
    required String displayName,
    required int ageYears,
  }) async {
    final json = await _apiClient.put(
      ApiConstants.getChild(childId),
      {'displayName': displayName, 'ageYears': ageYears},
    ) as Map<String, dynamic>;
    return ChildModel.fromJson(json);
  }

  Future<List<ChildNoteModel>> getNotes(int childId, {String? noteType}) async {
    var path = ApiConstants.childNotes(childId);
    if (noteType != null) path += '?noteType=$noteType';
    final result = await _apiClient.get(path);
    if (result is! List) return [];
    return result.map((e) => ChildNoteModel.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<ChildNoteModel> addNote(int childId, {
    required String noteType,
    required String note,
  }) async {
    final result = await _apiClient.post(ApiConstants.childNotes(childId), {
      'noteType': noteType,
      'note': note,
      'source': 'Manual',
      'needsConfirmation': false,
    }) as Map<String, dynamic>;
    return ChildNoteModel.fromJson(result);
  }

  Future<List<ChildRoutineModel>> getRoutinesForFamily(int familyId) async {
    final result = await _apiClient.get(ApiConstants.routinesByFamily(familyId));
    if (result is! List) return [];
    return result.map((e) => ChildRoutineModel.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<ChildModel> createChild({
    required int familyId,
    required String displayName,
    required int ageYears,
  }) async {
    final json = await _apiClient.post(ApiConstants.createChild, {
      'familyId': familyId,
      'displayName': displayName,
      'ageYears': ageYears,
    }) as Map<String, dynamic>;
    return ChildModel.fromJson(json);
  }
}

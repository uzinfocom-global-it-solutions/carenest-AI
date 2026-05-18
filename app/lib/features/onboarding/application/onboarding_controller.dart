import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:app/features/auth/application/auth_controller.dart';
import 'package:app/features/auth/data/token_storage.dart';
import 'package:app/features/onboarding/data/onboarding_service.dart';
import 'package:app/features/onboarding/domain/onboarding_models.dart';

class ChildDraft {
  const ChildDraft({required this.name, required this.ageYears});
  final String name;
  final int ageYears;
}

class OnboardingController extends ChangeNotifier {
  OnboardingController({
    required OnboardingService service,
    required TokenStorage storage,
    required AuthController auth,
  }) : _service = service,
       _storage = storage,
       _auth = auth;

  final OnboardingService _service;
  final TokenStorage _storage;
  final AuthController _auth;

  OnboardingService get service => _service;

  bool isLoading = false;
  String? error;
  FamilyInfo? family;
  List<ChildDraft> draftChildren = [];
  List<ChildInfo> createdChildren = [];
  List<RecommendationModel> recommendations = [];

  Future<void> loadFromStorage() async {
    final familyId = await _storage.getFamilyId();
    if (familyId != null) {
      family = FamilyInfo(id: familyId, name: '');
    }
    final json = await _storage.getChildrenJson();
    if (json != null) {
      try {
        final list = jsonDecode(json) as List;
        createdChildren = list
            .map((c) => ChildInfo.fromJson(c as Map<String, dynamic>))
            .toList();
      } catch (_) {}
    }
    notifyListeners();
  }

  Future<bool> createFamily(String name) async {
    isLoading = true;
    error = null;
    notifyListeners();
    try {
      family = await _service.createFamily(
        name.trim().isEmpty ? 'My Family' : name.trim(),
      );
      await _auth.markFamilyResolved(family!.id);
      return true;
    } on Exception catch (e) {
      error = e.toString();
      return false;
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  void addDraftChild(String name, int ageYears) {
    draftChildren = [
      ...draftChildren,
      ChildDraft(name: name, ageYears: ageYears),
    ];
    notifyListeners();
  }

  void removeDraftChild(int index) {
    final list = [...draftChildren];
    list.removeAt(index);
    draftChildren = list;
    notifyListeners();
  }

  Future<bool> submitChildren() async {
    if (family == null) return false;
    if (draftChildren.isEmpty) return true;
    isLoading = true;
    error = null;
    notifyListeners();
    try {
      final results = <ChildInfo>[];
      for (final draft in draftChildren) {
        final child = await _service.createChild(
          family!.id,
          draft.name,
          draft.ageYears,
        );
        results.add(child);
      }
      createdChildren = results;
      final json = jsonEncode(createdChildren.map((c) => c.toJson()).toList());
      await _storage.saveChildrenJson(json);
      return true;
    } on Exception catch (e) {
      error = e.toString();
      return false;
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> saveSensitivities(
    int childId,
    Map<String, bool> sensitivities,
  ) async {
    isLoading = true;
    error = null;
    notifyListeners();
    try {
      await _service.updateSensitivity(
        childId,
        heat: sensitivities['heat'] ?? false,
        cold: sensitivities['cold'] ?? false,
        airQuality: sensitivities['airQuality'] ?? false,
        pollen: sensitivities['pollen'] ?? false,
        uv: sensitivities['uv'] ?? false,
        skin: sensitivities['skin'] ?? false,
        activity: sensitivities['activity'] ?? false,
        respiratory: sensitivities['respiratory'] ?? false,
      );
      return true;
    } on Exception catch (e) {
      error = e.toString();
      return false;
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  Future<void> loadRecommendations() async {
    if (createdChildren.isEmpty) return;
    isLoading = true;
    notifyListeners();
    try {
      final all = <RecommendationModel>[];
      for (final child in createdChildren) {
        final recs = await _service.generateRecommendations(child.id);
        all.addAll(recs);
      }
      recommendations = all;
    } catch (_) {
      recommendations = [];
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  void reset() {
    draftChildren = [];
    createdChildren = [];
    family = null;
    recommendations = [];
    error = null;
    notifyListeners();
  }
}

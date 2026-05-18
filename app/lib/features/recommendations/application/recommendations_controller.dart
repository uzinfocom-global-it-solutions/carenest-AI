import 'package:flutter/foundation.dart';
import 'package:app/features/children/domain/child_models.dart';
import 'package:app/features/onboarding/domain/onboarding_models.dart';
import 'package:app/features/recommendations/data/recommendations_service.dart';

class RecommendationsController extends ChangeNotifier {
  RecommendationsController({required RecommendationsService service})
    : _service = service;

  final RecommendationsService _service;

  bool isLoading = false;
  String? error;
  List<RecommendationModel> recommendations = const [];

  int _loadVersion = 0;

  Future<void> loadForChildren(List<ChildModel> children) async {
    if (children.isEmpty) {
      recommendations = const [];
      notifyListeners();
      return;
    }
    final v = ++_loadVersion;
    isLoading = true;
    error = null;
    notifyListeners();
    debugPrint('[Recs] Load started (v$v) for ${children.length} children');
    try {
      final all = <RecommendationModel>[];
      for (final child in children) {
        final recs = await _service.generateForChild(child.id);
        if (v != _loadVersion) {
          debugPrint('[Recs] Stale response discarded mid-load (v$v)');
          return;
        }
        all.addAll(recs);
      }
      if (v != _loadVersion) {
        debugPrint('[Recs] Stale response discarded (v$v, current v$_loadVersion)');
        return;
      }
      recommendations = List.unmodifiable(all);
      debugPrint('[Recs] State updated (v$v): ${recommendations.length} recs');
    } catch (e) {
      if (v != _loadVersion) return;
      error = e.toString();
      recommendations = const [];
      debugPrint('[Recs] Load error (v$v): $e');
    } finally {
      if (v == _loadVersion) {
        isLoading = false;
        notifyListeners();
        debugPrint('[Recs] notifyListeners (v$v)');
      }
    }
  }

  Future<void> submitFeedback(int recId, String feedbackType) async {
    try {
      await _service.submitFeedback(recId, feedbackType);
    } catch (_) {}
  }

  void clear() {
    _loadVersion++;
    recommendations = const [];
    error = null;
    isLoading = false;
    notifyListeners();
  }
}

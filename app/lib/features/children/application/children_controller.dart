import 'package:flutter/foundation.dart';
import 'package:app/features/children/data/children_service.dart';
import 'package:app/features/children/domain/child_models.dart';

class ChildrenController extends ChangeNotifier {
  ChildrenController({required ChildrenService service}) : _service = service;

  final ChildrenService _service;

  bool isLoading = false;
  bool isCreating = false;
  String? error;
  List<ChildModel> children = const [];
  List<ChildRoutineModel> routines = const [];
  int? _lastFamilyId;

  int _childrenVersion = 0;
  int _routinesVersion = 0;

  int? get familyId => _lastFamilyId;

  Future<void> loadForFamily(int familyId) async {
    _lastFamilyId = familyId;
    final v = ++_childrenVersion;
    isLoading = true;
    error = null;
    notifyListeners();
    debugPrint('[Children] Load started (v$v) family=$familyId');
    try {
      final result = await _service.listByFamily(familyId);
      if (v != _childrenVersion) {
        debugPrint('[Children] Stale response discarded (v$v, current v$_childrenVersion)');
        return;
      }
      children = List.unmodifiable(result);
      debugPrint('[Children] State updated (v$v): ${children.length} children');
    } catch (e) {
      if (v != _childrenVersion) return;
      error = e.toString();
      children = const [];
      debugPrint('[Children] Load error (v$v): $e');
    } finally {
      if (v == _childrenVersion) {
        isLoading = false;
        notifyListeners();
        debugPrint('[Children] notifyListeners (v$v)');
      }
    }
  }

  Future<ChildModel?> addChild({
    required int familyId,
    required String name,
    required int ageYears,
  }) async {
    isCreating = true;
    error = null;
    notifyListeners();
    try {
      final created = await _service.createChild(
        familyId: familyId,
        displayName: name,
        ageYears: ageYears,
      );
      children = List.unmodifiable([...children, created]);
      return created;
    } catch (e) {
      error = e.toString();
      return null;
    } finally {
      isCreating = false;
      notifyListeners();
    }
  }

  final Map<int, List<ChildNoteModel>> _notes = {};

  List<ChildNoteModel> notesFor(int childId) => _notes[childId] ?? const [];

  Future<void> loadNotes(int childId) async {
    try {
      final notes = await _service.getNotes(childId);
      _notes[childId] = notes;
      notifyListeners();
    } catch (_) {}
  }

  Future<bool> addNote(int childId, {required String noteType, required String note}) async {
    try {
      final added = await _service.addNote(childId, noteType: noteType, note: note);
      _notes[childId] = [added, ...(_notes[childId] ?? [])];
      notifyListeners();
      return true;
    } catch (e) {
      error = e.toString();
      notifyListeners();
      return false;
    }
  }

  final Map<int, ChildSensitivityModel> _sensitivities = {};

  ChildSensitivityModel? sensitivityFor(int childId) => _sensitivities[childId];

  Future<ChildSensitivityModel?> loadSensitivity(int childId) async {
    try {
      final s = await _service.getSensitivity(childId);
      _sensitivities[childId] = s;
      notifyListeners();
      return s;
    } catch (_) {
      return null;
    }
  }

  Future<bool> updateSensitivity(int childId, ChildSensitivityModel s) async {
    try {
      await _service.updateSensitivity(childId, s);
      _sensitivities[childId] = s;
      notifyListeners();
      return true;
    } catch (e) {
      error = e.toString();
      notifyListeners();
      return false;
    }
  }

  Future<ChildModel?> updateChild(
    int childId, {
    required String displayName,
    required int ageYears,
  }) async {
    try {
      final updated = await _service.updateChild(
        childId,
        displayName: displayName,
        ageYears: ageYears,
      );
      children = List.unmodifiable(
        children.map((c) => c.id == childId ? updated : c).toList(),
      );
      notifyListeners();
      return updated;
    } catch (e) {
      error = e.toString();
      notifyListeners();
      return null;
    }
  }

  Future<void> loadRoutinesForFamily(int familyId) async {
    final v = ++_routinesVersion;
    debugPrint('[Children] Routines load started (v$v) family=$familyId');
    try {
      final result = await _service.getRoutinesForFamily(familyId);
      if (v != _routinesVersion) {
        debugPrint('[Children] Stale routines discarded (v$v)');
        return;
      }
      routines = List.unmodifiable(result);
    } catch (_) {
      if (v != _routinesVersion) return;
      routines = const [];
    }
    notifyListeners();
  }

  List<ChildRoutineModel> routinesForDay(DateTime day) =>
      routines.where((r) => r.isActiveOnDay(day)).toList()
        ..sort((a, b) => a.startTime.compareTo(b.startTime));

  Future<void> refresh() async {
    if (_lastFamilyId != null) {
      await loadForFamily(_lastFamilyId!);
    }
  }

  void clear() {
    _childrenVersion++;
    _routinesVersion++;
    children = const [];
    routines = const [];
    error = null;
    isLoading = false;
    isCreating = false;
    _lastFamilyId = null;
    notifyListeners();
  }
}

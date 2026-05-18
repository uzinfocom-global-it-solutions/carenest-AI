import 'package:flutter/foundation.dart';
import 'package:app/shared/api/api_client.dart';

class FamilyItemModel {
  final int id;
  final int familyId;
  final int? childId;
  final String name;
  final String category;
  final bool isRequired;
  final bool isActive;
  final String? notes;

  const FamilyItemModel({
    required this.id,
    required this.familyId,
    this.childId,
    required this.name,
    required this.category,
    required this.isRequired,
    required this.isActive,
    this.notes,
  });

  factory FamilyItemModel.fromJson(Map<String, dynamic> json) => FamilyItemModel(
        id: json['id'] as int,
        familyId: json['familyId'] as int,
        childId: json['childId'] as int?,
        name: json['name'] as String? ?? '',
        category: json['category'] as String? ?? 'General',
        isRequired: json['isRequired'] as bool? ?? false,
        isActive: json['isActive'] as bool? ?? true,
        notes: json['notes'] as String?,
      );

  FamilyItemModel copyWith({bool? isActive}) => FamilyItemModel(
        id: id,
        familyId: familyId,
        childId: childId,
        name: name,
        category: category,
        isRequired: isRequired,
        isActive: isActive ?? this.isActive,
        notes: notes,
      );
}

class FamilyItemsController extends ChangeNotifier {
  FamilyItemsController({required ApiClient api}) : _api = api;

  final ApiClient _api;

  List<FamilyItemModel> _items = [];
  bool _loading = false;
  String? _error;

  List<FamilyItemModel> get items => List.unmodifiable(_items);
  bool get loading => _loading;
  String? get error => _error;

  Map<String, List<FamilyItemModel>> get byCategory {
    final map = <String, List<FamilyItemModel>>{};
    for (final item in _items) {
      map.putIfAbsent(item.category, () => []).add(item);
    }
    return map;
  }

  Future<void> load() async {
    _loading = true;
    _error = null;
    notifyListeners();
    try {
      final result = await _api.get('/api/v1/family-items');
      if (result is List) {
        _items = result
            .map((e) => FamilyItemModel.fromJson(e as Map<String, dynamic>))
            .toList();
      }
    } catch (e) {
      _error = e.toString();
      debugPrint('[FamilyItems] load error: $e');
    } finally {
      _loading = false;
      notifyListeners();
    }
  }

  Future<void> create({
    required String name,
    required String category,
    int? childId,
    bool isRequired = false,
    String? notes,
  }) async {
    try {
      final result = await _api.post('/api/v1/family-items', {
        'name': name,
        'category': category,
        'childId': childId,
        'isRequired': isRequired,
        'notes': notes,
      });
      if (result is Map<String, dynamic>) {
        _items.add(FamilyItemModel.fromJson(result));
        notifyListeners();
      }
    } catch (e) {
      _error = e.toString();
      debugPrint('[FamilyItems] create error: $e');
      notifyListeners();
      rethrow;
    }
  }

  Future<void> delete(int id) async {
    try {
      await _api.delete('/api/v1/family-items/$id');
      _items.removeWhere((i) => i.id == id);
      notifyListeners();
    } catch (e) {
      _error = e.toString();
      debugPrint('[FamilyItems] delete error: $e');
      notifyListeners();
      rethrow;
    }
  }

  Future<void> toggleActive(int id) async {
    final idx = _items.indexWhere((i) => i.id == id);
    if (idx == -1) return;
    final current = _items[idx];
    final newActive = !current.isActive;
    _items[idx] = current.copyWith(isActive: newActive);
    notifyListeners();
    try {
      await _api.put('/api/v1/family-items/$id', {'isActive': newActive});
    } catch (e) {
      _items[idx] = current;
      _error = e.toString();
      debugPrint('[FamilyItems] toggleActive error: $e');
      notifyListeners();
    }
  }
}

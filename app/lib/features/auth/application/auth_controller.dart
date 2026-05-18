import 'package:flutter/foundation.dart';
import 'package:app/shared/api/api_client.dart';
import 'package:app/features/auth/data/token_storage.dart';
import 'package:app/features/auth/domain/auth_repository.dart';

class AuthController extends ChangeNotifier {
  AuthController({
    required AuthRepository repository,
    required TokenStorage tokenStorage,
  }) : _repository = repository,
       _tokenStorage = tokenStorage;

  final AuthRepository _repository;
  final TokenStorage _tokenStorage;

  bool _isLoading = false;
  bool _isAuthenticated = false;
  bool _hasFamily = false;
  bool _familyResolved = false;
  String? _error;
  String? _displayName;
  String? _email;

  bool get isLoading => _isLoading;
  bool get isAuthenticated => _isAuthenticated;
  bool get hasFamily => _hasFamily;
  bool get familyResolved => _familyResolved;

  String? get error => _error;
  String? get displayName => _displayName;
  String? get email => _email;

  Future<void> markFamilyResolved(int? familyId) async {
    if (familyId == null) {
      await _tokenStorage.clearFamilyId();
      _hasFamily = false;
    } else {
      await _tokenStorage.saveFamilyId(familyId);
      _hasFamily = true;
    }
    _familyResolved = true;
    notifyListeners();
  }

  void invalidateFamilyResolution() {
    if (!_familyResolved) return;
    _familyResolved = false;
    notifyListeners();
  }

  Future<void> tryAutoLogin() async {
    _setLoading(true);
    try {
      _isAuthenticated = await _tokenStorage.hasTokens();
      if (_isAuthenticated) {
        _displayName = await _tokenStorage.getDisplayName();
        _email = await _tokenStorage.getEmail();
        final cachedFamilyId = await _tokenStorage.getFamilyId();
        if (cachedFamilyId != null) {
          _hasFamily = true;
          _familyResolved = true;
        }
      }
    } finally {
      _setLoading(false);
    }
  }

  Future<void> setFamily(int? familyId) async {
    if (familyId == null) {
      await _tokenStorage.clearFamilyId();
      if (_hasFamily) {
        _hasFamily = false;
        notifyListeners();
      }
      return;
    }
    await _tokenStorage.saveFamilyId(familyId);
    if (!_hasFamily) {
      _hasFamily = true;
      notifyListeners();
    }
  }

  Future<bool> register({
    required String email,
    required String password,
    String? displayName,
  }) async {
    _setLoading(true);
    _error = null;
    try {
      final tokens = await _repository.register(
        email: email,
        password: password,
        displayName: displayName,
      );
      await _tokenStorage.saveTokens(
        userId: tokens.userId,
        accessToken: tokens.accessToken,
        refreshToken: tokens.refreshToken,
        displayName: displayName,
        email: email,
      );
      _displayName = displayName;
      _email = email;
      _isAuthenticated = true;
      _hasFamily = false;
      _familyResolved = false;
      await _tokenStorage.clearFamilyId();
      notifyListeners();
      return true;
    } on ApiException catch (e) {
      _error = e.message;
      notifyListeners();
      return false;
    } finally {
      _setLoading(false);
    }
  }

  Future<bool> login({required String email, required String password}) async {
    _setLoading(true);
    _error = null;
    try {
      final tokens = await _repository.login(email: email, password: password);
      await _tokenStorage.saveTokens(
        userId: tokens.userId,
        accessToken: tokens.accessToken,
        refreshToken: tokens.refreshToken,
        email: email,
      );
      _email = email;
      _displayName = await _tokenStorage.getDisplayName();
      _isAuthenticated = true;
      _hasFamily = false;
      _familyResolved = false;
      notifyListeners();
      return true;
    } on ApiException catch (e) {
      _error = e.message;
      notifyListeners();
      return false;
    } finally {
      _setLoading(false);
    }
  }

  Future<void> forceLogout() async {
    if (!_isAuthenticated &&
        !_hasFamily &&
        _displayName == null &&
        _email == null) {
      return;
    }
    await _tokenStorage.clearTokens();
    _isAuthenticated = false;
    _hasFamily = false;
    _displayName = null;
    _email = null;
    notifyListeners();
  }

  Future<void> logout() async {
    final refreshToken = await _tokenStorage.getRefreshToken();
    if (refreshToken != null) {
      try {
        await _repository.logout(refreshToken);
      } catch (_) {}
    }
    await _tokenStorage.clearTokens();
    _isAuthenticated = false;
    _hasFamily = false;
    _displayName = null;
    _email = null;
    notifyListeners();
  }

  void clearError() {
    _error = null;
    notifyListeners();
  }

  void _setLoading(bool value) {
    _isLoading = value;
    notifyListeners();
  }
}

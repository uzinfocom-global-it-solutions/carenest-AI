import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class TokenStorage {
  TokenStorage({FlutterSecureStorage? storage})
    : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;

  static const _accessTokenKey = 'access_token';
  static const _refreshTokenKey = 'refresh_token';
  static const _userIdKey = 'user_id';
  static const _displayNameKey = 'display_name';
  static const _emailKey = 'email';
  static const _familyIdKey = 'family_id';
  static const _childrenJsonKey = 'children_json';
  static const _activeChatIdKey = 'active_chat_id';

  Future<void> saveTokens({
    required String userId,
    required String accessToken,
    required String refreshToken,
    String? displayName,
    String? email,
  }) async {
    await Future.wait([
      _storage.write(key: _userIdKey, value: userId),
      _storage.write(key: _accessTokenKey, value: accessToken),
      _storage.write(key: _refreshTokenKey, value: refreshToken),
      if (displayName != null)
        _storage.write(key: _displayNameKey, value: displayName),
      if (email != null) _storage.write(key: _emailKey, value: email),
    ]);
  }

  Future<String?> getAccessToken() => _storage.read(key: _accessTokenKey);
  Future<String?> getRefreshToken() => _storage.read(key: _refreshTokenKey);
  Future<String?> getUserId() => _storage.read(key: _userIdKey);
  Future<String?> getDisplayName() => _storage.read(key: _displayNameKey);
  Future<String?> getEmail() => _storage.read(key: _emailKey);

  Future<void> saveFamilyId(int familyId) =>
      _storage.write(key: _familyIdKey, value: familyId.toString());

  Future<void> clearFamilyId() => _storage.delete(key: _familyIdKey);

  Future<int?> getFamilyId() async {
    final val = await _storage.read(key: _familyIdKey);
    return val != null ? int.tryParse(val) : null;
  }

  Future<void> saveChildrenJson(String json) =>
      _storage.write(key: _childrenJsonKey, value: json);

  Future<String?> getChildrenJson() => _storage.read(key: _childrenJsonKey);

  Future<void> saveActiveChatId(int chatId) =>
      _storage.write(key: _activeChatIdKey, value: chatId.toString());

  Future<int?> getActiveChatId() async {
    final val = await _storage.read(key: _activeChatIdKey);
    return val != null ? int.tryParse(val) : null;
  }

  Future<bool> hasTokens() async {
    final token = await _storage.read(key: _refreshTokenKey);
    return token != null;
  }

  Future<void> clearTokens() async {
    await Future.wait([
      _storage.delete(key: _userIdKey),
      _storage.delete(key: _accessTokenKey),
      _storage.delete(key: _refreshTokenKey),
      _storage.delete(key: _displayNameKey),
      _storage.delete(key: _emailKey),
      _storage.delete(key: _familyIdKey),
      _storage.delete(key: _childrenJsonKey),
      _storage.delete(key: _activeChatIdKey),
    ]);
  }
}

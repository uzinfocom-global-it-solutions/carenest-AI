import 'package:app/core/constants/api_constants.dart';
import 'package:app/shared/api/api_client.dart';
import 'package:app/features/auth/domain/auth_models.dart';
import 'package:app/features/auth/domain/auth_repository.dart';

class AuthRepositoryImpl implements AuthRepository {
  AuthRepositoryImpl({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<AuthTokens> register({
    required String email,
    required String password,
    String? displayName,
    String? deviceId,
  }) async {
    final json = await _apiClient.post(ApiConstants.register, {
      'email': email,
      'password': password,
      'displayName': displayName,
      'deviceId': deviceId,
    });
    return AuthTokens.fromJson(json);
  }

  @override
  Future<AuthTokens> login({
    required String email,
    required String password,
    String? deviceId,
  }) async {
    final json = await _apiClient.post(ApiConstants.login, {
      'email': email,
      'password': password,
      'deviceId': deviceId,
    });
    return AuthTokens.fromJson(json);
  }

  @override
  Future<AuthTokens> refresh(String refreshToken) async {
    final json = await _apiClient.post(ApiConstants.refresh, {
      'refreshToken': refreshToken,
    });
    return AuthTokens.fromJson(json);
  }

  @override
  Future<void> logout(String refreshToken) async {
    await _apiClient.post(ApiConstants.logout, {'refreshToken': refreshToken});
  }
}

import 'auth_models.dart';

abstract class AuthRepository {
  Future<AuthTokens> register({
    required String email,
    required String password,
    String? displayName,
    String? deviceId,
  });

  Future<AuthTokens> login({
    required String email,
    required String password,
    String? deviceId,
  });

  Future<AuthTokens> refresh(String refreshToken);

  Future<void> logout(String refreshToken);
}

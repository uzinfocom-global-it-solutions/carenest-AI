class AuthTokens {
  const AuthTokens({
    required this.userId,
    required this.accessToken,
    required this.refreshToken,
  });

  final String userId;
  final String accessToken;
  final String refreshToken;

  factory AuthTokens.fromJson(Map<String, dynamic> json) => AuthTokens(
    userId: json['userId'] as String,
    accessToken: json['accessToken'] as String,
    refreshToken: json['refreshToken'] as String,
  );
}

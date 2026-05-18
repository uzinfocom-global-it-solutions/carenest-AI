import 'dart:async';
import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:app/core/constants/api_constants.dart';

class ApiException implements Exception {
  const ApiException(this.message, this.statusCode);
  final String message;
  final int statusCode;

  @override
  String toString() => 'ApiException($statusCode): $message';
}

/// Pluggable refresh hook. Returns the new access token on success, or null
/// if the refresh failed (caller will then trigger logout). Wired up by the
/// auth layer so ApiClient stays free of auth-state knowledge.
typedef RefreshTokenFn = Future<String?> Function();

class ApiClient {
  ApiClient({
    http.Client? client,
    this.tokenProvider,
    this.refreshToken,
    this.onUnauthorized,
  }) : _client = client ?? http.Client();

  final http.Client _client;
  final Future<String?> Function()? tokenProvider;

  /// Called when a request returns 401 — if it returns a new access token,
  /// the original request is retried once with the new token. If it returns
  /// null, [onUnauthorized] fires and the request fails with 401.
  final RefreshTokenFn? refreshToken;

  /// Invoked once when refresh fails or no refresh hook is wired. Used by
  /// the auth layer to clear secure storage and bounce to /login.
  final Future<void> Function()? onUnauthorized;

  Future<Map<String, String>> _getHeaders() async {
    final headers = <String, String>{'Content-Type': 'application/json'};
    if (tokenProvider != null) {
      final token = await tokenProvider!();
      if (token != null) {
        headers['Authorization'] = 'Bearer $token';
      }
    }
    return headers;
  }

  static const _defaultTimeout = Duration(seconds: 15);
  static const aiTimeout = Duration(seconds: 60);

  Future<dynamic> get(String path) async {
    return _safeRequest(
      () async {
        final headers = await _getHeaders();
        return _client
            .get(Uri.parse('${ApiConstants.baseUrl}$path'), headers: headers)
            .timeout(_defaultTimeout);
      },
      path,
    );
  }

  Future<dynamic> post(String path,
      [Map<String, dynamic>? body, Duration? timeout]) async {
    final t = timeout ?? _defaultTimeout;
    return _safeRequest(
      () async {
        final headers = await _getHeaders();
        return _client
            .post(Uri.parse('${ApiConstants.baseUrl}$path'),
                headers: headers,
                body: body != null ? jsonEncode(body) : null)
            .timeout(t);
      },
      path,
    );
  }

  Future<dynamic> put(String path, [Map<String, dynamic>? body]) async {
    return _safeRequest(
      () async {
        final headers = await _getHeaders();
        return _client
            .put(Uri.parse('${ApiConstants.baseUrl}$path'),
                headers: headers,
                body: body != null ? jsonEncode(body) : null)
            .timeout(_defaultTimeout);
      },
      path,
    );
  }

  Future<dynamic> delete(String path) async {
    return _safeRequest(
      () async {
        final headers = await _getHeaders();
        return _client
            .delete(Uri.parse('${ApiConstants.baseUrl}$path'), headers: headers)
            .timeout(_defaultTimeout);
      },
      path,
    );
  }

  static bool _isAuthPath(String path) =>
      path == ApiConstants.login ||
      path == ApiConstants.register ||
      path == ApiConstants.refresh;

  /// Coalesce concurrent refreshes — if 5 requests all hit 401 at once, we
  /// only want to call /auth/refresh once and have everyone wait on the same
  /// future. Otherwise the second refresh sees a rotated (now-invalid) token.
  Future<String?>? _inflightRefresh;

  Future<dynamic> _safeRequest(
      Future<http.Response> Function() fn, String path) async {
    try {
      final response = await fn();

      // Try a single refresh + retry on auth failure (skip auth endpoints
      // themselves, since refreshing on a bad password would loop).
      if (response.statusCode == 401 &&
          !_isAuthPath(path) &&
          refreshToken != null) {
        final newToken = await _refreshOnce();
        if (newToken != null) {
          final retry = await fn();
          return _handleResponse(retry, path);
        }
      }

      return _handleResponse(response, path);
    } on TimeoutException {
      throw const ApiException(
          'Сервер не отвечает. Проверьте подключение.', 0);
    }
  }

  Future<String?> _refreshOnce() {
    return _inflightRefresh ??= () async {
      try {
        return await refreshToken!.call();
      } finally {
        _inflightRefresh = null;
      }
    }();
  }

  dynamic _handleResponse(http.Response response, String path) {
    if (response.statusCode == 401 && !_isAuthPath(path)) {
      // Fire-and-forget — caller will get a thrown exception anyway, but the
      // background task scrubs the bad token so the next render bounces to /login.
      onUnauthorized?.call();
    }

    if (response.body.isEmpty) {
      if (response.statusCode >= 200 && response.statusCode < 300) {
        return null;
      }
      throw ApiException(
        'Request failed with status ${response.statusCode}',
        response.statusCode,
      );
    }

    if (response.statusCode >= 200 && response.statusCode < 300) {
      try {
        return jsonDecode(response.body);
      } catch (_) {
        return response.body;
      }
    }

    try {
      final json = jsonDecode(response.body);
      String detail = 'Request failed';
      if (json is Map<String, dynamic>) {
        detail = json['detail'] as String? ?? json['title'] as String? ?? detail;
      }
      throw ApiException(detail, response.statusCode);
    } catch (_) {
      throw ApiException('Server error: ${response.statusCode} (HTML response)', response.statusCode);
    }
  }
}

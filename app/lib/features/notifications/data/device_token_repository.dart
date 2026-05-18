import '../../../shared/api/api_client.dart';

class DeviceTokenRepository {
  DeviceTokenRepository(this._api);

  final ApiClient _api;

  Future<void> register({
    required String token,
    required String platform,
    String? deviceName,
  }) async {
    await _api.post('/api/v1/devices/register', {
      'token': token,
      'platform': platform,
      'deviceName': deviceName,
    });
  }

  Future<void> unregister(String token) async {
    await _api.post('/api/v1/devices/unregister', {'token': token});
  }

  Future<List<Map<String, dynamic>>> listMyTokens() async {
    final result = await _api.get('/api/v1/devices');
    return List<Map<String, dynamic>>.from(result as List);
  }
}

import '../../../shared/api/api_client.dart';
import '../domain/health_monitoring_session.dart';

class MonitoringService {
  final ApiClient _api;
  const MonitoringService({required ApiClient api}) : _api = api;

  Future<List<HealthMonitoringSession>> getActiveSessions(int familyId) async {
    final response = await _api.get('/api/v1/families/$familyId/monitoring/sessions');
    final list = response as List<dynamic>;
    return list
        .map((e) => HealthMonitoringSession.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<List<AiDecisionLog>> getRecentDecisions(int familyId) async {
    final response = await _api.get('/api/v1/debug/ai/decisions?familyId=$familyId&limit=50');
    final list = response as List<dynamic>;
    return list
        .map((e) => AiDecisionLog.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<bool> startTestMode(int intervalSeconds) async {
    await _api.post('/api/v1/debug/ai/test-mode/start',
        {'intervalSeconds': intervalSeconds});
    return true;
  }

  Future<bool> stopTestMode() async {
    await _api.post('/api/v1/debug/ai/test-mode/stop', {});
    return true;
  }

  Future<Map<String, dynamic>?> getTestModeStatus() async {
    final response = await _api.get('/api/v1/debug/ai/test-mode/status');
    return response as Map<String, dynamic>?;
  }
}

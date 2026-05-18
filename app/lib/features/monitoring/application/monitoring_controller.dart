import 'package:flutter/foundation.dart';
import '../domain/health_monitoring_session.dart';
import '../data/monitoring_service.dart';

class MonitoringController extends ChangeNotifier {
  final MonitoringService _service;

  MonitoringController({required MonitoringService service}) : _service = service;

  List<HealthMonitoringSession> _sessions = const [];
  List<AiDecisionLog> _recentDecisions = const [];
  bool _loading = false;
  String? _error;

  bool _testModeEnabled = false;
  bool _testModeLoading = false;
  int _testModeInterval = 5;
  double _testModeEventsPerMin = 0;
  String? _testModeError;

  int _loadVersion = 0;

  List<HealthMonitoringSession> get sessions => _sessions;
  List<AiDecisionLog> get recentDecisions => _recentDecisions;
  bool get loading => _loading;
  String? get error => _error;
  bool get testModeEnabled => _testModeEnabled;
  bool get testModeLoading => _testModeLoading;
  int get testModeInterval => _testModeInterval;
  double get testModeEventsPerMin => _testModeEventsPerMin;
  String? get testModeError => _testModeError;
  bool get hasActiveSessions => _sessions.isNotEmpty;

  HealthMonitoringSession? get highestRiskSession =>
      _sessions.isEmpty ? null : _sessions.reduce((a, b) => a.riskScore >= b.riskScore ? a : b);

  bool get hasEmergency =>
      _sessions.any((s) => s.severity == MonitoringSeverity.emergency || s.severity == MonitoringSeverity.critical);

  bool get hasPredictedEscalation =>
      _sessions.any((s) => s.predictedEscalationAt != null && !s.isResolved);

  int get maxPredictedRisk => _sessions.isEmpty
      ? 0
      : _sessions
          .map((s) => s.predictedRiskScore ?? s.riskScore)
          .reduce((a, b) => a > b ? a : b);

  Future<void> loadForFamily(int familyId) async {
    final v = ++_loadVersion;
    _loading = true;
    _error = null;
    notifyListeners();
    debugPrint('[Monitoring] Load started (v$v) family=$familyId');
    try {
      final result = await _service.getActiveSessions(familyId);
      if (v != _loadVersion) {
        debugPrint('[Monitoring] Stale response discarded (v$v, current v$_loadVersion)');
        return;
      }
      _sessions = List.unmodifiable(
        result..sort((a, b) => b.riskScore.compareTo(a.riskScore)),
      );
      debugPrint('[Monitoring] State updated (v$v): ${_sessions.length} sessions');
    } catch (e) {
      if (v != _loadVersion) return;
      _error = e.toString();
      debugPrint('[Monitoring] Load error (v$v): $e');
    } finally {
      if (v == _loadVersion) {
        _loading = false;
        notifyListeners();
        debugPrint('[Monitoring] notifyListeners (v$v)');
      }
    }
  }

  Future<void> loadDecisions(int familyId) async {
    try {
      _recentDecisions = List.unmodifiable(
        await _service.getRecentDecisions(familyId),
      );
      notifyListeners();
    } catch (_) {}
  }

  Future<void> refreshTestModeStatus() async {
    try {
      final data = await _service.getTestModeStatus();
      if (data == null) return;
      _testModeEnabled = data['isEnabled'] as bool? ?? false;
      _testModeInterval = data['intervalSeconds'] as int? ?? 5;
      _testModeEventsPerMin = (data['notificationsPerMinute'] as num?)?.toDouble() ?? 0;
      notifyListeners();
    } catch (_) {}
  }

  Future<bool> startTestMode(int intervalSeconds) async {
    _testModeLoading = true;
    _testModeError = null;
    notifyListeners();
    try {
      final ok = await _service.startTestMode(intervalSeconds);
      if (ok) {
        _testModeEnabled = true;
        _testModeInterval = intervalSeconds;
      }
      return ok;
    } catch (e) {
      _testModeError = e.toString();
      return false;
    } finally {
      _testModeLoading = false;
      notifyListeners();
    }
  }

  Future<bool> stopTestMode() async {
    _testModeLoading = true;
    notifyListeners();
    try {
      final ok = await _service.stopTestMode();
      if (ok) {
        _testModeEnabled = false;
        _testModeEventsPerMin = 0;
      }
      return ok;
    } catch (_) {
      return false;
    } finally {
      _testModeLoading = false;
      notifyListeners();
    }
  }

  void onSseEvent(String eventType, Map<String, dynamic> data) {
    debugPrint('[Monitoring] SSE event: $eventType');
    switch (eventType) {
      case 'monitoring_session_started':
      case 'monitoring_session_updated':
        final sessionData = data['session'] as Map<String, dynamic>?;
        if (sessionData != null) _upsertSession(HealthMonitoringSession.fromJson(sessionData));

      case 'monitoring_escalated':
        final sessionData = data['session'] as Map<String, dynamic>?;
        if (sessionData != null) _upsertSession(HealthMonitoringSession.fromJson(sessionData));

      case 'risk_score_updated':
        final sessionId = data['sessionId'] as int?;
        final score = data['predictedRiskScore'] as int?;
        final escalationAtRaw = data['predictedEscalationAt'] as String?;
        if (sessionId != null) {
          final idx = _sessions.indexWhere((s) => s.id == sessionId);
          if (idx >= 0 && score != null) {
            final s = _sessions[idx];
            final updated = HealthMonitoringSession(
              id: s.id,
              familyId: s.familyId,
              childId: s.childId,
              childName: s.childName,
              issueType: s.issueType,
              severity: s.severity,
              status: s.status,
              lifecyclePhase: s.lifecyclePhase,
              riskScore: s.riskScore,
              predictedRiskScore: score,
              predictedEscalationAt: escalationAtRaw != null
                  ? DateTime.parse(escalationAtRaw)
                  : s.predictedEscalationAt,
              monitoringChainId: s.monitoringChainId,
              startedAt: s.startedAt,
              lastUpdatedAt: s.lastUpdatedAt,
              nextFollowUpAt: s.nextFollowUpAt,
              initialSymptomDescription: s.initialSymptomDescription,
              followUpCount: s.followUpCount,
              missedFollowUps: s.missedFollowUps,
              isResolved: s.isResolved,
            );
            final mutable = List<HealthMonitoringSession>.from(_sessions);
            mutable[idx] = updated;
            _sessions = List.unmodifiable(mutable);
            notifyListeners();
          }
        }

      case 'predictive_risk_detected':
        final sessionData = data['session'] as Map<String, dynamic>?;
        if (sessionData != null) _upsertSession(HealthMonitoringSession.fromJson(sessionData));

      case 'monitoring_plan_updated':
        final sessionData = data['session'] as Map<String, dynamic>?;
        if (sessionData != null) _upsertSession(HealthMonitoringSession.fromJson(sessionData));

      case 'followup_chain_updated':
        final sessionData = data['session'] as Map<String, dynamic>?;
        if (sessionData != null) _upsertSession(HealthMonitoringSession.fromJson(sessionData));

      case 'ai_decision_created':
        final decisionData = data['decision'] as Map<String, dynamic>?;
        if (decisionData != null) {
          final decision = AiDecisionLog.fromJson(decisionData);
          _recentDecisions = List.unmodifiable(
            [decision, ..._recentDecisions.take(49)],
          );
          notifyListeners();
        }

      case 'risk_escalated':
      case 'followup_escalated':
        final sessionId = data['sessionId'] as int?;
        if (sessionId != null) {
          final idx = _sessions.indexWhere((s) => s.id == sessionId);
          if (idx >= 0) loadForFamily(_sessions[idx].familyId);
        }

      case 'family_context_updated':
        final activeCount = data['activeSessionCount'] as int? ?? 0;
        if (activeCount == 0 && _sessions.isNotEmpty) {
          _sessions = List.unmodifiable(
            _sessions.where((s) => !s.isResolved).toList(),
          );
          notifyListeners();
        }
    }
  }

  void _upsertSession(HealthMonitoringSession updated) {
    final mutable = List<HealthMonitoringSession>.from(_sessions);
    final idx = mutable.indexWhere((s) => s.id == updated.id);
    if (idx >= 0) {
      mutable[idx] = updated;
    } else {
      mutable.insert(0, updated);
    }
    mutable.sort((a, b) => b.riskScore.compareTo(a.riskScore));
    _sessions = List.unmodifiable(mutable);
    notifyListeners();
  }

  void clear() {
    _loadVersion++;
    _sessions = const [];
    _recentDecisions = const [];
    _error = null;
    _loading = false;
    notifyListeners();
  }
}

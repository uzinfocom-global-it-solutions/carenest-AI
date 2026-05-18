enum MonitoringIssueType {
  fever,
  cough,
  asthma,
  allergy,
  medicationTracking,
  hydration,
  sleepMonitoring,
  stressMonitoring,
  respiratoryMonitoring,
  vomiting,
  rash,
  unknown,
}

enum MonitoringSeverity { low, medium, high, critical, emergency }

enum MonitoringStatus { active, monitoring, escalated, resolved, abandoned }

enum MonitoringLifecyclePhase {
  detected,
  monitoring,
  escalated,
  critical,
  stabilizing,
  resolved,
  archived,
}

class AiDecisionLog {
  final int id;
  final int familyId;
  final String decisionType;
  final String message;
  final String priority;
  final int? targetChildId;
  final int? relatedSessionId;
  final String? decisionChainId;
  final bool executed;
  final String? suppressedBy;
  final String? escalationRationale;
  final DateTime createdAt;

  const AiDecisionLog({
    required this.id,
    required this.familyId,
    required this.decisionType,
    required this.message,
    required this.priority,
    this.targetChildId,
    this.relatedSessionId,
    this.decisionChainId,
    required this.executed,
    this.suppressedBy,
    this.escalationRationale,
    required this.createdAt,
  });

  bool get isSuppressed => suppressedBy != null;

  factory AiDecisionLog.fromJson(Map<String, dynamic> json) {
    return AiDecisionLog(
      id: json['id'] as int,
      familyId: json['familyId'] as int,
      decisionType: json['decisionType'] as String? ?? '',
      message: json['message'] as String? ?? '',
      priority: json['priority'] as String? ?? 'Normal',
      targetChildId: json['targetChildId'] as int?,
      relatedSessionId: json['relatedSessionId'] as int?,
      decisionChainId: json['decisionChainId'] as String?,
      executed: json['executed'] as bool? ?? true,
      suppressedBy: json['suppressedBy'] as String?,
      escalationRationale: json['escalationRationale'] as String?,
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }
}

class HealthMonitoringSession {
  final int id;
  final int familyId;
  final int? childId;
  final String? childName;
  final MonitoringIssueType issueType;
  final MonitoringSeverity severity;
  final MonitoringStatus status;
  final MonitoringLifecyclePhase lifecyclePhase;
  final int riskScore;
  final int? predictedRiskScore;
  final DateTime? predictedEscalationAt;
  final String? monitoringChainId;
  final DateTime startedAt;
  final DateTime lastUpdatedAt;
  final DateTime? nextFollowUpAt;
  final String? initialSymptomDescription;
  final int followUpCount;
  final int missedFollowUps;
  final bool isResolved;

  const HealthMonitoringSession({
    required this.id,
    required this.familyId,
    this.childId,
    this.childName,
    required this.issueType,
    required this.severity,
    required this.status,
    this.lifecyclePhase = MonitoringLifecyclePhase.detected,
    required this.riskScore,
    this.predictedRiskScore,
    this.predictedEscalationAt,
    this.monitoringChainId,
    required this.startedAt,
    required this.lastUpdatedAt,
    this.nextFollowUpAt,
    this.initialSymptomDescription,
    required this.followUpCount,
    required this.missedFollowUps,
    required this.isResolved,
  });

  factory HealthMonitoringSession.fromJson(Map<String, dynamic> json) {
    return HealthMonitoringSession(
      id: json['id'] as int,
      familyId: json['familyId'] as int,
      childId: json['childId'] as int?,
      childName: json['childName'] as String?,
      issueType: _parseIssueType(json['issueType'] as String? ?? 'unknown'),
      severity: _parseSeverity(json['severity'] as String? ?? 'low'),
      status: _parseStatus(json['status'] as String? ?? 'active'),
      lifecyclePhase: _parseLifecyclePhase(json['lifecyclePhase'] as String? ?? 'Detected'),
      riskScore: json['riskScore'] as int? ?? 0,
      predictedRiskScore: json['predictedRiskScore'] as int?,
      predictedEscalationAt: json['predictedEscalationAt'] != null
          ? DateTime.parse(json['predictedEscalationAt'] as String)
          : null,
      monitoringChainId: json['monitoringChainId'] as String?,
      startedAt: DateTime.parse(json['startedAt'] as String),
      lastUpdatedAt: DateTime.parse(json['lastUpdatedAt'] as String),
      nextFollowUpAt: json['nextFollowUpAt'] != null
          ? DateTime.parse(json['nextFollowUpAt'] as String)
          : null,
      initialSymptomDescription: json['initialSymptomDescription'] as String?,
      followUpCount: json['followUpCount'] as int? ?? 0,
      missedFollowUps: json['missedFollowUps'] as int? ?? 0,
      isResolved: json['isResolved'] as bool? ?? false,
    );
  }

  static MonitoringIssueType _parseIssueType(String s) =>
      MonitoringIssueType.values.firstWhere(
        (e) => e.name.toLowerCase() == s.toLowerCase().replaceAll('_', ''),
        orElse: () => MonitoringIssueType.unknown,
      );

  static MonitoringSeverity _parseSeverity(String s) =>
      MonitoringSeverity.values.firstWhere(
        (e) => e.name.toLowerCase() == s.toLowerCase(),
        orElse: () => MonitoringSeverity.low,
      );

  static MonitoringStatus _parseStatus(String s) =>
      MonitoringStatus.values.firstWhere(
        (e) => e.name.toLowerCase() == s.toLowerCase(),
        orElse: () => MonitoringStatus.active,
      );

  static MonitoringLifecyclePhase _parseLifecyclePhase(String s) =>
      MonitoringLifecyclePhase.values.firstWhere(
        (e) => e.name.toLowerCase() == s.toLowerCase(),
        orElse: () => MonitoringLifecyclePhase.detected,
      );

  String get issueLabel => switch (issueType) {
        MonitoringIssueType.fever => 'Температура',
        MonitoringIssueType.cough => 'Кашель',
        MonitoringIssueType.asthma => 'Астма',
        MonitoringIssueType.allergy => 'Аллергия',
        MonitoringIssueType.vomiting => 'Рвота',
        MonitoringIssueType.rash => 'Сыпь',
        MonitoringIssueType.hydration => 'Гидратация',
        MonitoringIssueType.sleepMonitoring => 'Сон',
        MonitoringIssueType.respiratoryMonitoring => 'Дыхание',
        MonitoringIssueType.medicationTracking => 'Лекарства',
        MonitoringIssueType.stressMonitoring => 'Стресс',
        MonitoringIssueType.unknown => 'Состояние',
      };
}

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../shared/theme/app_colors.dart';
import '../application/monitoring_controller.dart';
import '../domain/health_monitoring_session.dart';

class EscalationTimelineScreen extends StatelessWidget {
  final int? sessionId;
  const EscalationTimelineScreen({super.key, this.sessionId});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Хронология эскалаций'),
        backgroundColor: AppColors.background,
        elevation: 0,
        foregroundColor: AppColors.textPrimary,
      ),
      body: Consumer<MonitoringController>(
        builder: (context, ctrl, _) {
          final sessions = sessionId != null
              ? ctrl.sessions.where((s) => s.id == sessionId).toList()
              : ctrl.sessions;

          if (sessions.isEmpty) {
            return const Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(Icons.timeline, size: 56, color: AppColors.textQuaternary),
                  SizedBox(height: 16),
                  Text('Нет данных для отображения',
                      style: TextStyle(
                          color: AppColors.textSecondary, fontSize: 16)),
                ],
              ),
            );
          }

          final decisions = ctrl.recentDecisions;

          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              ...sessions.map((s) => _SessionTimeline(
                    session: s,
                    decisions: decisions
                        .where((d) => d.relatedSessionId == s.id)
                        .toList(),
                  )),
            ],
          );
        },
      ),
    );
  }
}

class _SessionTimeline extends StatelessWidget {
  final HealthMonitoringSession session;
  final List<AiDecisionLog> decisions;

  const _SessionTimeline({required this.session, required this.decisions});

  @override
  Widget build(BuildContext context) {
    final severityColor = _severityColor(session.severity);

    final events = <_TimelineEvent>[
      _TimelineEvent(
        at: session.startedAt,
        label: 'Начало мониторинга',
        sublabel: session.initialSymptomDescription,
        icon: Icons.play_circle_outline,
        color: AppColors.blue,
      ),
      ...decisions.map((d) => _TimelineEvent(
            at: d.createdAt,
            label: _decisionLabel(d.decisionType),
            sublabel: d.message,
            icon: d.isSuppressed ? Icons.block : _decisionIcon(d.decisionType),
            color: d.isSuppressed
                ? AppColors.textQuaternary
                : _priorityColor(d.priority),
            chainId: d.decisionChainId,
            suppressed: d.isSuppressed,
            escalationRationale: d.escalationRationale,
          )),
      if (session.isResolved)
        _TimelineEvent(
          at: session.lastUpdatedAt,
          label: 'Сессия завершена',
          sublabel: null,
          icon: Icons.check_circle_outline,
          color: AppColors.green,
        ),
    ]..sort((a, b) => a.at.compareTo(b.at));

    return Container(
      margin: const EdgeInsets.only(bottom: 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
            decoration: BoxDecoration(
              color: severityColor.withValues(alpha: 0.08),
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: severityColor.withValues(alpha: 0.25)),
            ),
            child: Row(
              children: [
                Icon(_issueIcon(session.issueType),
                    color: severityColor, size: 18),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '${session.issueLabel}${session.childName != null ? ' · ${session.childName}' : ''}',
                        style: TextStyle(
                            color: severityColor,
                            fontWeight: FontWeight.w700,
                            fontSize: 14),
                      ),
                      Text(
                        '${_lifecycleLabel(session.lifecyclePhase)} · Риск ${session.riskScore}${session.predictedRiskScore != null ? ' → ${session.predictedRiskScore}' : ''}',
                        style: const TextStyle(
                            color: AppColors.textTertiary, fontSize: 12),
                      ),
                    ],
                  ),
                ),
                Text('${events.length} событий',
                    style: const TextStyle(
                        color: AppColors.textQuaternary, fontSize: 11)),
              ],
            ),
          ),
          const SizedBox(height: 12),
          ...events.asMap().entries.map((entry) {
            final isLast = entry.key == events.length - 1;
            return _TimelineRow(event: entry.value, isLast: isLast);
          }),
        ],
      ),
    );
  }

  static Color _severityColor(MonitoringSeverity s) => switch (s) {
        MonitoringSeverity.low => AppColors.green,
        MonitoringSeverity.medium => AppColors.amber,
        MonitoringSeverity.high => const Color(0xFFE07900),
        MonitoringSeverity.critical => AppColors.red,
        MonitoringSeverity.emergency => const Color(0xFF8B0000),
      };

  static Color _priorityColor(String p) => switch (p.toLowerCase()) {
        'emergency' => const Color(0xFF8B0000),
        'high' => AppColors.red,
        'normal' => AppColors.blue,
        _ => AppColors.textTertiary,
      };

  static String _decisionLabel(String type) => switch (type) {
        'EscalationAlert' => 'Эскалация',
        'HealthAlert' => 'Предупреждение о здоровье',
        'PredictiveRiskAlert' => 'Прогноз риска',
        'FollowUpCheck' => 'Проверочный вопрос',
        'MonitoringResolved' => 'Автоматическое завершение',
        'ResponseAnalyzed' => 'Анализ ответа',
        'EnvironmentalAlert' => 'Экологическая угроза',
        'MedicationReminder' => 'Напоминание о лекарстве',
        _ => type,
      };

  static IconData _decisionIcon(String type) => switch (type) {
        'EscalationAlert' => Icons.warning_amber_rounded,
        'HealthAlert' => Icons.monitor_heart,
        'PredictiveRiskAlert' => Icons.trending_up,
        'FollowUpCheck' => Icons.question_answer_outlined,
        'MonitoringResolved' => Icons.check_circle_outline,
        'ResponseAnalyzed' => Icons.analytics_outlined,
        'EnvironmentalAlert' => Icons.cloud_outlined,
        'MedicationReminder' => Icons.medication_outlined,
        _ => Icons.smart_toy_outlined,
      };

  static String _lifecycleLabel(MonitoringLifecyclePhase p) => switch (p) {
        MonitoringLifecyclePhase.detected => 'Обнаружено',
        MonitoringLifecyclePhase.monitoring => 'Мониторинг',
        MonitoringLifecyclePhase.escalated => 'Эскалация',
        MonitoringLifecyclePhase.critical => 'Критично',
        MonitoringLifecyclePhase.stabilizing => 'Улучшение',
        MonitoringLifecyclePhase.resolved => 'Завершено',
        MonitoringLifecyclePhase.archived => 'Архив',
      };

  static IconData _issueIcon(MonitoringIssueType t) => switch (t) {
        MonitoringIssueType.fever => Icons.thermostat,
        MonitoringIssueType.cough => Icons.air,
        MonitoringIssueType.asthma => Icons.air,
        MonitoringIssueType.allergy => Icons.local_florist,
        MonitoringIssueType.vomiting => Icons.sick,
        MonitoringIssueType.rash => Icons.healing,
        MonitoringIssueType.hydration => Icons.water_drop,
        MonitoringIssueType.sleepMonitoring => Icons.bedtime,
        MonitoringIssueType.respiratoryMonitoring => Icons.air,
        MonitoringIssueType.medicationTracking => Icons.medication,
        MonitoringIssueType.stressMonitoring => Icons.psychology,
        MonitoringIssueType.unknown => Icons.monitor_heart_outlined,
      };
}

class _TimelineEvent {
  final DateTime at;
  final String label;
  final String? sublabel;
  final IconData icon;
  final Color color;
  final String? chainId;
  final bool suppressed;
  final String? escalationRationale;

  const _TimelineEvent({
    required this.at,
    required this.label,
    required this.sublabel,
    required this.icon,
    required this.color,
    this.chainId,
    this.suppressed = false,
    this.escalationRationale,
  });
}

class _TimelineRow extends StatelessWidget {
  final _TimelineEvent event;
  final bool isLast;

  const _TimelineRow({required this.event, required this.isLast});

  @override
  Widget build(BuildContext context) {
    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 32,
            child: Column(
              children: [
                Container(
                  width: 28,
                  height: 28,
                  decoration: BoxDecoration(
                    color: event.color.withValues(alpha: 0.12),
                    shape: BoxShape.circle,
                    border: Border.all(
                        color: event.color.withValues(alpha: 0.4), width: 1.5),
                  ),
                  child: Icon(event.icon, size: 14, color: event.color),
                ),
                if (!isLast)
                  Expanded(
                    child: Container(
                      width: 1.5,
                      color: AppColors.border,
                      margin: const EdgeInsets.symmetric(vertical: 2),
                    ),
                  ),
              ],
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Padding(
              padding: EdgeInsets.only(bottom: isLast ? 0 : 16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(event.label,
                            style: TextStyle(
                                color: event.suppressed
                                    ? AppColors.textTertiary
                                    : AppColors.textPrimary,
                                fontWeight: FontWeight.w600,
                                fontSize: 13)),
                      ),
                      Text(_formatTime(event.at),
                          style: const TextStyle(
                              color: AppColors.textQuaternary,
                              fontSize: 11)),
                    ],
                  ),
                  if (event.sublabel != null) ...[
                    const SizedBox(height: 3),
                    Text(event.sublabel!,
                        style: TextStyle(
                            color: event.suppressed
                                ? AppColors.textQuaternary
                                : AppColors.textTertiary,
                            fontSize: 12),
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis),
                  ],
                  if (event.escalationRationale != null) ...[
                    const SizedBox(height: 3),
                    Text(event.escalationRationale!,
                        style: const TextStyle(
                            color: AppColors.red, fontSize: 11),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis),
                  ],
                  if (event.chainId != null) ...[
                    const SizedBox(height: 2),
                    Text(event.chainId!,
                        style: const TextStyle(
                            color: AppColors.textQuaternary,
                            fontSize: 9,
                            fontFamily: 'monospace')),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  static String _formatTime(DateTime dt) {
    final now = DateTime.now();
    final diff = now.difference(dt);
    if (diff.inMinutes < 1) return 'только что';
    if (diff.inMinutes < 60) return '${diff.inMinutes} мин';
    if (diff.inHours < 24) {
      return '${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';
    }
    return '${dt.day}.${dt.month.toString().padLeft(2, '0')} ${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';
  }
}

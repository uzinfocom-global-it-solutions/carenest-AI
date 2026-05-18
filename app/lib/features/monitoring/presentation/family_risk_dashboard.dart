import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import '../../../shared/theme/app_colors.dart';
import '../application/monitoring_controller.dart';
import '../domain/health_monitoring_session.dart';

class FamilyRiskDashboard extends StatefulWidget {
  const FamilyRiskDashboard({super.key});

  @override
  State<FamilyRiskDashboard> createState() => _FamilyRiskDashboardState();
}

class _FamilyRiskDashboardState extends State<FamilyRiskDashboard>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs;

  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: 2, vsync: this);
  }

  @override
  void dispose() {
    _tabs.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Панель рисков'),
        backgroundColor: AppColors.background,
        elevation: 0,
        foregroundColor: AppColors.textPrimary,
        actions: [
          IconButton(
            icon: const Icon(Icons.timeline_outlined),
            tooltip: 'Эскалации',
            onPressed: () => context.push('/monitoring/escalations'),
          ),
        ],
        bottom: TabBar(
          controller: _tabs,
          labelColor: AppColors.primary,
          unselectedLabelColor: AppColors.textTertiary,
          indicatorColor: AppColors.primary,
          tabs: const [
            Tab(text: 'Сессии'),
            Tab(text: 'Решения ИИ'),
          ],
        ),
      ),
      body: Consumer<MonitoringController>(
        builder: (context, ctrl, _) {
          return TabBarView(
            controller: _tabs,
            children: [
              _SessionsTab(ctrl: ctrl),
              _DecisionsTab(ctrl: ctrl),
            ],
          );
        },
      ),
    );
  }
}

// ── Sessions tab ─────────────────────────────────────────────────────────────

class _SessionsTab extends StatelessWidget {
  final MonitoringController ctrl;
  const _SessionsTab({required this.ctrl});

  @override
  Widget build(BuildContext context) {
    if (ctrl.loading) return const Center(child: CircularProgressIndicator());

    if (ctrl.sessions.isEmpty) {
      return const Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.shield_outlined, size: 56, color: AppColors.textQuaternary),
            SizedBox(height: 16),
            Text('Активных рисков нет',
                style: TextStyle(color: AppColors.textSecondary, fontSize: 16)),
            SizedBox(height: 8),
            Text('ИИ непрерывно анализирует состояние семьи',
                style: TextStyle(color: AppColors.textTertiary, fontSize: 13),
                textAlign: TextAlign.center),
          ],
        ),
      );
    }

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _RiskSummaryBar(ctrl: ctrl),
        const SizedBox(height: 16),
        ...ctrl.sessions.map((s) => _SessionRiskCard(session: s)),
      ],
    );
  }
}

class _RiskSummaryBar extends StatelessWidget {
  final MonitoringController ctrl;
  const _RiskSummaryBar({required this.ctrl});

  @override
  Widget build(BuildContext context) {
    final maxRisk = ctrl.maxPredictedRisk;
    final color = _riskColor(maxRisk);

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: color.withValues(alpha: 0.25)),
      ),
      child: Row(
        children: [
          SizedBox(
            width: 52,
            height: 52,
            child: Stack(
              alignment: Alignment.center,
              children: [
                CircularProgressIndicator(
                  value: maxRisk / 100,
                  strokeWidth: 4,
                  backgroundColor: color.withValues(alpha: 0.15),
                  valueColor: AlwaysStoppedAnimation<Color>(color),
                ),
                Text('$maxRisk',
                    style: TextStyle(
                        fontSize: 13,
                        fontWeight: FontWeight.w800,
                        color: color)),
              ],
            ),
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Прогнозируемый риск',
                    style: TextStyle(
                        color: color,
                        fontWeight: FontWeight.w700,
                        fontSize: 14)),
                const SizedBox(height: 2),
                Text('${ctrl.sessions.length} активных сессий · '
                    '${ctrl.sessions.where((s) => s.predictedEscalationAt != null).length} с прогнозом эскалации',
                    style: const TextStyle(
                        color: AppColors.textTertiary, fontSize: 12)),
              ],
            ),
          ),
        ],
      ),
    );
  }

  static Color _riskColor(int score) {
    if (score >= 86) return const Color(0xFF8B0000);
    if (score >= 71) return AppColors.red;
    if (score >= 51) return const Color(0xFFE07900);
    if (score >= 31) return AppColors.amber;
    return AppColors.green;
  }
}

class _SessionRiskCard extends StatelessWidget {
  final HealthMonitoringSession session;
  const _SessionRiskCard({required this.session});

  @override
  Widget build(BuildContext context) {
    final severityColor = _severityColor(session.severity);
    final predicted = session.predictedRiskScore;
    final escalationAt = session.predictedEscalationAt;

    return GestureDetector(
      onTap: () => context.push('/monitoring/escalations', extra: session.id),
      child: Container(
        margin: const EdgeInsets.only(bottom: 12),
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: AppColors.card,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: severityColor.withValues(alpha: 0.3)),
          boxShadow: [
            BoxShadow(
                color: AppColors.ink.withValues(alpha: 0.04),
                blurRadius: 8,
                offset: const Offset(0, 2)),
          ],
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                _LifecycleChip(phase: session.lifecyclePhase),
                const Spacer(),
                _DualRiskIndicator(current: session.riskScore, predicted: predicted),
              ],
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                Icon(_issueIcon(session.issueType),
                    color: AppColors.textSecondary, size: 17),
                const SizedBox(width: 8),
                Text(session.issueLabel,
                    style: const TextStyle(
                        color: AppColors.textPrimary,
                        fontWeight: FontWeight.w600,
                        fontSize: 15)),
                if (session.childName != null) ...[
                  const SizedBox(width: 6),
                  Text('· ${session.childName}',
                      style: const TextStyle(
                          color: AppColors.textSecondary, fontSize: 13)),
                ],
              ],
            ),
            if (escalationAt != null) ...[
              const SizedBox(height: 8),
              _EscalationForecast(at: escalationAt),
            ],
            if (session.monitoringChainId != null) ...[
              const SizedBox(height: 6),
              Text('Chain: ${session.monitoringChainId}',
                  style: const TextStyle(
                      color: AppColors.textQuaternary,
                      fontSize: 10,
                      fontFamily: 'monospace')),
            ],
          ],
        ),
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

class _LifecycleChip extends StatelessWidget {
  final MonitoringLifecyclePhase phase;
  const _LifecycleChip({required this.phase});

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (phase) {
      MonitoringLifecyclePhase.detected => ('Обнаружено', AppColors.blue),
      MonitoringLifecyclePhase.monitoring => ('Мониторинг', AppColors.amber),
      MonitoringLifecyclePhase.escalated => ('Эскалация', const Color(0xFFE07900)),
      MonitoringLifecyclePhase.critical => ('Критично', AppColors.red),
      MonitoringLifecyclePhase.stabilizing => ('Улучшение', AppColors.green),
      MonitoringLifecyclePhase.resolved => ('Resolved', AppColors.green),
      MonitoringLifecyclePhase.archived => ('Архив', AppColors.textQuaternary),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(label,
          style: TextStyle(
              color: color, fontSize: 11, fontWeight: FontWeight.w600)),
    );
  }
}

class _DualRiskIndicator extends StatelessWidget {
  final int current;
  final int? predicted;
  const _DualRiskIndicator({required this.current, this.predicted});

  @override
  Widget build(BuildContext context) {
    final display = predicted ?? current;
    final color = _scoreColor(display);
    final hasPrediction = predicted != null && predicted != current;

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        if (hasPrediction) ...[
          Text('$current→',
              style: const TextStyle(
                  fontSize: 10,
                  color: AppColors.textTertiary,
                  fontWeight: FontWeight.w500)),
          const SizedBox(width: 2),
        ],
        SizedBox(
          width: 38,
          height: 38,
          child: Stack(
            alignment: Alignment.center,
            children: [
              CircularProgressIndicator(
                value: display / 100,
                strokeWidth: 3,
                backgroundColor: color.withValues(alpha: 0.15),
                valueColor: AlwaysStoppedAnimation<Color>(color),
              ),
              Text('$display',
                  style: TextStyle(
                      fontSize: 10,
                      fontWeight: FontWeight.w700,
                      color: color)),
            ],
          ),
        ),
      ],
    );
  }

  static Color _scoreColor(int s) {
    if (s >= 86) return const Color(0xFF8B0000);
    if (s >= 71) return AppColors.red;
    if (s >= 51) return const Color(0xFFE07900);
    if (s >= 31) return AppColors.amber;
    return AppColors.green;
  }
}

class _EscalationForecast extends StatelessWidget {
  final DateTime at;
  const _EscalationForecast({required this.at});

  @override
  Widget build(BuildContext context) {
    final now = DateTime.now();
    final diff = at.difference(now);
    final label = diff.isNegative
        ? 'Эскалация ожидалась ${(-diff.inMinutes)} мин назад'
        : diff.inHours > 0
            ? 'Прогноз эскалации через ${diff.inHours} ч ${diff.inMinutes % 60} мин'
            : 'Прогноз эскалации через ${diff.inMinutes} мин';

    return Row(
      children: [
        const Icon(Icons.trending_up, size: 13, color: AppColors.red),
        const SizedBox(width: 4),
        Text(label,
            style: const TextStyle(
                fontSize: 12,
                color: AppColors.red,
                fontWeight: FontWeight.w500)),
      ],
    );
  }
}

// ── AI Decisions tab ──────────────────────────────────────────────────────────

class _DecisionsTab extends StatelessWidget {
  final MonitoringController ctrl;
  const _DecisionsTab({required this.ctrl});

  @override
  Widget build(BuildContext context) {
    if (ctrl.recentDecisions.isEmpty) {
      return const Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.psychology_outlined, size: 56, color: AppColors.textQuaternary),
            SizedBox(height: 16),
            Text('Решений ИИ нет',
                style: TextStyle(color: AppColors.textSecondary, fontSize: 16)),
            SizedBox(height: 8),
            Text('Записи появятся при следующем цикле оркестрации',
                style: TextStyle(color: AppColors.textTertiary, fontSize: 13),
                textAlign: TextAlign.center),
          ],
        ),
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: ctrl.recentDecisions.length,
      separatorBuilder: (_, _) => const SizedBox(height: 8),
      itemBuilder: (_, i) => _DecisionCard(log: ctrl.recentDecisions[i]),
    );
  }
}

class _DecisionCard extends StatelessWidget {
  final AiDecisionLog log;
  const _DecisionCard({required this.log});

  @override
  Widget build(BuildContext context) {
    final isSuppressed = log.isSuppressed;
    final priorityColor = _priorityColor(log.priority);

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: isSuppressed
            ? AppColors.surface
            : AppColors.card,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(
          color: isSuppressed
              ? AppColors.border
              : priorityColor.withValues(alpha: 0.25),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              _TypeBadge(type: log.decisionType, suppressed: isSuppressed),
              const Spacer(),
              if (!isSuppressed)
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                  decoration: BoxDecoration(
                    color: priorityColor.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(10),
                  ),
                  child: Text(log.priority,
                      style: TextStyle(
                          color: priorityColor,
                          fontSize: 10,
                          fontWeight: FontWeight.w600)),
                ),
              if (isSuppressed)
                const Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(Icons.block, size: 12, color: AppColors.textQuaternary),
                    SizedBox(width: 3),
                    Text('Подавлено',
                        style: TextStyle(
                            color: AppColors.textQuaternary,
                            fontSize: 11)),
                  ],
                ),
            ],
          ),
          const SizedBox(height: 8),
          Text(log.message,
              style: TextStyle(
                  color: isSuppressed
                      ? AppColors.textTertiary
                      : AppColors.textPrimary,
                  fontSize: 13),
              maxLines: 3,
              overflow: TextOverflow.ellipsis),
          if (log.escalationRationale != null) ...[
            const SizedBox(height: 6),
            Text(log.escalationRationale!,
                style: const TextStyle(
                    color: AppColors.red, fontSize: 12),
                maxLines: 2,
                overflow: TextOverflow.ellipsis),
          ],
          if (log.suppressedBy != null) ...[
            const SizedBox(height: 4),
            Text('Причина: ${log.suppressedBy}',
                style: const TextStyle(
                    color: AppColors.textQuaternary, fontSize: 11),
                maxLines: 1,
                overflow: TextOverflow.ellipsis),
          ],
          const SizedBox(height: 8),
          Row(
            children: [
              if (log.decisionChainId != null)
                Text(log.decisionChainId!,
                    style: const TextStyle(
                        color: AppColors.textQuaternary,
                        fontSize: 9,
                        fontFamily: 'monospace')),
              const Spacer(),
              Text(_formatTime(log.createdAt),
                  style: const TextStyle(
                      color: AppColors.textQuaternary, fontSize: 11)),
            ],
          ),
        ],
      ),
    );
  }

  static Color _priorityColor(String p) => switch (p.toLowerCase()) {
        'emergency' => const Color(0xFF8B0000),
        'high' => AppColors.red,
        'normal' => AppColors.blue,
        _ => AppColors.textTertiary,
      };

  static String _formatTime(DateTime dt) {
    final now = DateTime.now();
    final diff = now.difference(dt);
    if (diff.inMinutes < 1) return 'только что';
    if (diff.inMinutes < 60) return '${diff.inMinutes} мин назад';
    if (diff.inHours < 24) return '${diff.inHours} ч назад';
    return '${dt.day}.${dt.month.toString().padLeft(2, '0')}';
  }
}

class _TypeBadge extends StatelessWidget {
  final String type;
  final bool suppressed;
  const _TypeBadge({required this.type, required this.suppressed});

  @override
  Widget build(BuildContext context) {
    final label = switch (type) {
      'EscalationAlert' => 'Эскалация',
      'HealthAlert' => 'Здоровье',
      'PredictiveRiskAlert' => 'Прогноз',
      'FollowUpCheck' => 'Follow-up',
      'MonitoringResolved' => 'Resolved',
      'ResponseAnalyzed' => 'Ответ',
      'EnvironmentalAlert' => 'Среда',
      'MedicationReminder' => 'Лекарство',
      'GeneralAdvice' => 'Совет',
      _ => type,
    };

    return Text(label,
        style: TextStyle(
            color: suppressed ? AppColors.textQuaternary : AppColors.textSecondary,
            fontWeight: FontWeight.w600,
            fontSize: 12));
  }
}

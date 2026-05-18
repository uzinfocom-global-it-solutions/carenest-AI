import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../../shared/theme/app_colors.dart';
import '../application/monitoring_controller.dart';
import '../domain/health_monitoring_session.dart';

class ActiveMonitoringScreen extends StatelessWidget {
  const ActiveMonitoringScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Мониторинг здоровья'),
        backgroundColor: AppColors.background,
        elevation: 0,
        foregroundColor: AppColors.textPrimary,
      ),
      body: Consumer<MonitoringController>(
        builder: (context, controller, _) {
          if (controller.loading) {
            return const Center(child: CircularProgressIndicator());
          }
          if (controller.sessions.isEmpty) {
            return const _EmptyState();
          }
          return RefreshIndicator(
            onRefresh: () async {
              final session = controller.sessions.firstOrNull;
              if (session != null) await controller.loadForFamily(session.familyId);
            },
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                if (controller.hasEmergency) const _EmergencyBanner(),
                const SizedBox(height: 8),
                ...controller.sessions.map((s) => _SessionCard(session: s)),
              ],
            ),
          );
        },
      ),
    );
  }
}

class _EmergencyBanner extends StatelessWidget {
  const _EmergencyBanner();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      margin: const EdgeInsets.only(bottom: 12),
      decoration: BoxDecoration(
        color: AppColors.redSoft,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.red.withValues(alpha: 0.4)),
      ),
      child: Row(
        children: [
          const Icon(Icons.emergency, color: AppColors.red, size: 28),
          const SizedBox(width: 12),
          const Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Требуется срочная помощь',
                    style: TextStyle(
                        color: AppColors.red,
                        fontWeight: FontWeight.w700,
                        fontSize: 15)),
                SizedBox(height: 2),
                Text('Обратитесь к врачу или вызовите скорую помощь',
                    style: TextStyle(color: AppColors.red, fontSize: 13)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SessionCard extends StatelessWidget {
  final HealthMonitoringSession session;
  const _SessionCard({required this.session});

  @override
  Widget build(BuildContext context) {
    final severityColor = _severityColor(session.severity);
    final severityLabel = _severityLabel(session.severity);

    return Container(
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
            offset: const Offset(0, 2),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: severityColor.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(20),
                ),
                child: Text(severityLabel,
                    style: TextStyle(
                        color: severityColor,
                        fontSize: 12,
                        fontWeight: FontWeight.w600)),
              ),
              const Spacer(),
              _RiskScoreIndicator(score: session.riskScore),
            ],
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Icon(_issueIcon(session.issueType), color: AppColors.textSecondary, size: 18),
              const SizedBox(width: 8),
              Text(session.issueLabel,
                  style: const TextStyle(
                      color: AppColors.textPrimary,
                      fontWeight: FontWeight.w600,
                      fontSize: 16)),
              if (session.childName != null) ...[
                const SizedBox(width: 8),
                Text('· ${session.childName}',
                    style: const TextStyle(
                        color: AppColors.textSecondary, fontSize: 14)),
              ],
            ],
          ),
          if (session.initialSymptomDescription != null) ...[
            const SizedBox(height: 8),
            Text(session.initialSymptomDescription!,
                style: const TextStyle(
                    color: AppColors.textTertiary, fontSize: 13),
                maxLines: 2,
                overflow: TextOverflow.ellipsis),
          ],
          const SizedBox(height: 12),
          Row(
            children: [
              _InfoChip(
                icon: Icons.repeat,
                label: 'Follow-ups: ${session.followUpCount}',
              ),
              const SizedBox(width: 8),
              if (session.missedFollowUps > 0)
                _InfoChip(
                  icon: Icons.warning_amber_rounded,
                  label: 'Пропущено: ${session.missedFollowUps}',
                  color: AppColors.amber,
                ),
              const Spacer(),
              if (session.nextFollowUpAt != null) _NextFollowUpLabel(at: session.nextFollowUpAt!),
            ],
          ),
        ],
      ),
    );
  }

  static Color _severityColor(MonitoringSeverity s) => switch (s) {
        MonitoringSeverity.low      => AppColors.green,
        MonitoringSeverity.medium   => AppColors.amber,
        MonitoringSeverity.high     => const Color(0xFFE07900),
        MonitoringSeverity.critical => AppColors.red,
        MonitoringSeverity.emergency => const Color(0xFF8B0000),
      };

  static String _severityLabel(MonitoringSeverity s) => switch (s) {
        MonitoringSeverity.low      => 'Низкий риск',
        MonitoringSeverity.medium   => 'Средний риск',
        MonitoringSeverity.high     => 'Высокий риск',
        MonitoringSeverity.critical => 'Критический',
        MonitoringSeverity.emergency => '⚠ Экстренно',
      };

  static IconData _issueIcon(MonitoringIssueType t) => switch (t) {
        MonitoringIssueType.fever        => Icons.thermostat,
        MonitoringIssueType.cough        => Icons.air,
        MonitoringIssueType.asthma       => Icons.air,
        MonitoringIssueType.allergy      => Icons.local_florist,
        MonitoringIssueType.vomiting     => Icons.sick,
        MonitoringIssueType.rash         => Icons.healing,
        MonitoringIssueType.hydration    => Icons.water_drop,
        MonitoringIssueType.sleepMonitoring => Icons.bedtime,
        MonitoringIssueType.respiratoryMonitoring => Icons.air,
        MonitoringIssueType.medicationTracking => Icons.medication,
        MonitoringIssueType.stressMonitoring => Icons.psychology,
        MonitoringIssueType.unknown      => Icons.monitor_heart_outlined,
      };
}

class _RiskScoreIndicator extends StatelessWidget {
  final int score;
  const _RiskScoreIndicator({required this.score});

  @override
  Widget build(BuildContext context) {
    final color = score >= 86
        ? const Color(0xFF8B0000)
        : score >= 71
            ? AppColors.red
            : score >= 51
                ? const Color(0xFFE07900)
                : score >= 31
                    ? AppColors.amber
                    : AppColors.green;

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        SizedBox(
          width: 36,
          height: 36,
          child: Stack(
            alignment: Alignment.center,
            children: [
              CircularProgressIndicator(
                value: score / 100,
                strokeWidth: 3,
                backgroundColor: color.withValues(alpha: 0.15),
                valueColor: AlwaysStoppedAnimation<Color>(color),
              ),
              Text('$score',
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
}

class _InfoChip extends StatelessWidget {
  final IconData icon;
  final String label;
  final Color color;

  const _InfoChip({
    required this.icon,
    required this.label,
    this.color = AppColors.textTertiary,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 13, color: color),
        const SizedBox(width: 4),
        Text(label, style: TextStyle(fontSize: 12, color: color)),
      ],
    );
  }
}

class _NextFollowUpLabel extends StatelessWidget {
  final DateTime at;
  const _NextFollowUpLabel({required this.at});

  @override
  Widget build(BuildContext context) {
    final now = DateTime.now();
    final diff = at.difference(now);
    final label = diff.isNegative
        ? 'Ожидает ответа'
        : diff.inMinutes < 1
            ? 'Сейчас'
            : 'Через ${diff.inMinutes} мин';

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        const Icon(Icons.schedule, size: 13, color: AppColors.primary),
        const SizedBox(width: 4),
        Text(label,
            style: const TextStyle(
                fontSize: 12,
                color: AppColors.primary,
                fontWeight: FontWeight.w500)),
      ],
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState();

  @override
  Widget build(BuildContext context) {
    return const Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(Icons.monitor_heart_outlined, size: 64, color: AppColors.textQuaternary),
          SizedBox(height: 16),
          Text('Активных наблюдений нет',
              style: TextStyle(
                  color: AppColors.textSecondary,
                  fontSize: 16,
                  fontWeight: FontWeight.w500)),
          SizedBox(height: 8),
          Text('ИИ начнёт мониторинг при обнаружении симптомов',
              style: TextStyle(color: AppColors.textTertiary, fontSize: 13),
              textAlign: TextAlign.center),
        ],
      ),
    );
  }
}

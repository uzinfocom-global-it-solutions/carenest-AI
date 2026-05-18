import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import '../theme/app_colors.dart';
import '../../features/monitoring/application/monitoring_controller.dart';
import '../../features/monitoring/domain/health_monitoring_session.dart';

class HealthRiskBanner extends StatelessWidget {
  const HealthRiskBanner({super.key});

  @override
  Widget build(BuildContext context) {
    return Consumer<MonitoringController>(
      builder: (context, controller, _) {
        if (!controller.hasActiveSessions) return const SizedBox.shrink();

        final session = controller.highestRiskSession!;
        final isEmergency = controller.hasEmergency;
        final hasPrediction = controller.hasPredictedEscalation;

        return GestureDetector(
          onTap: () => context.push('/monitoring'),
          onLongPress: () => context.push('/monitoring/dashboard'),
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 300),
            margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            decoration: BoxDecoration(
              color: isEmergency ? AppColors.red : _severityColor(session.severity),
              borderRadius: BorderRadius.circular(14),
              boxShadow: [
                BoxShadow(
                  color: (isEmergency ? AppColors.red : _severityColor(session.severity))
                      .withValues(alpha: 0.3),
                  blurRadius: 12,
                  offset: const Offset(0, 4),
                ),
              ],
            ),
            child: Row(
              children: [
                Icon(
                  isEmergency
                      ? Icons.emergency
                      : hasPrediction
                          ? Icons.trending_up
                          : Icons.monitor_heart_outlined,
                  color: Colors.white,
                  size: 22,
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        isEmergency
                            ? 'Критическое состояние!'
                            : hasPrediction
                                ? 'Прогноз эскалации'
                                : 'Активный мониторинг',
                        style: const TextStyle(
                          color: Colors.white,
                          fontWeight: FontWeight.w700,
                          fontSize: 14,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        _buildDescription(session, controller.sessions.length,
                            controller.maxPredictedRisk),
                        style: const TextStyle(
                          color: Colors.white,
                          fontSize: 12,
                          fontWeight: FontWeight.w400,
                        ),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ],
                  ),
                ),
                const Icon(Icons.chevron_right, color: Colors.white, size: 20),
              ],
            ),
          ),
        );
      },
    );
  }

  String _buildDescription(
      HealthMonitoringSession session, int total, int maxPredicted) {
    final childPart = session.childName != null ? '${session.childName} — ' : '';
    final extra = total > 1 ? ' (+${total - 1})' : '';
    final riskPart = maxPredicted > session.riskScore
        ? 'Риск: ${session.riskScore}→$maxPredicted/100'
        : 'Риск: ${session.riskScore}/100';
    return '$childPart${session.issueLabel}$extra · $riskPart';
  }

  static Color _severityColor(MonitoringSeverity severity) => switch (severity) {
        MonitoringSeverity.low => AppColors.green,
        MonitoringSeverity.medium => AppColors.amber,
        MonitoringSeverity.high => const Color(0xFFE07900),
        MonitoringSeverity.critical => AppColors.red,
        MonitoringSeverity.emergency => const Color(0xFF8B0000),
      };
}

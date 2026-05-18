import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../application/voice_notification_controller.dart';

class NotificationCenterScreen extends StatefulWidget {
  const NotificationCenterScreen({super.key});

  @override
  State<NotificationCenterScreen> createState() => _NotificationCenterScreenState();
}

class _NotificationCenterScreenState extends State<NotificationCenterScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabs;

  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: 2, vsync: this);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<VoiceNotificationController>().loadTimeline();
    });
  }

  @override
  void dispose() {
    _tabs.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF0F172A),
      appBar: AppBar(
        backgroundColor: const Color(0xFF1E293B),
        title: const Text('Уведомления AI', style: TextStyle(color: Colors.white)),
        iconTheme: const IconThemeData(color: Colors.white),
        bottom: TabBar(
          controller: _tabs,
          indicatorColor: const Color(0xFF4F46E5),
          labelColor: Colors.white,
          unselectedLabelColor: const Color(0xFF94A3B8),
          tabs: const [
            Tab(text: 'Активные'),
            Tab(text: 'История'),
          ],
        ),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh, color: Colors.white),
            onPressed: () => context.read<VoiceNotificationController>().loadTimeline(),
          ),
        ],
      ),
      body: Consumer<VoiceNotificationController>(
        builder: (context, ctrl, _) {
          if (ctrl.loading) {
            return const Center(child: CircularProgressIndicator(color: Color(0xFF4F46E5)));
          }
          if (ctrl.error != null) {
            return Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.error_outline, color: Color(0xFFEF4444), size: 40),
                  const SizedBox(height: 12),
                  Text(ctrl.error!, style: const TextStyle(color: Color(0xFF94A3B8))),
                  const SizedBox(height: 16),
                  ElevatedButton(
                    onPressed: ctrl.loadTimeline,
                    child: const Text('Повторить'),
                  ),
                ],
              ),
            );
          }

          final pending = ctrl.pending;
          final all = ctrl.actions;

          return TabBarView(
            controller: _tabs,
            children: [
              _buildPendingList(pending, ctrl),
              _buildHistoryList(all, ctrl),
            ],
          );
        },
      ),
    );
  }

  Widget _buildPendingList(List<VoiceActionModel> items, VoiceNotificationController ctrl) {
    if (items.isEmpty) {
      return const Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.check_circle_outline, size: 56, color: Color(0xFF10B981)),
            SizedBox(height: 12),
            Text('Нет активных уведомлений', style: TextStyle(color: Color(0xFF94A3B8))),
          ],
        ),
      );
    }

    return RefreshIndicator(
      color: const Color(0xFF4F46E5),
      onRefresh: ctrl.loadPending,
      child: ListView.builder(
        padding: const EdgeInsets.all(16),
        itemCount: items.length,
        itemBuilder: (_, i) => _VoiceActionCard(action: items[i], ctrl: ctrl),
      ),
    );
  }

  Widget _buildHistoryList(List<VoiceActionModel> items, VoiceNotificationController ctrl) {
    if (items.isEmpty) {
      return const Center(
        child: Text('История пуста', style: TextStyle(color: Color(0xFF94A3B8))),
      );
    }

    return RefreshIndicator(
      color: const Color(0xFF4F46E5),
      onRefresh: ctrl.loadTimeline,
      child: ListView.builder(
        padding: const EdgeInsets.all(16),
        itemCount: items.length,
        itemBuilder: (_, i) => _VoiceActionCard(action: items[i], ctrl: ctrl, compact: true),
      ),
    );
  }
}

class _VoiceActionCard extends StatelessWidget {
  const _VoiceActionCard({
    required this.action,
    required this.ctrl,
    this.compact = false,
  });

  final VoiceActionModel action;
  final VoiceNotificationController ctrl;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final isPending = action.status == 'Pending' || action.status == 'Delivered';
    final isEscalating = action.escalationStep > 0;

    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      decoration: BoxDecoration(
        color: const Color(0xFF1E293B),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(
          color: isEscalating
              ? const Color(0xFFF59E0B)
              : isPending
                  ? const Color(0xFF4F46E5)
                  : const Color(0xFF334155),
          width: isEscalating ? 2 : 1,
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                _typeIcon(),
                const SizedBox(width: 8),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        _typeLabel(),
                        style: const TextStyle(
                          color: Color(0xFF94A3B8),
                          fontSize: 11,
                          fontWeight: FontWeight.w600,
                          letterSpacing: 0.5,
                        ),
                      ),
                      _StatusBadge(status: action.status),
                    ],
                  ),
                ),
                _PriorityBadge(priority: action.priority),
              ],
            ),
            const SizedBox(height: 10),
            Text(
              action.text,
              style: const TextStyle(color: Colors.white, fontSize: 14, height: 1.4),
            ),
            if (isEscalating) ...[
              const SizedBox(height: 8),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: const Color(0xFF451A03),
                  borderRadius: BorderRadius.circular(6),
                ),
                child: Text(
                  '⚠️ Эскалация шаг ${action.escalationStep}',
                  style: const TextStyle(color: Color(0xFFF59E0B), fontSize: 11),
                ),
              ),
            ],
            const SizedBox(height: 10),
            Text(
              _formatDate(action.createdAt),
              style: const TextStyle(color: Color(0xFF64748B), fontSize: 11),
            ),
            if (isPending && action.requiresConfirmation && !compact) ...[
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: _ActionButton(
                      label: 'Подтвердить ✅',
                      color: const Color(0xFF10B981),
                      onTap: () => ctrl.confirm(action.id),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: _ActionButton(
                      label: 'Пропустить',
                      color: const Color(0xFF334155),
                      onTap: () => ctrl.skip(action.id),
                    ),
                  ),
                ],
              ),
            ] else if (isPending && !action.requiresConfirmation && !compact) ...[
              const SizedBox(height: 8),
              _ActionButton(
                label: 'Закрыть',
                color: const Color(0xFF334155),
                onTap: () => ctrl.skip(action.id),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _typeIcon() {
    final icon = switch (action.type) {
      'MedicationReminder' => '💊',
      'ProactiveRecommendation' => '🤖',
      'CalendarAlert' => '📅',
      'EmergencyAlert' => '🚨',
      'RoutineReminder' => '🔔',
      _ => '📢',
    };
    return Text(icon, style: const TextStyle(fontSize: 22));
  }

  String _typeLabel() => switch (action.type) {
        'MedicationReminder' => 'ЛЕКАРСТВО',
        'ProactiveRecommendation' => 'AI РЕКОМЕНДАЦИЯ',
        'CalendarAlert' => 'СОБЫТИЕ',
        'EmergencyAlert' => 'ЭКСТРЕННОЕ',
        'RoutineReminder' => 'РЕЖИМ',
        _ => 'УВЕДОМЛЕНИЕ',
      };

  String _formatDate(DateTime dt) {
    final now = DateTime.now();
    final diff = now.difference(dt);
    if (diff.inMinutes < 1) return 'только что';
    if (diff.inHours < 1) return '${diff.inMinutes} мин назад';
    if (diff.inDays < 1) return '${diff.inHours} ч назад';
    return '${dt.day}.${dt.month.toString().padLeft(2, '0')} ${dt.hour}:${dt.minute.toString().padLeft(2, '0')}';
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.status});
  final String status;

  @override
  Widget build(BuildContext context) {
    final (color, bg, label) = switch (status) {
      'Confirmed' => (const Color(0xFF10B981), const Color(0xFF064E3B), 'Подтверждено'),
      'Skipped' => (const Color(0xFF94A3B8), const Color(0xFF1E293B), 'Пропущено'),
      'Failed' => (const Color(0xFFEF4444), const Color(0xFF450A0A), 'Ошибка'),
      'Escalated' => (const Color(0xFFF59E0B), const Color(0xFF451A03), 'Эскалация'),
      'Delivered' => (const Color(0xFF60A5FA), const Color(0xFF0C2461), 'Доставлено'),
      _ => (const Color(0xFF94A3B8), const Color(0xFF1E293B), 'Ожидает'),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(4)),
      child: Text(label, style: TextStyle(color: color, fontSize: 10, fontWeight: FontWeight.w600)),
    );
  }
}

class _PriorityBadge extends StatelessWidget {
  const _PriorityBadge({required this.priority});
  final String priority;

  @override
  Widget build(BuildContext context) {
    final (icon, color) = switch (priority.toLowerCase()) {
      'emergency' => ('🚨', const Color(0xFFEF4444)),
      'high' => ('❗', const Color(0xFFF59E0B)),
      'low' => ('▽', const Color(0xFF64748B)),
      _ => ('●', const Color(0xFF60A5FA)),
    };
    return Text(icon, style: TextStyle(fontSize: 16, color: color));
  }
}

class _ActionButton extends StatelessWidget {
  const _ActionButton({required this.label, required this.color, required this.onTap});
  final String label;
  final Color color;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 10),
        decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(8)),
        child: Center(
          child: Text(label, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600, fontSize: 13)),
        ),
      ),
    );
  }
}

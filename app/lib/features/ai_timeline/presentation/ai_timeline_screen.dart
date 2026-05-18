import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:app/features/ai_timeline/application/ai_feed_controller.dart';
import 'package:app/features/notifications/application/voice_notification_controller.dart';

class AITimelineScreen extends StatefulWidget {
  const AITimelineScreen({super.key});

  @override
  State<AITimelineScreen> createState() => _AITimelineScreenState();
}

class _AITimelineScreenState extends State<AITimelineScreen> {
  static const _bg = Color(0xFF0F172A);
  static const _card = Color(0xFF1E293B);
  static const _primary = Color(0xFF4F46E5);
  static const _textPrimary = Color(0xFFE2E8F0);

  static const _filterLabels = ['Все', 'Напоминания', 'Здоровье', 'Погода', 'Брифинги'];
  static const _filterTypes = <String, Set<String>>{
    'Напоминания': {'MedicationReminder', 'LeaveHomeChecklist'},
    'Здоровье': {'AQIAlert', 'ProactiveRecommendation'},
    'Погода': {'WeatherAlert'},
    'Брифинги': {'MorningBriefing'},
  };

  int _filterIndex = 0;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<AIFeedController>().load();
    });
  }

  List<AIFeedItem> _filteredItems(List<AIFeedItem> items) {
    if (_filterIndex == 0) return items;
    final label = _filterLabels[_filterIndex];
    final types = _filterTypes[label];
    if (types == null) return items;
    return items.where((i) => types.contains(i.eventType)).toList();
  }

  @override
  Widget build(BuildContext context) {
    final feed = context.watch<AIFeedController>();
    final voiceCtrl = context.watch<VoiceNotificationController>();
    final filtered = _filteredItems(feed.items);

    return Scaffold(
      backgroundColor: _bg,
      appBar: AppBar(
        backgroundColor: _bg,
        elevation: 0,
        centerTitle: false,
        title: const Text(
          'AI Таймлайн',
          style: TextStyle(
            color: _textPrimary,
            fontSize: 20,
            fontWeight: FontWeight.w700,
            letterSpacing: -0.3,
          ),
        ),
        iconTheme: const IconThemeData(color: _textPrimary),
      ),
      body: Column(
        children: [
          _FilterTabs(
            labels: _filterLabels,
            selectedIndex: _filterIndex,
            onSelect: (i) => setState(() => _filterIndex = i),
          ),
          const SizedBox(height: 4),
          Expanded(
            child: feed.loading && feed.items.isEmpty
                ? const Center(
                    child: CircularProgressIndicator(
                      color: _primary,
                      strokeWidth: 2,
                    ),
                  )
                : filtered.isEmpty
                    ? _EmptyState(isFiltered: _filterIndex != 0)
                    : RefreshIndicator(
                        color: _primary,
                        backgroundColor: _card,
                        onRefresh: () => feed.load(),
                        child: ListView.separated(
                          padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
                          itemCount: filtered.length,
                          separatorBuilder: (_, _) => const SizedBox(height: 8),
                          itemBuilder: (context, i) {
                            final item = filtered[i];
                            VoiceActionModel? linkedAction;
                            if (item.correlationId != null) {
                              try {
                                linkedAction = voiceCtrl.actions.firstWhere(
                                  (a) => a.id.toString() == item.correlationId,
                                );
                              } catch (_) {
                                linkedAction = null;
                              }
                            }
                            return _TimelineCard(
                              item: item,
                              linkedAction: linkedAction,
                              voiceCtrl: voiceCtrl,
                            );
                          },
                        ),
                      ),
          ),
        ],
      ),
    );
  }
}

class _FilterTabs extends StatelessWidget {
  const _FilterTabs({
    required this.labels,
    required this.selectedIndex,
    required this.onSelect,
  });

  final List<String> labels;
  final int selectedIndex;
  final ValueChanged<int> onSelect;

  static const _card = Color(0xFF1E293B);
  static const _primary = Color(0xFF4F46E5);
  static const _textSecondary = Color(0xFF94A3B8);

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 40,
      margin: const EdgeInsets.fromLTRB(16, 8, 16, 0),
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: labels.length,
        separatorBuilder: (_, _) => const SizedBox(width: 8),
        itemBuilder: (context, i) {
          final selected = i == selectedIndex;
          return GestureDetector(
            onTap: () => onSelect(i),
            child: AnimatedContainer(
              duration: const Duration(milliseconds: 180),
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
              decoration: BoxDecoration(
                color: selected ? _primary : _card,
                borderRadius: BorderRadius.circular(20),
                border: Border.all(
                  color: selected
                      ? _primary
                      : const Color(0xFF334155),
                ),
              ),
              child: Text(
                labels[i],
                style: TextStyle(
                  color: selected ? Colors.white : _textSecondary,
                  fontSize: 13,
                  fontWeight: selected ? FontWeight.w600 : FontWeight.w400,
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}

class _TimelineCard extends StatelessWidget {
  const _TimelineCard({
    required this.item,
    this.linkedAction,
    required this.voiceCtrl,
  });

  final AIFeedItem item;
  final VoiceActionModel? linkedAction;
  final VoiceNotificationController voiceCtrl;

  static const _card = Color(0xFF1E293B);
  static const _primary = Color(0xFF4F46E5);
  static const _textPrimary = Color(0xFFE2E8F0);
  static const _textSecondary = Color(0xFF94A3B8);

  Color _dotColor() {
    switch (item.eventType) {
      case 'MorningBriefing':
        return const Color(0xFFF59E0B);
      case 'MedicationReminder':
        return const Color(0xFFEF4444);
      case 'WeatherAlert':
        return const Color(0xFF3B82F6);
      case 'AQIAlert':
        return const Color(0xFFF97316);
      case 'LeaveHomeChecklist':
        return const Color(0xFF22C55E);
      default:
        return _primary;
    }
  }

  IconData _icon() {
    switch (item.eventType) {
      case 'MorningBriefing':
        return Icons.wb_sunny_outlined;
      case 'MedicationReminder':
        return Icons.medication_outlined;
      case 'WeatherAlert':
        return Icons.cloud_outlined;
      case 'AQIAlert':
        return Icons.air;
      case 'LeaveHomeChecklist':
        return Icons.checklist;
      case 'ProactiveRecommendation':
        return Icons.lightbulb_outline;
      default:
        return Icons.smart_toy_outlined;
    }
  }

  String _eventLabel() {
    switch (item.eventType) {
      case 'MorningBriefing':
        return 'Утренний брифинг';
      case 'MedicationReminder':
        return 'Напоминание о лекарстве';
      case 'WeatherAlert':
        return 'Погодное оповещение';
      case 'AQIAlert':
        return 'Качество воздуха';
      case 'LeaveHomeChecklist':
        return 'Чек-лист выхода';
      case 'ProactiveRecommendation':
        return 'Рекомендация AI';
      default:
        return 'AI Событие';
    }
  }

  String _statusLabel() {
    switch (item.status) {
      case 'Confirmed':
        return 'Подтверждено';
      case 'Skipped':
        return 'Пропущено';
      default:
        return 'Ожидает';
    }
  }

  Color _statusColor() {
    switch (item.status) {
      case 'Confirmed':
        return const Color(0xFF22C55E);
      case 'Skipped':
        return _textSecondary;
      default:
        return const Color(0xFFF59E0B);
    }
  }

  String _relativeTime() {
    final diff = DateTime.now().difference(item.createdAt);
    if (diff.inMinutes < 1) return 'только что';
    if (diff.inMinutes < 60) return '${diff.inMinutes} мин назад';
    if (diff.inHours < 24) return '${diff.inHours} ч назад';
    return '${diff.inDays} д назад';
  }

  bool get _isPending =>
      item.status == 'Pending' || item.status == 'Delivered';

  @override
  Widget build(BuildContext context) {
    final dotColor = _dotColor();

    return Container(
      decoration: BoxDecoration(
        color: _card,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFF2D3748)),
      ),
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Container(
              width: 4,
              decoration: BoxDecoration(
                color: dotColor,
                borderRadius: const BorderRadius.only(
                  topLeft: Radius.circular(14),
                  bottomLeft: Radius.circular(14),
                ),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(0, 12, 12, 12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Icon(_icon(), size: 16, color: dotColor),
                        const SizedBox(width: 6),
                        Expanded(
                          child: Text(
                            _eventLabel(),
                            style: const TextStyle(
                              color: _textSecondary,
                              fontSize: 11,
                              fontWeight: FontWeight.w500,
                              letterSpacing: 0.5,
                            ),
                          ),
                        ),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 8,
                            vertical: 3,
                          ),
                          decoration: BoxDecoration(
                            color: _statusColor().withValues(alpha: 0.15),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Text(
                            _statusLabel(),
                            style: TextStyle(
                              color: _statusColor(),
                              fontSize: 10,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Text(
                      item.content,
                      maxLines: 3,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: _textPrimary,
                        fontSize: 14,
                        height: 1.45,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      _relativeTime(),
                      style: const TextStyle(
                        color: _textSecondary,
                        fontSize: 11,
                      ),
                    ),
                    if (_isPending && linkedAction != null) ...[
                      const SizedBox(height: 10),
                      Row(
                        children: [
                          Expanded(
                            child: _ActionButton(
                              label: 'Подтвердить ✅',
                              color: const Color(0xFF22C55E),
                              onTap: () => voiceCtrl.confirm(linkedAction!.id),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: _ActionButton(
                              label: 'Пропустить',
                              color: _textSecondary,
                              onTap: () => voiceCtrl.skip(linkedAction!.id),
                            ),
                          ),
                        ],
                      ),
                    ],
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ActionButton extends StatelessWidget {
  const _ActionButton({
    required this.label,
    required this.color,
    required this.onTap,
  });

  final String label;
  final Color color;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        height: 34,
        decoration: BoxDecoration(
          color: color.withValues(alpha: 0.12),
          borderRadius: BorderRadius.circular(10),
          border: Border.all(color: color.withValues(alpha: 0.3)),
        ),
        alignment: Alignment.center,
        child: Text(
          label,
          style: TextStyle(
            color: color,
            fontSize: 12,
            fontWeight: FontWeight.w600,
          ),
        ),
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.isFiltered});
  final bool isFiltered;

  static const _textSecondary = Color(0xFF94A3B8);

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              isFiltered ? Icons.filter_list_off : Icons.smart_toy_outlined,
              size: 48,
              color: _textSecondary,
            ),
            const SizedBox(height: 16),
            Text(
              isFiltered ? 'Нет событий в этой категории' : 'AI ещё не создавал событий',
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: _textSecondary,
                fontSize: 15,
                fontWeight: FontWeight.w500,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              isFiltered
                  ? 'Попробуйте выбрать другую категорию'
                  : 'Проактивные уведомления появятся здесь',
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: _textSecondary,
                fontSize: 13,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

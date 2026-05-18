import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_text_styles.dart';
import 'package:app/shared/theme/app_spacing.dart';
import 'package:app/features/notifications/application/notifications_controller.dart';
import 'package:app/features/notifications/domain/notification_model.dart';
import 'package:app/features/settings/application/app_settings_controller.dart';
import 'package:app/core/localization/app_strings.dart';

class NotificationsScreen extends StatefulWidget {
  const NotificationsScreen({super.key});

  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends State<NotificationsScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<NotificationsController>().load();
    });
  }

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    final ctrl = context.watch<NotificationsController>();

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Column(
          children: [
            // Header
            Padding(
              padding: const EdgeInsets.fromLTRB(
                  AppSpacing.lg, AppSpacing.lg, AppSpacing.lg, 8),
              child: Row(
                children: [
                  GestureDetector(
                    onTap: () => context.pop(),
                    child: Container(
                      width: 36,
                      height: 36,
                      decoration: BoxDecoration(
                        color: AppColors.card,
                        shape: BoxShape.circle,
                        border: Border.all(color: AppColors.line),
                      ),
                      child: const Icon(Icons.arrow_back,
                          size: 18, color: AppColors.textSecondary),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(strings.notificationsTitle,
                        style: AppTextStyles.titleLarge
                            .copyWith(fontSize: 22)),
                  ),
                  if (ctrl.unreadCount > 0)
                    GestureDetector(
                      onTap: () => ctrl.markAllRead(),
                      child: Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 12, vertical: 8),
                        decoration: BoxDecoration(
                          color: AppColors.blueSoft,
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Text(
                          strings.markAllRead,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: AppTextStyles.labelMedium.copyWith(
                              color: AppColors.primary,
                              fontWeight: FontWeight.w600,
                              fontSize: 12),
                        ),
                      ),
                    ),
                ],
              ),
            ),

            const SizedBox(height: 4),

            Expanded(
              child: ctrl.isLoading && ctrl.items.isEmpty
                  ? const Center(
                      child: CircularProgressIndicator(
                          strokeWidth: 2, color: AppColors.primary))
                  : ctrl.items.isEmpty
                      ? _Empty(strings: strings)
                      : RefreshIndicator(
                          color: AppColors.primary,
                          onRefresh: ctrl.load,
                          child: _GroupedList(
                            items: ctrl.items,
                            strings: strings,
                            onTapItem: (n) {
                              if (n.isUnread) ctrl.markRead(n.id);
                              context.push('/rec/${n.recommendationId}');
                            },
                          ),
                        ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Empty extends StatelessWidget {
  const _Empty({required this.strings});
  final AppStrings strings;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 72,
              height: 72,
              decoration: BoxDecoration(
                color: AppColors.blueSoft,
                shape: BoxShape.circle,
              ),
              child: const Icon(Icons.notifications_none,
                  size: 32, color: AppColors.primary),
            ),
            const SizedBox(height: 16),
            Text(strings.noNotifications,
                style: AppTextStyles.titleLarge.copyWith(fontSize: 18)),
            const SizedBox(height: 6),
            Text(
              strings.noNotificationsHint,
              textAlign: TextAlign.center,
              style: AppTextStyles.bodySmall
                  .copyWith(color: AppColors.textTertiary),
            ),
          ],
        ),
      ),
    );
  }
}

class _GroupedList extends StatelessWidget {
  const _GroupedList({
    required this.items,
    required this.strings,
    required this.onTapItem,
  });
  final List<NotificationModel> items;
  final AppStrings strings;
  final void Function(NotificationModel) onTapItem;

  @override
  Widget build(BuildContext context) {
    final today = <NotificationModel>[];
    final yesterday = <NotificationModel>[];
    final earlier = <NotificationModel>[];
    final now = DateTime.now();
    final startOfToday = DateTime(now.year, now.month, now.day);
    final startOfYesterday = startOfToday.subtract(const Duration(days: 1));

    for (final n in items) {
      if (n.createdAt.isAfter(startOfToday)) {
        today.add(n);
      } else if (n.createdAt.isAfter(startOfYesterday)) {
        yesterday.add(n);
      } else {
        earlier.add(n);
      }
    }

    final blocks = <Widget>[];
    void addBlock(String label, List<NotificationModel> list) {
      if (list.isEmpty) return;
      blocks.add(Padding(
        padding: const EdgeInsets.fromLTRB(4, 10, 4, 6),
        child: Text(
          label,
          style: AppTextStyles.labelSmall.copyWith(
            color: AppColors.textTertiary,
            letterSpacing: 0.8,
            fontSize: 10,
          ),
        ),
      ));
      for (final n in list) {
        blocks.add(Padding(
          padding: const EdgeInsets.only(bottom: 10),
          child: _NotificationTile(
            n: n,
            strings: strings,
            onTap: () => onTapItem(n),
          ),
        ));
      }
    }

    addBlock(strings.sectionToday, today);
    addBlock(strings.sectionYesterday, yesterday);
    addBlock(strings.sectionEarlier, earlier);

    return ListView(
      padding: const EdgeInsets.fromLTRB(
          AppSpacing.lg, 4, AppSpacing.lg, AppSpacing.lg),
      children: blocks,
    );
  }
}

class _NotificationTile extends StatelessWidget {
  const _NotificationTile({
    required this.n,
    required this.strings,
    required this.onTap,
  });

  final NotificationModel n;
  final AppStrings strings;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final tone = _toneFor(n.type);
    final isUnread = n.isUnread;
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: isUnread ? AppColors.blueSoft.withValues(alpha: 0.4) : AppColors.card,
          borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
          border: Border.all(
            color: isUnread
                ? AppColors.primary.withValues(alpha: 0.25)
                : AppColors.line,
          ),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              width: 38,
              height: 38,
              decoration: BoxDecoration(
                color: tone.bg,
                borderRadius: BorderRadius.circular(12),
              ),
              child: Icon(_iconFor(n.type), size: 18, color: tone.ink),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          n.title,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: AppTextStyles.labelLarge.copyWith(
                            fontSize: 14.5,
                            fontWeight:
                                isUnread ? FontWeight.w700 : FontWeight.w600,
                          ),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Text(
                        _relativeTime(n.createdAt, strings),
                        style: AppTextStyles.caption.copyWith(
                            color: AppColors.textTertiary, fontSize: 11),
                      ),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text(
                    n.message,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: AppTextStyles.bodySmall.copyWith(
                      color: AppColors.textSecondary,
                      height: 1.35,
                    ),
                  ),
                ],
              ),
            ),
            if (isUnread)
              Container(
                margin: const EdgeInsets.only(left: 8, top: 4),
                width: 8,
                height: 8,
                decoration: const BoxDecoration(
                  color: AppColors.primary,
                  shape: BoxShape.circle,
                ),
              ),
          ],
        ),
      ),
    );
  }

  static String _relativeTime(DateTime t, AppStrings strings) {
    final diff = DateTime.now().difference(t);
    if (diff.inMinutes < 1) return strings.justNow;
    if (diff.inMinutes < 60) return strings.minutesAgo(diff.inMinutes);
    if (diff.inHours < 24) return strings.hoursAgo(diff.inHours);
    return strings.daysAgo(diff.inDays);
  }

  static IconData _iconFor(String type) {
    switch (type.toLowerCase()) {
      case 'clothing':
        return Icons.checkroom_outlined;
      case 'hydration':
        return Icons.water_drop_outlined;
      case 'sleep':
      case 'bedtime':
        return Icons.bedtime_outlined;
      case 'health':
        return Icons.favorite_outline;
      case 'activity':
        return Icons.directions_run;
      case 'nutrition':
      case 'food':
        return Icons.restaurant_outlined;
      case 'safety':
        return Icons.shield_outlined;
      default:
        return Icons.lightbulb_outline;
    }
  }

  static _Tone _toneFor(String type) {
    switch (type.toLowerCase()) {
      case 'clothing':
        return const _Tone(bg: AppColors.blueSoft, ink: AppColors.blue2);
      case 'hydration':
        return const _Tone(bg: AppColors.amberSoft, ink: Color(0xFF7A5800));
      case 'sleep':
      case 'bedtime':
        return const _Tone(bg: AppColors.violetSoft, ink: Color(0xFF3A2BB3));
      case 'health':
        return const _Tone(bg: AppColors.redSoft, ink: AppColors.red);
      case 'activity':
      case 'nutrition':
      case 'food':
        return const _Tone(bg: AppColors.greenSoft, ink: Color(0xFF0A6E44));
      default:
        return const _Tone(bg: AppColors.surface, ink: AppColors.textSecondary);
    }
  }
}

class _Tone {
  const _Tone({required this.bg, required this.ink});
  final Color bg;
  final Color ink;
}

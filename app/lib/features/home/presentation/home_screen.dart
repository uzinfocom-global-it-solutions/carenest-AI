import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:app/core/localization/app_strings.dart';
import 'package:app/features/settings/application/app_settings_controller.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_text_styles.dart';
import 'package:app/shared/theme/app_spacing.dart';
import 'package:app/shared/widgets/app_avatar.dart';
import 'package:app/shared/widgets/app_card.dart';
import 'package:app/features/auth/application/auth_controller.dart';
import 'package:app/features/children/application/children_controller.dart';
import 'package:app/features/children/domain/child_models.dart';
import 'package:app/features/notifications/application/notifications_controller.dart';
import 'package:app/features/onboarding/domain/onboarding_models.dart';
import 'package:app/features/recommendations/application/recommendations_controller.dart';
import 'package:app/features/weather/application/weather_controller.dart';
import 'package:app/features/weather/data/location_service.dart';
import 'package:app/features/weather/domain/weather_models.dart';
import 'package:app/shared/widgets/health_risk_banner.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  int? _selectedChildId;

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthController>();
    final childrenCtrl = context.watch<ChildrenController>();
    final weatherCtrl = context.watch<WeatherController>();
    final recsCtrl = context.watch<RecommendationsController>();

    final children = childrenCtrl.children;
    final weather = weatherCtrl.weather;
    final recs = _selectedChildId == null
        ? recsCtrl.recommendations
        : recsCtrl.recommendations
              .where((r) => r.childId == _selectedChildId)
              .toList();

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        bottom: false,
        child: RefreshIndicator(
          color: AppColors.primary,
          onRefresh: () async {
            final location = await LocationService().getLocation();
            if (location != null) {
              await weatherCtrl.load(
                location.locationKey,
                lat: location.latitude,
                lon: location.longitude,
              );
            } else {
              await weatherCtrl.load('home', lat: 41.2995, lon: 69.2401);
            }
            await recsCtrl.loadForChildren(children);
          },
          child: CustomScrollView(
            slivers: [
              SliverToBoxAdapter(
                child: _Header(
                  displayName: auth.displayName,
                  unreadCount: context.watch<NotificationsController>().unreadCount,
                ),
              ),
              SliverPadding(
                padding: const EdgeInsets.fromLTRB(
                  AppSpacing.lg,
                  AppSpacing.sm,
                  AppSpacing.lg,
                  24,
                ),
                sliver: SliverList.list(
                  children: [
                    const HealthRiskBanner(),
                    if (children.isNotEmpty)
                      _ChildSwitcher(
                        children: children,
                        selectedId: _selectedChildId,
                        onSelect: (id) => setState(() => _selectedChildId = id),
                      ),
                    if (children.isNotEmpty) const SizedBox(height: 14),
                    _WeatherCard(
                      weather: weather,
                      loading: weatherCtrl.isLoading,
                    ),
                    const SizedBox(height: 18),
                    _RecsHeader(count: recs.length),
                    const SizedBox(height: 8),
                    if (recsCtrl.isLoading)
                      const Padding(
                        padding: EdgeInsets.symmetric(vertical: 32),
                        child: Center(
                          child: CircularProgressIndicator(
                            color: AppColors.primary,
                          ),
                        ),
                      )
                    else if (recs.isEmpty)
                      _EmptyRecs(hasChildren: children.isNotEmpty)
                    else
                      ...recs.map(
                        (r) => _RecCard(
                          rec: r,
                          childName: _childNameFor(children, r.childId),
                          onTap: () => context.push('/rec/${r.id}'),
                        ),
                      ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  String _childNameFor(List<ChildModel> children, int childId) {
    final match = children.where((c) => c.id == childId).toList();
    return match.isEmpty ? '' : match.first.displayName;
  }
}

class _Header extends StatelessWidget {
  const _Header({this.displayName, this.unreadCount = 0});
  final String? displayName;
  final int unreadCount;

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    final now = DateTime.now();
    final dateLabel = _formatDate(now, strings);
    final greeting = _greetingFor(now, strings);
    final name = (displayName == null || displayName!.isEmpty)
        ? ''
        : ', ${displayName!.split(' ').first}';

    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 18, 20, 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  dateLabel,
                  style: AppTextStyles.caption.copyWith(
                    color: AppColors.textTertiary,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  '$greeting$name',
                  style: AppTextStyles.titleLarge.copyWith(fontSize: 26),
                ),
              ],
            ),
          ),
          _CircleIconButton(
            icon: Icons.notifications_none,
            badge: unreadCount,
            onTap: () => context.push('/notifications'),
          ),
          const SizedBox(width: 8),
          _CircleIconButton(
            icon: Icons.settings_outlined,
            onTap: () => context.push('/settings'),
          ),
        ],
      ),
    );
  }

  static String _formatDate(DateTime d, AppStrings strings) {
    final weekdays = strings.dayNames;
    final months = strings.monthAbbr;
    return '${weekdays[d.weekday - 1]} · ${months[d.month - 1]} ${d.day}';
  }

  static String _greetingFor(DateTime d, AppStrings strings) {
    final h = d.hour;
    if (h < 12) return strings.goodMorning;
    if (h < 18) return strings.goodAfternoon;
    return strings.goodEvening;
  }
}


class _CircleIconButton extends StatelessWidget {
  const _CircleIconButton({
    required this.icon,
    required this.onTap,
    this.badge = 0,
  });
  final IconData icon;
  final VoidCallback onTap;
  final int badge;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: SizedBox(
        width: 40,
        height: 40,
        child: Stack(
          clipBehavior: Clip.none,
          alignment: Alignment.center,
          children: [
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color: AppColors.card,
                borderRadius: BorderRadius.circular(20),
                border: Border.all(color: AppColors.line),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withValues(alpha: 0.05),
                    blurRadius: 8,
                    offset: const Offset(0, 2),
                  ),
                ],
              ),
              child: Icon(icon, size: 18, color: AppColors.textSecondary),
            ),
            if (badge > 0)
              Positioned(
                top: -2,
                right: -2,
                child: Container(
                  constraints:
                      const BoxConstraints(minWidth: 16, minHeight: 16),
                  padding: const EdgeInsets.symmetric(horizontal: 4),
                  decoration: BoxDecoration(
                    color: AppColors.red,
                    borderRadius: BorderRadius.circular(10),
                    border: Border.all(color: AppColors.background, width: 1.5),
                  ),
                  alignment: Alignment.center,
                  child: Text(
                    badge > 9 ? '9+' : '$badge',
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 9.5,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _ChildSwitcher extends StatelessWidget {
  const _ChildSwitcher({
    required this.children,
    required this.selectedId,
    required this.onSelect,
  });

  final List<ChildModel> children;
  final int? selectedId;
  final ValueChanged<int?> onSelect;

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    return SizedBox(
      height: 36,
      child: ListView(
        scrollDirection: Axis.horizontal,
        children: [
          _Chip(
            label: strings.filterAll,
            selected: selectedId == null,
            onTap: () => onSelect(null),
          ),
          ...children.asMap().entries.map((entry) {
            final i = entry.key;
            final c = entry.value;
            return Padding(
              padding: const EdgeInsets.only(left: 8),
              child: _Chip(
                label: c.displayName,
                selected: selectedId == c.id,
                onTap: () => onSelect(c.id),
                avatarColor: ChildAvatarColor.forIndex(i),
                initial: c.displayName.isEmpty ? '?' : c.displayName[0],
              ),
            );
          }),
        ],
      ),
    );
  }
}

class _Chip extends StatelessWidget {
  const _Chip({
    required this.label,
    required this.selected,
    required this.onTap,
    this.avatarColor,
    this.initial,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;
  final ChildAvatarColor? avatarColor;
  final String? initial;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 180),
        padding: EdgeInsets.fromLTRB(initial == null ? 14 : 6, 0, 12, 0),
        decoration: BoxDecoration(
          gradient: selected
              ? const LinearGradient(
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                  colors: [Color(0xFFEBF0FF), AppColors.blueSoft],
                )
              : null,
          color: selected ? null : AppColors.card,
          borderRadius: BorderRadius.circular(18),
          border: Border.all(
            color: selected ? AppColors.primary.withValues(alpha: 0.5) : AppColors.line,
            width: selected ? 1.5 : 1,
          ),
          boxShadow: selected
              ? [BoxShadow(color: AppColors.primary.withValues(alpha: 0.12), blurRadius: 8, offset: const Offset(0, 2))]
              : null,
        ),
        child: Row(
          children: [
            if (initial != null && avatarColor != null) ...[
              AppAvatar(initial: initial!, size: 24, childColor: avatarColor!),
              const SizedBox(width: 8),
            ],
            Text(
              label,
              style: AppTextStyles.labelMedium.copyWith(
                color: selected ? AppColors.blue2 : AppColors.textSecondary,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _WeatherCard extends StatelessWidget {
  const _WeatherCard({required this.weather, required this.loading});
  final WeatherModel? weather;
  final bool loading;

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    if (weather == null) {
      return AppCard(
        padding: const EdgeInsets.all(20),
        child: Row(
          children: [
            const Icon(Icons.cloud_outlined, color: AppColors.textQuaternary),
            const SizedBox(width: 12),
            Text(
              loading ? strings.loadingWeather : strings.weatherUnavailable,
              style: AppTextStyles.bodyMedium.copyWith(
                color: AppColors.textTertiary,
              ),
            ),
          ],
        ),
      );
    }
    final w = weather!;
    return Container(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [
            _gradientStartFor(w.condition),
            _gradientEndFor(w.condition),
          ],
        ),
        borderRadius: BorderRadius.circular(16),
        boxShadow: [
          BoxShadow(
            color: _gradientStartFor(w.condition).withValues(alpha: 0.18),
            blurRadius: 16,
            offset: const Offset(0, 6),
          ),
        ],
      ),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '${_titleCase(w.locationKey)} · Today',
                      style: AppTextStyles.caption.copyWith(
                        color: AppColors.textTertiary,
                        letterSpacing: 0.3,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.baseline,
                      textBaseline: TextBaseline.alphabetic,
                      children: [
                        Text(
                          w.tempLabel,
                          style: AppTextStyles.titleLarge.copyWith(
                            fontSize: 34,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        const SizedBox(width: 6),
                        if (w.feelsLikeC != null)
                          Text(
                            'feels like ${w.feelsLikeC!.round()}°',
                            style: AppTextStyles.caption.copyWith(
                              color: AppColors.textTertiary,
                            ),
                          ),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Text(
                      w.condition,
                      style: AppTextStyles.bodySmall.copyWith(
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              Container(
                width: 56,
                height: 56,
                decoration: BoxDecoration(
                  gradient: const LinearGradient(
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                    colors: [Color(0xFFCFDCFF), AppColors.blueSoft],
                  ),
                  borderRadius: BorderRadius.circular(16),
                  boxShadow: [
                    BoxShadow(
                      color: AppColors.primary.withValues(alpha: 0.15),
                      blurRadius: 10,
                      offset: const Offset(0, 4),
                    ),
                  ],
                ),
                child: Icon(
                  _iconForCondition(w.condition),
                  size: 32,
                  color: AppColors.primary,
                ),
              ),
            ],
          ),
          const SizedBox(height: 14),
          Row(
            children: [
              if (w.aqi != null)
                _MetricChip(
                  icon: Icons.water_drop_outlined,
                  label: 'AQI',
                  value: w.aqi!,
                ),
              if (w.uvIndex != null) ...[
                if (w.aqi != null) const SizedBox(width: 6),
                _MetricChip(
                  icon: Icons.wb_sunny_outlined,
                  label: 'UV',
                  value: w.uvIndex!.round().toString(),
                ),
              ],
              if (w.pollenLevel != null) ...[
                const SizedBox(width: 6),
                _MetricChip(
                  icon: Icons.grass_outlined,
                  label: 'Pollen',
                  value: w.pollenLevel!,
                ),
              ],
              if (w.windKmh != null) ...[
                const SizedBox(width: 6),
                _MetricChip(
                  icon: Icons.air,
                  label: 'Wind',
                  value: w.windKmh!.round().toString(),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }

  static IconData _iconForCondition(String c) {
    switch (c.toLowerCase()) {
      case 'sunny':
        return Icons.wb_sunny_outlined;
      case 'rainy':
        return Icons.umbrella_outlined;
      case 'snowy':
        return Icons.ac_unit_outlined;
      case 'storm':
        return Icons.flash_on_outlined;
      case 'windy':
        return Icons.air;
      default:
        return Icons.cloud_outlined;
    }
  }

  static String _titleCase(String s) {
    if (s.isEmpty) return s;
    return s[0].toUpperCase() + s.substring(1).toLowerCase();
  }

  static Color _gradientStartFor(String c) {
    switch (c.toLowerCase()) {
      case 'sunny':
        return const Color(0xFFFFF8E1);
      case 'rainy':
        return const Color(0xFFE3F0FF);
      case 'snowy':
        return const Color(0xFFEAF4FF);
      case 'storm':
        return const Color(0xFFEDE9FF);
      case 'windy':
        return const Color(0xFFE8F5F0);
      default:
        return const Color(0xFFEEF3FF);
    }
  }

  static Color _gradientEndFor(String c) {
    switch (c.toLowerCase()) {
      case 'sunny':
        return const Color(0xFFFFF3E0);
      case 'rainy':
        return const Color(0xFFD6E8FF);
      case 'snowy':
        return const Color(0xFFDDF1FF);
      case 'storm':
        return const Color(0xFFE0D8FF);
      case 'windy':
        return const Color(0xFFDCF2EC);
      default:
        return const Color(0xFFE4ECFF);
    }
  }
}

class _MetricChip extends StatelessWidget {
  const _MetricChip({
    required this.icon,
    required this.label,
    required this.value,
  });
  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Container(
        height: 64,
        decoration: BoxDecoration(
          color: AppColors.backgroundElevated,
          borderRadius: BorderRadius.circular(12),
        ),
        padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 8),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(icon, size: 14, color: AppColors.textTertiary),
            const SizedBox(height: 2),
            FittedBox(
              fit: BoxFit.scaleDown,
              child: Text(
                value,
                maxLines: 1,
                style: AppTextStyles.labelMedium.copyWith(
                  fontSize: 13,
                  height: 1.1,
                ),
              ),
            ),
            FittedBox(
              fit: BoxFit.scaleDown,
              child: Text(
                label,
                maxLines: 1,
                style: AppTextStyles.caption.copyWith(
                  fontSize: 10,
                  height: 1.1,
                  color: AppColors.textTertiary,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}


class _RecsHeader extends StatelessWidget {
  const _RecsHeader({required this.count});
  final int count;

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    return Padding(
      padding: const EdgeInsets.fromLTRB(4, 4, 4, 0),
      child: Row(
        children: [
          Text(
            strings.forToday,
            style: AppTextStyles.labelLarge.copyWith(fontSize: 16),
          ),
          const Spacer(),
          if (count > 0)
            Text(
              strings.suggestions(count),
              style: AppTextStyles.caption.copyWith(
                color: AppColors.textTertiary,
              ),
            ),
        ],
      ),
    );
  }
}

class _EmptyRecs extends StatelessWidget {
  const _EmptyRecs({required this.hasChildren});
  final bool hasChildren;

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    return AppCard(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            hasChildren ? Icons.auto_awesome : Icons.family_restroom,
            size: 28,
            color: AppColors.textTertiary,
          ),
          const SizedBox(height: 10),
          Text(
            hasChildren ? strings.noSuggestionsYet : strings.addChildForSuggestions,
            style: AppTextStyles.labelLarge.copyWith(fontSize: 15),
          ),
          const SizedBox(height: 4),
          Text(
            hasChildren
                ? 'Pull to refresh — recommendations are generated from weather, sensitivities, and routines.'
                : 'Tap the voice circle above and say "Add my son Noah, he\'s 3" to get started.',
            style: AppTextStyles.bodySmall.copyWith(
              color: AppColors.textTertiary,
            ),
          ),
        ],
      ),
    );
  }
}

class _RecCard extends StatelessWidget {
  const _RecCard({
    required this.rec,
    required this.childName,
    required this.onTap,
  });

  final RecommendationModel rec;
  final String childName;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final tone = _toneFor(rec.type);
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: GestureDetector(
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: AppColors.card,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: AppColors.line),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.04),
                blurRadius: 10,
                offset: const Offset(0, 3),
              ),
            ],
          ),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 36,
                height: 36,
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                    colors: [tone.bg, Color.lerp(tone.bg, tone.ink, 0.08)!],
                  ),
                  borderRadius: BorderRadius.circular(12),
                  boxShadow: [
                    BoxShadow(
                      color: tone.ink.withValues(alpha: 0.10),
                      blurRadius: 6,
                      offset: const Offset(0, 2),
                    ),
                  ],
                ),
                child: Icon(_iconFor(rec.type), size: 18, color: tone.ink),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Text(
                          rec.type.toUpperCase(),
                          style: AppTextStyles.caption.copyWith(
                            color: tone.ink,
                            fontWeight: FontWeight.w600,
                            letterSpacing: 0.6,
                          ),
                        ),
                        if (childName.isNotEmpty) ...[
                          const SizedBox(width: 6),
                          Text(
                            '· $childName',
                            style: AppTextStyles.caption.copyWith(
                              color: AppColors.textQuaternary,
                            ),
                          ),
                        ],
                      ],
                    ),
                    const SizedBox(height: 3),
                    Text(
                      rec.title,
                      style: AppTextStyles.labelLarge.copyWith(
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    const SizedBox(height: 3),
                    Text(
                      rec.message,
                      style: AppTextStyles.bodySmall.copyWith(
                        color: AppColors.textTertiary,
                        height: 1.4,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  static IconData _iconFor(String type) {
    switch (type.toLowerCase()) {
      case 'clothing':
        return Icons.checkroom_outlined;
      case 'hydration':
        return Icons.water_drop_outlined;
      case 'bedtime':
      case 'sleep':
        return Icons.bedtime_outlined;
      case 'health':
        return Icons.favorite_outline;
      case 'activity':
        return Icons.directions_run;
      case 'nutrition':
      case 'food':
        return Icons.restaurant_outlined;
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
      case 'bedtime':
      case 'sleep':
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

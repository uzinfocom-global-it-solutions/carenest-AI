import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_text_styles.dart';
import 'package:app/shared/theme/app_spacing.dart';
import 'package:app/features/calendar/application/calendar_controller.dart';
import 'package:app/features/calendar/domain/calendar_models.dart';
import 'package:app/features/children/application/children_controller.dart';
import 'package:app/features/children/domain/child_models.dart';
import 'package:app/features/weather/application/weather_controller.dart';
import 'package:app/core/localization/app_strings.dart';
import 'package:app/features/settings/application/app_settings_controller.dart';
import 'package:go_router/go_router.dart';

// ─── Unified timeline item ────────────────────────────────────────────────────
class _PlanItem {
  final CalendarEventModel? event;
  final ChildRoutineModel? routine;
  final String _sortKey;

  _PlanItem.fromEvent(CalendarEventModel e)
      : event = e,
        routine = null,
        _sortKey =
            '${e.startDatetime.hour.toString().padLeft(2, '0')}:'
            '${e.startDatetime.minute.toString().padLeft(2, '0')}';

  _PlanItem.fromRoutine(ChildRoutineModel r)
      : routine = r,
        event = null,
        _sortKey = r.timeLabel;

  bool get isRoutine => routine != null;
  String get sortKey => _sortKey;
}

// ─── Screen ───────────────────────────────────────────────────────────────────
class PlanScreen extends StatefulWidget {
  const PlanScreen({super.key});
  @override
  State<PlanScreen> createState() => _PlanScreenState();
}

class _PlanScreenState extends State<PlanScreen> {
  int _selectedDayOffset = 0;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final today = DateTime.now();
      setState(() => _selectedDayOffset = today.weekday - 1);
    });
  }

  void _openAddEvent(BuildContext context, DateTime selectedDay) {
    final calCtrl = context.read<CalendarController>();
    final childCtrl = context.read<ChildrenController>();
    if (calCtrl.familyId == null) return;

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => _AddEventSheet(
        familyId: calCtrl.familyId!,
        selectedDay: selectedDay,
        children: childCtrl.children,
        onCreate: (body) async {
          final ok = await calCtrl.createEvent(body);
          return ok != null;
        },
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    final calCtrl = context.watch<CalendarController>();
    final childrenCtrl = context.watch<ChildrenController>();
    final weatherCtrl = context.watch<WeatherController>();

    final weekStart = calCtrl.activeWeekStart;
    final days = List.generate(7, (i) => weekStart.add(Duration(days: i)));
    final selectedDay = days[_selectedDayOffset.clamp(0, 6)];

    final todayEvents = calCtrl.eventsForDay(selectedDay);
    final todayRoutines = childrenCtrl.routinesForDay(selectedDay);

    final items = [
      ...todayEvents.map(_PlanItem.fromEvent),
      ...todayRoutines.map(_PlanItem.fromRoutine),
    ]..sort((a, b) => a.sortKey.compareTo(b.sortKey));

    final months = strings.monthAbbr;
    final dayLabels = strings.dayInitials;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Column(
          children: [
            // Header
            Padding(
              padding: const EdgeInsets.fromLTRB(
                  AppSpacing.xl, AppSpacing.lg, AppSpacing.xl, 8),
              child: Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('${months[weekStart.month - 1]} ${weekStart.year}',
                            style: AppTextStyles.caption),
                        Text(strings.thisWeek,
                            style: AppTextStyles.titleLarge
                                .copyWith(fontSize: 24)),
                      ],
                    ),
                  ),
                  GestureDetector(
                    onTap: () => _openAddEvent(context, selectedDay),
                    child: Container(
                      width: 36,
                      height: 36,
                      decoration: BoxDecoration(
                        color: AppColors.card,
                        shape: BoxShape.circle,
                        border: Border.all(color: AppColors.line),
                      ),
                      child: const Icon(Icons.add,
                          size: 18, color: AppColors.primary),
                    ),
                  ),
                ],
              ),
            ),

            // Day picker
            Padding(
              padding: const EdgeInsets.fromLTRB(
                  AppSpacing.lg, 6, AppSpacing.lg, 0),
              child: Row(
                children: List.generate(7, (i) {
                  final day = days[i];
                  final active = i == _selectedDayOffset;
                  final isToday = _isSameDay(day, DateTime.now());
                  return Expanded(
                    child: GestureDetector(
                      onTap: () => setState(() => _selectedDayOffset = i),
                      child: AnimatedContainer(
                        duration: const Duration(milliseconds: 200),
                        padding: const EdgeInsets.symmetric(vertical: 8),
                        margin: const EdgeInsets.symmetric(horizontal: 2),
                        decoration: BoxDecoration(
                          color: active
                              ? AppColors.primary
                              : Colors.transparent,
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Column(
                          children: [
                            Text(dayLabels[i],
                                style: TextStyle(
                                    fontSize: 10,
                                    color: active
                                        ? Colors.white
                                            .withValues(alpha: 0.7)
                                        : AppColors.textSecondary)),
                            const SizedBox(height: 4),
                            Text('${day.day}',
                                style: TextStyle(
                                    fontSize: 16,
                                    fontWeight: FontWeight.w700,
                                    color: active
                                        ? Colors.white
                                        : (isToday
                                            ? AppColors.primary
                                            : AppColors
                                                .textSecondary))),
                            const SizedBox(height: 4),
                            Container(
                              width: 4,
                              height: 4,
                              decoration: BoxDecoration(
                                color: isToday && !active
                                    ? AppColors.primary
                                    : Colors.transparent,
                                shape: BoxShape.circle,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  );
                }),
              ),
            ),

            const SizedBox(height: 8),

            // Content
            Expanded(
              child: calCtrl.isLoading
                  ? const Center(
                      child: CircularProgressIndicator(
                          strokeWidth: 2, color: AppColors.primary))
                  : RefreshIndicator(
                      color: AppColors.primary,
                      onRefresh: () async {
                        final fid = calCtrl.familyId;
                        if (fid != null) await calCtrl.loadWeek(fid);
                      },
                      child: ListView(
                      padding: const EdgeInsets.fromLTRB(
                          AppSpacing.lg, 6, AppSpacing.lg, AppSpacing.lg),
                      children: [
                        if (weatherCtrl.weather != null)
                          _WeatherBanner(
                            temperatureLabel:
                                weatherCtrl.weather!.tempLabel,
                            condition: weatherCtrl.weather!.condition,
                            outdoorCount: todayEvents
                                .where((e) => e.isOutdoor)
                                .length,
                          ),
                        if (weatherCtrl.weather != null)
                          const SizedBox(height: 14),
                        if (items.isEmpty)
                          Padding(
                            padding: const EdgeInsets.only(top: 32),
                            child: Center(
                              child: Text(
                                strings.noEventsForDay,
                                style: AppTextStyles.bodySmall.copyWith(
                                  color: AppColors.textTertiary,
                                ),
                              ),
                            ),
                          )
                        else
                          ...items.map((item) => Padding(
                                padding: const EdgeInsets.only(bottom: 14),
                                child: item.isRoutine
                                    ? _RoutineRow(
                                        routine: item.routine!,
                                        childName: childrenCtrl.children
                                            .where((c) =>
                                                c.id == item.routine!.childId)
                                            .map((c) => c.displayName)
                                            .firstOrNull,
                                      )
                                    : _EventRow(
                                        event: item.event!,
                                        childName: childrenCtrl.children
                                            .where((c) =>
                                                c.id == item.event!.childId)
                                            .map((c) => c.displayName)
                                            .firstOrNull,
                                      ),
                              )),
                      ],
                    ),
                  ),
            ),
          ],
        ),
      ),
    );
  }

  static bool _isSameDay(DateTime a, DateTime b) =>
      a.year == b.year && a.month == b.month && a.day == b.day;
}

// ─── Add Event bottom sheet ───────────────────────────────────────────────────
class _AddEventSheet extends StatefulWidget {
  const _AddEventSheet({
    required this.familyId,
    required this.selectedDay,
    required this.children,
    required this.onCreate,
  });
  final int familyId;
  final DateTime selectedDay;
  final List<ChildModel> children;
  final Future<bool> Function(Map<String, dynamic> body) onCreate;

  @override
  State<_AddEventSheet> createState() => _AddEventSheetState();
}

class _AddEventSheetState extends State<_AddEventSheet> {
  final _titleCtrl = TextEditingController();
  int? _childId;
  TimeOfDay _startTime = const TimeOfDay(hour: 10, minute: 0);
  TimeOfDay _endTime = const TimeOfDay(hour: 11, minute: 0);
  final String _locationType = 'Indoor';
  final String _activityIntensity = 'Low';
  bool _weatherSensitive = false;
  int _reminderMinutes = 15;
  bool _saving = false;

  static const _reminderOptions = [5, 10, 15, 30, 60];

  @override
  void dispose() {
    _titleCtrl.dispose();
    super.dispose();
  }

  Future<void> _pickTime({required bool isStart}) async {
    final picked = await showTimePicker(
      context: context,
      initialTime: isStart ? _startTime : _endTime,
    );
    if (picked == null) return;
    setState(() {
      if (isStart) {
        _startTime = picked;
        if (_endTime.hour * 60 + _endTime.minute <=
            picked.hour * 60 + picked.minute) {
          _endTime = TimeOfDay(
              hour: (picked.hour + 1) % 24, minute: picked.minute);
        }
      } else {
        _endTime = picked;
      }
    });
  }

  Future<void> _save() async {
    if (_titleCtrl.text.trim().isEmpty) return;
    setState(() => _saving = true);

    final d = widget.selectedDay;
    DateTime toUtc(TimeOfDay t) =>
        DateTime(d.year, d.month, d.day, t.hour, t.minute).toUtc();

    final ok = await widget.onCreate({
      'childId': _childId,
      'title': _titleCtrl.text.trim(),
      'description': null,
      'startDatetime': toUtc(_startTime).toIso8601String(),
      'endDatetime': toUtc(_endTime).toIso8601String(),
      'locationLabel': null,
      'locationKey': null,
      'latitude': null,
      'longitude': null,
      'locationType': _locationType,
      'activityIntensity': _activityIntensity,
      'weatherSensitive': _weatherSensitive,
      'reminderMinutes': _reminderMinutes,
    });

    if (!mounted) return;
    if (ok) {
      Navigator.pop(context);
    } else {
      setState(() => _saving = false);
      ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Could not save event.')));
    }
  }


  @override
  Widget build(BuildContext context) {
    final isRu = context.read<AppSettingsController>().language ==
        AppLanguage.russian;

    return Padding(
      padding:
          EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
      child: Container(
        decoration: const BoxDecoration(
          color: AppColors.background,
          borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
        ),
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Center(
              child: Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                    color: AppColors.line,
                    borderRadius: BorderRadius.circular(2)),
              ),
            ),
            const SizedBox(height: 16),
            Text(isRu ? 'Новое событие' : 'New event',
                style: AppTextStyles.titleLarge.copyWith(fontSize: 18)),
            const SizedBox(height: 16),

            // Title
            TextField(
              controller: _titleCtrl,
              autofocus: true,
              style: AppTextStyles.bodyMedium,
              decoration: InputDecoration(
                hintText: isRu ? 'Название' : 'Title',
                hintStyle: AppTextStyles.bodyMedium
                    .copyWith(color: AppColors.textTertiary),
                filled: true,
                fillColor: AppColors.surface,
                border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(12),
                    borderSide: BorderSide.none),
                contentPadding: const EdgeInsets.symmetric(
                    horizontal: 14, vertical: 12),
              ),
            ),
            const SizedBox(height: 12),

            // Child picker
            if (widget.children.isNotEmpty) ...[
              DropdownButtonFormField<int?>(
                initialValue: _childId,
                decoration: InputDecoration(
                  hintText:
                      isRu ? 'Ребёнок (не обязательно)' : 'Child (optional)',
                  filled: true,
                  fillColor: AppColors.surface,
                  border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(12),
                      borderSide: BorderSide.none),
                  contentPadding: const EdgeInsets.symmetric(
                      horizontal: 14, vertical: 12),
                ),
                items: [
                  DropdownMenuItem(
                      value: null, child: Text(isRu ? 'Нет' : 'None')),
                  ...widget.children.map((c) => DropdownMenuItem(
                      value: c.id, child: Text(c.displayName))),
                ],
                onChanged: (v) => setState(() => _childId = v),
              ),
              const SizedBox(height: 12),
            ],

            // Time row
            Row(
              children: [
                Expanded(
                  child: GestureDetector(
                    onTap: () => _pickTime(isStart: true),
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 14, vertical: 12),
                      decoration: BoxDecoration(
                          color: AppColors.surface,
                          borderRadius: BorderRadius.circular(12)),
                      child: Row(
                        children: [
                          const Icon(Icons.access_time,
                              size: 16, color: AppColors.textTertiary),
                          const SizedBox(width: 8),
                          Text(_startTime.format(context),
                              style: AppTextStyles.bodyMedium),
                        ],
                      ),
                    ),
                  ),
                ),
                const Padding(
                  padding: EdgeInsets.symmetric(horizontal: 10),
                  child: Text('→',
                      style: TextStyle(color: AppColors.textSecondary)),
                ),
                Expanded(
                  child: GestureDetector(
                    onTap: () => _pickTime(isStart: false),
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 14, vertical: 12),
                      decoration: BoxDecoration(
                          color: AppColors.surface,
                          borderRadius: BorderRadius.circular(12)),
                      child: Row(
                        children: [
                          const Icon(Icons.access_time,
                              size: 16, color: AppColors.textTertiary),
                          const SizedBox(width: 8),
                          Text(_endTime.format(context),
                              style: AppTextStyles.bodyMedium),
                        ],
                      ),
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),

            // Reminder picker
            Text(isRu ? 'Напомнить за' : 'Remind before',
                style: AppTextStyles.bodySmall
                    .copyWith(color: AppColors.textSecondary)),
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              children: _reminderOptions.map((min) {
                final selected = _reminderMinutes == min;
                final label = min < 60
                    ? (isRu ? '$min мин' : '${min}m')
                    : (isRu ? '1 час' : '1h');
                return GestureDetector(
                  onTap: () => setState(() => _reminderMinutes = min),
                  child: Container(
                    padding: const EdgeInsets.symmetric(
                        horizontal: 14, vertical: 8),
                    decoration: BoxDecoration(
                      color: selected ? AppColors.primary : AppColors.surface,
                      borderRadius: BorderRadius.circular(20),
                    ),
                    child: Text(
                      label,
                      style: AppTextStyles.bodySmall.copyWith(
                        color: selected ? Colors.white : AppColors.textPrimary,
                        fontWeight: selected
                            ? FontWeight.w600
                            : FontWeight.normal,
                      ),
                    ),
                  ),
                );
              }).toList(),
            ),
            const SizedBox(height: 12),

            // Weather sensitive
            Row(
              children: [
                Text(isRu ? 'Зависит от погоды' : 'Weather sensitive',
                    style: AppTextStyles.bodySmall),
                const Spacer(),
                Switch.adaptive(
                  value: _weatherSensitive,
                  activeThumbColor: AppColors.primary,
                  onChanged: (v) => setState(() => _weatherSensitive = v),
                ),
              ],
            ),
            const SizedBox(height: 16),

            // Save button
            SizedBox(
              width: double.infinity,
              child: GestureDetector(
                onTap: _saving ? null : _save,
                child: Container(
                  height: 50,
                  decoration: BoxDecoration(
                      color: _saving
                          ? AppColors.lineStrong
                          : AppColors.primary,
                      borderRadius: BorderRadius.circular(14)),
                  alignment: Alignment.center,
                  child: _saving
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(
                              strokeWidth: 2, color: Colors.white))
                      : Text(isRu ? 'Сохранить' : 'Save',
                          style: const TextStyle(
                              color: Colors.white,
                              fontWeight: FontWeight.w700,
                              fontSize: 16)),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ─── Weather banner ───────────────────────────────────────────────────────────
class _WeatherBanner extends StatelessWidget {
  const _WeatherBanner({
    required this.temperatureLabel,
    required this.condition,
    required this.outdoorCount,
  });
  final String temperatureLabel;
  final String condition;
  final int outdoorCount;

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    final icon = switch (condition.toLowerCase()) {
      'rainy' => Icons.water_drop_outlined,
      'sunny' => Icons.wb_sunny_outlined,
      'snowy' => Icons.ac_unit_outlined,
      'storm' => Icons.thunderstorm_outlined,
      'windy' => Icons.air_outlined,
      _ => Icons.cloud_outlined,
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [Color(0xFFCFDCFF), AppColors.blueSoft],
        ),
        borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
      ),
      child: Row(
        children: [
          Icon(icon, size: 26, color: AppColors.blue2),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '$temperatureLabel · $condition',
                  style: AppTextStyles.labelLarge
                      .copyWith(color: AppColors.blueInk, fontSize: 13.5),
                ),
                Text(
                  outdoorCount == 0
                      ? strings.noOutdoorEvents
                      : '$outdoorCount outdoor event${outdoorCount > 1 ? 's' : ''} flagged',
                  style: AppTextStyles.caption
                      .copyWith(color: AppColors.textSecondary),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

// ─── Routine row ──────────────────────────────────────────────────────────────
class _RoutineRow extends StatelessWidget {
  const _RoutineRow({required this.routine, this.childName});
  final ChildRoutineModel routine;
  final String? childName;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 16),
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SizedBox(
              width: 52,
              child: Column(
                children: [
                  const SizedBox(height: 16),
                  Text(routine.timeLabel,
                      style: AppTextStyles.labelMedium.copyWith(
                          color: AppColors.textSecondary,
                          fontWeight: FontWeight.w700,
                          fontSize: 13)),
                ],
              ),
            ),
            SizedBox(
              width: 24,
              child: Column(
                children: [
                  Container(
                    width: 2,
                    height: 22,
                    color: AppColors.line,
                  ),
                  Container(
                    width: 12,
                    height: 12,
                    decoration: BoxDecoration(
                      color: AppColors.avatarGreen,
                      shape: BoxShape.circle,
                      border: Border.all(color: AppColors.background, width: 2.5),
                    ),
                  ),
                  Expanded(
                    child: Container(
                      width: 2,
                      color: AppColors.line,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: Container(
                padding: const EdgeInsets.fromLTRB(16, 14, 16, 14),
                decoration: BoxDecoration(
                  color: AppColors.card,
                  borderRadius: BorderRadius.circular(16),
                  boxShadow: [
                    BoxShadow(
                      color: AppColors.textPrimary.withValues(alpha: 0.04),
                      blurRadius: 10,
                      offset: const Offset(0, 4),
                    ),
                  ],
                  border: Border.all(color: AppColors.line.withValues(alpha: 0.5)),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(routine.title,
                        style: AppTextStyles.titleMedium.copyWith(fontSize: 15)),
                    if (childName != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 4),
                        child: Text(childName!,
                            style: AppTextStyles.caption.copyWith(
                                fontSize: 12,
                                fontWeight: FontWeight.w600,
                                color: AppColors.avatarGreen)),
                      ),
                    const SizedBox(height: 6),
                    Row(
                      children: [
                        const Icon(Icons.repeat, size: 13, color: AppColors.textTertiary),
                        const SizedBox(width: 4),
                        Text(routine.repeatPattern,
                            style: AppTextStyles.caption.copyWith(color: AppColors.textTertiary)),
                        if (routine.isOutdoor) ...[
                          const SizedBox(width: 10),
                          const Icon(Icons.park_outlined, size: 13, color: Color(0xFF0A6E44)),
                          const SizedBox(width: 4),
                          const Text('Outdoor',
                              style: TextStyle(fontSize: 11, color: Color(0xFF0A6E44), fontWeight: FontWeight.w500)),
                        ],
                      ],
                    ),
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

// ─── Event row ────────────────────────────────────────────────────────────────
class _EventRow extends StatelessWidget {
  const _EventRow({required this.event, this.childName});
  final CalendarEventModel event;
  final String? childName;

  Future<void> _confirmDelete(BuildContext context) async {
    final isRu = context.read<AppSettingsController>().language == AppLanguage.russian;
    final ok = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(isRu ? 'Удалить событие?' : 'Delete event?'),
        content: Text(event.title),
        actions: [
          TextButton(onPressed: () => dialogContext.pop(false),
              child: Text(isRu ? 'Отмена' : 'Cancel')),
          TextButton(onPressed: () => dialogContext.pop(true),
              child: Text(isRu ? 'Удалить' : 'Delete',
                  style: const TextStyle(color: AppColors.red))),
        ],
      ),
    );
    if (ok == true && context.mounted) {
      await context.read<CalendarController>().deleteEvent(event.id);
    }
  }

  @override
  Widget build(BuildContext context) {
    final accentColor =
        childName != null ? AppColors.avatarGold : AppColors.avatarViolet;

    return Padding(
      padding: const EdgeInsets.only(bottom: 16),
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SizedBox(
              width: 52,
              child: Column(
                children: [
                  const SizedBox(height: 16),
                  Text(event.timeLabel,
                      style: AppTextStyles.labelMedium.copyWith(
                          color: AppColors.textSecondary,
                          fontWeight: FontWeight.w700,
                          fontSize: 13)),
                ],
              ),
            ),
            SizedBox(
              width: 24,
              child: Column(
                children: [
                  Container(
                    width: 2,
                    height: 22,
                    color: AppColors.line,
                  ),
                  Container(
                    width: 12,
                    height: 12,
                    decoration: BoxDecoration(
                      color: accentColor,
                      shape: BoxShape.circle,
                      border: Border.all(color: AppColors.background, width: 2.5),
                    ),
                  ),
                  Expanded(
                    child: Container(
                      width: 2,
                      color: AppColors.line,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: Container(
                padding: const EdgeInsets.fromLTRB(16, 14, 16, 14),
                decoration: BoxDecoration(
                  color: AppColors.card,
                  borderRadius: BorderRadius.circular(16),
                  boxShadow: [
                    BoxShadow(
                      color: AppColors.textPrimary.withValues(alpha: 0.04),
                      blurRadius: 10,
                      offset: const Offset(0, 4),
                    ),
                  ],
                  border: Border.all(color: AppColors.line.withValues(alpha: 0.5)),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Expanded(
                          child: Text(event.title,
                              style: AppTextStyles.titleMedium.copyWith(fontSize: 15)),
                        ),
                        GestureDetector(
                          onTap: () => _confirmDelete(context),
                          child: const Padding(
                            padding: EdgeInsets.only(left: 8),
                            child: Icon(Icons.close, size: 18, color: AppColors.textTertiary),
                          ),
                        ),
                      ],
                    ),
                    if (event.locationLabel != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 6),
                        child: Row(
                          children: [
                            const Icon(Icons.location_on_outlined,
                                size: 14, color: AppColors.textTertiary),
                            const SizedBox(width: 6),
                            Text(event.locationLabel!,
                                style: AppTextStyles.bodySmall
                                    .copyWith(fontSize: 12, color: AppColors.textSecondary)),
                          ],
                        ),
                      ),
                    if (childName != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 6),
                        child: Text(childName!,
                            style: AppTextStyles.caption
                                .copyWith(fontSize: 12, fontWeight: FontWeight.w600, color: accentColor)),
                      ),
                    if (event.isOutdoor || event.weatherSensitive)
                      Padding(
                        padding: const EdgeInsets.only(top: 8),
                        child: Row(
                          children: [
                            if (event.isOutdoor) ...[
                              const Icon(Icons.park_outlined, size: 13, color: Color(0xFF0A6E44)),
                              const SizedBox(width: 4),
                              const Text('Outdoor',
                                  style: TextStyle(fontSize: 11, color: Color(0xFF0A6E44), fontWeight: FontWeight.w500)),
                            ],
                            if (event.isOutdoor && event.weatherSensitive)
                              const SizedBox(width: 10),
                            if (event.weatherSensitive) ...[
                              const Icon(Icons.wb_cloudy_outlined, size: 13, color: Color(0xFF7A5800)),
                              const SizedBox(width: 4),
                              const Text('weather-sensitive',
                                  style: TextStyle(fontSize: 11, color: Color(0xFF7A5800), fontWeight: FontWeight.w500)),
                            ],
                          ],
                        ),
                      ),
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

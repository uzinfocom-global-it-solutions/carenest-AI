import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_text_styles.dart';
import 'package:app/shared/theme/app_spacing.dart';
import 'package:app/shared/widgets/app_avatar.dart';
import 'package:app/shared/widgets/section_label.dart';
import 'package:app/features/auth/application/auth_controller.dart';
import 'package:app/features/children/application/children_controller.dart';
import 'package:app/features/settings/application/app_settings_controller.dart';
import 'package:app/features/monitoring/application/monitoring_controller.dart';
import 'package:app/core/localization/app_strings.dart';

class SettingsScreen extends StatelessWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(
                AppSpacing.xl,
                AppSpacing.xl,
                AppSpacing.xl,
                6,
              ),
              child: Text(
                strings.settingsTitle,
                style: AppTextStyles.display.copyWith(fontSize: 28),
              ),
            ),
            Expanded(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(
                  AppSpacing.lg,
                  AppSpacing.lg,
                  AppSpacing.lg,
                  AppSpacing.lg,
                ),
                children: [
                  const _ProfileCard(),
                  const SizedBox(height: 18),
                  Padding(
                    padding: const EdgeInsets.only(left: 8, bottom: 8),
                    child: SectionLabel(strings.sectionLanguage),
                  ),
                  _LanguageGroup(),
                  const SizedBox(height: 18),
                  Padding(
                    padding: const EdgeInsets.only(left: 8, bottom: 8),
                    child: SectionLabel(strings.sectionConversation),
                  ),
                  _InputModeGroup(),
                  const SizedBox(height: 18),
                  Padding(
                    padding: const EdgeInsets.only(left: 8, bottom: 8),
                    child: SectionLabel(
                      context.watch<AppSettingsController>().language == AppLanguage.russian
                          ? 'Уведомления'
                          : 'Notifications',
                    ),
                  ),
                  _NotificationsGroup(),
                  const SizedBox(height: 18),
                  Padding(
                    padding: const EdgeInsets.only(left: 8, bottom: 8),
                    child: SectionLabel(strings.sectionFamily),
                  ),
                  _FamilyGroup(),
                  const SizedBox(height: 18),
                  Padding(
                    padding: const EdgeInsets.only(left: 8, bottom: 8),
                    child: SectionLabel(strings.sectionAccount),
                  ),
                  Builder(
                    builder: (ctx) {
                      final s = AppStrings(ctx.watch<AppSettingsController>().language);
                      return _SettingsGroup(
                        children: [
                          _NavRow(
                            icon: Icons.logout,
                            label: s.signOut,
                            sub: s.signOutDescription,
                            trailing: const Icon(
                              Icons.chevron_right,
                              size: 16,
                              color: AppColors.textQuaternary,
                            ),
                            onTap: () => ctx.read<AuthController>().logout(),
                          ),
                        ],
                      );
                    },
                  ),
                  const SizedBox(height: 18),
                  Padding(
                    padding: const EdgeInsets.only(left: 8, bottom: 8),
                    child: SectionLabel(
                      context.watch<AppSettingsController>().language == AppLanguage.russian
                          ? 'Разработка'
                          : 'Developer',
                    ),
                  ),
                  const _AiTestModeCard(),
                  const SizedBox(height: 18),
                  Text(
                    'CareNestAI · ${strings.appSubtitle}',
                    textAlign: TextAlign.center,
                    style: AppTextStyles.caption.copyWith(
                      color: AppColors.textQuaternary,
                      height: 1.45,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ProfileCard extends StatelessWidget {
  const _ProfileCard();

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    final auth = context.watch<AuthController>();
    final name = (auth.displayName?.trim().isNotEmpty ?? false)
        ? auth.displayName!.trim()
        : (auth.email ?? strings.you);
    final initial = name.isEmpty ? '?' : name[0].toUpperCase();
    final sub = auth.email ?? '';

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
        border: Border.all(color: AppColors.line),
      ),
      child: Row(
        children: [
          AppAvatar(
            initial: initial,
            size: 48,
            childColor: ChildAvatarColor.ai,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  name,
                  style: AppTextStyles.titleSmall.copyWith(
                    fontWeight: FontWeight.w700,
                    fontSize: 16,
                  ),
                ),
                if (sub.isNotEmpty) ...[
                  const SizedBox(height: 2),
                  Text(
                    sub,
                    style: AppTextStyles.caption.copyWith(fontSize: 12),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _LanguageGroup extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final settings = context.watch<AppSettingsController>();
    final isRu = settings.language == AppLanguage.russian;
    return Container(
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
        border: Border.all(color: AppColors.line),
      ),
      padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
      child: Row(
        children: [
          _IconBox(icon: Icons.language_outlined),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  isRu ? 'Язык / Language' : 'Language / Язык',
                  style: AppTextStyles.labelLarge.copyWith(fontSize: 14.5),
                ),
                Text(
                  isRu ? 'Голос и ИИ — на русском' : 'Voice and AI — in English',
                  style: AppTextStyles.caption.copyWith(fontSize: 11.5),
                ),
              ],
            ),
          ),
          Container(
            padding: const EdgeInsets.all(3),
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(10),
            ),
            child: Row(
              children: [
                _LangChip(
                  label: 'EN',
                  active: !isRu,
                  onTap: () => settings.setLanguage(AppLanguage.english),
                ),
                _LangChip(
                  label: 'RU',
                  active: isRu,
                  onTap: () => settings.setLanguage(AppLanguage.russian),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _LangChip extends StatelessWidget {
  const _LangChip({required this.label, required this.active, required this.onTap});
  final String label;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 150),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
        decoration: BoxDecoration(
          color: active ? AppColors.primary : Colors.transparent,
          borderRadius: BorderRadius.circular(7),
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w700,
            color: active ? Colors.white : AppColors.textTertiary,
          ),
        ),
      ),
    );
  }
}

class _InputModeGroup extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    final settings = context.watch<AppSettingsController>();
    return Container(
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
        border: Border.all(color: AppColors.line),
      ),
      padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
      child: Row(
        children: [
          _IconBox(icon: Icons.mic_none_outlined),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  strings.defaultInput,
                  style: AppTextStyles.labelLarge.copyWith(fontSize: 14.5),
                ),
                Text(
                  strings.defaultInputDescription,
                  style: AppTextStyles.caption.copyWith(fontSize: 11.5),
                ),
              ],
            ),
          ),
          _ModeToggle(
            voice: settings.defaultInput == InputMode.voice,
            onVoice: () => settings.setDefaultInput(InputMode.voice),
            onText: () => settings.setDefaultInput(InputMode.text),
          ),
        ],
      ),
    );
  }
}

class _ModeToggle extends StatelessWidget {
  const _ModeToggle({
    required this.voice,
    required this.onVoice,
    required this.onText,
  });
  final bool voice;
  final VoidCallback onVoice;
  final VoidCallback onText;

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    return Container(
      padding: const EdgeInsets.all(3),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(10),
      ),
      child: Row(
        children: [
          _ModeChip(label: strings.modeVoice, active: voice, onTap: onVoice),
          _ModeChip(label: strings.modeText, active: !voice, onTap: onText),
        ],
      ),
    );
  }
}

class _ModeChip extends StatelessWidget {
  const _ModeChip({
    required this.label,
    required this.active,
    required this.onTap,
  });
  final String label;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 150),
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
        decoration: BoxDecoration(
          color: active ? AppColors.card : Colors.transparent,
          borderRadius: BorderRadius.circular(7),
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w600,
            color: active ? AppColors.textPrimary : AppColors.textTertiary,
          ),
        ),
      ),
    );
  }
}

class _NotificationsGroup extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final settings = context.watch<AppSettingsController>();
    final isRu = settings.language == AppLanguage.russian;
    return _SettingsGroup(
      children: [
        _ToggleRow(
          icon: Icons.notifications_outlined,
          label: isRu ? 'Push-уведомления' : 'Push Notifications',
          sub: isRu
              ? 'Напоминания, checklist, события'
              : 'Reminders, checklist, events',
          value: settings.notificationsEnabled,
          onChanged: settings.setNotificationsEnabled,
        ),
        Divider(height: 1, thickness: 0.5, indent: 52, color: AppColors.line),
        _ToggleRow(
          icon: Icons.record_voice_over_outlined,
          label: isRu ? 'Голосовой ИИ' : 'Voice AI',
          sub: isRu
              ? 'Озвучивать уведомления голосом'
              : 'Read notifications aloud with TTS',
          value: settings.notificationsEnabled && settings.voiceWithNotifications,
          onChanged: settings.notificationsEnabled
              ? settings.setVoiceWithNotifications
              : null,
        ),
      ],
    );
  }
}

class _ToggleRow extends StatelessWidget {
  const _ToggleRow({
    required this.icon,
    required this.label,
    required this.sub,
    required this.value,
    required this.onChanged,
  });
  final IconData icon;
  final String label;
  final String sub;
  final bool value;
  final Future<void> Function(bool)? onChanged;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(14, 10, 10, 10),
      child: Row(
        children: [
          _IconBox(icon: icon),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label,
                  style: AppTextStyles.labelLarge.copyWith(fontSize: 14.5),
                ),
                Text(
                  sub,
                  style: AppTextStyles.caption.copyWith(fontSize: 11.5),
                ),
              ],
            ),
          ),
          Switch(
            value: value,
            onChanged: onChanged != null ? (v) => onChanged!(v) : null,
            activeThumbColor: AppColors.primary,
            activeTrackColor: AppColors.primary.withValues(alpha: 0.4),
          ),
        ],
      ),
    );
  }
}

class _FamilyGroup extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    final count = context.watch<ChildrenController>().children.length;
    final sub = count == 0
        ? strings.noChildrenYetShort
        : count == 1
        ? '1 child'
        : '$count children';
    return _SettingsGroup(
      children: [
        _NavRow(
          icon: Icons.people_outline,
          label: strings.members,
          sub: sub,
          trailing: const Icon(
            Icons.chevron_right,
            size: 16,
            color: AppColors.textQuaternary,
          ),
          onTap: () => context.go('/family'),
        ),
      ],
    );
  }
}

class _SettingsGroup extends StatelessWidget {
  const _SettingsGroup({required this.children});
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
        border: Border.all(color: AppColors.line),
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(children: children),
    );
  }
}

class _NavRow extends StatelessWidget {
  const _NavRow({
    required this.icon,
    required this.label,
    required this.sub,
    required this.trailing,
    this.onTap,
  });
  final IconData icon;
  final String label;
  final String sub;
  final Widget trailing;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
        child: Row(
          children: [
            _IconBox(icon: icon),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    label,
                    style: AppTextStyles.labelLarge.copyWith(fontSize: 14.5),
                  ),
                  Text(
                    sub,
                    style: AppTextStyles.caption.copyWith(fontSize: 11.5),
                  ),
                ],
              ),
            ),
            trailing,
          ],
        ),
      ),
    );
  }
}

// ── AI Test Mode card ─────────────────────────────────────────────────────────
class _AiTestModeCard extends StatefulWidget {
  const _AiTestModeCard();

  @override
  State<_AiTestModeCard> createState() => _AiTestModeCardState();
}

class _AiTestModeCardState extends State<_AiTestModeCard> {
  int _interval = 5;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<MonitoringController>().refreshTestModeStatus();
    });
  }

  @override
  Widget build(BuildContext context) {
    final ctrl = context.watch<MonitoringController>();
    final isRu = context.read<AppSettingsController>().language == AppLanguage.russian;
    final enabled = ctrl.testModeEnabled;
    final loading = ctrl.testModeLoading;

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
        border: Border.all(
          color: enabled ? AppColors.primary.withValues(alpha: 0.5) : AppColors.line,
          width: enabled ? 1.5 : 1,
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 30,
                height: 30,
                decoration: BoxDecoration(
                  color: enabled ? AppColors.blueSoft : AppColors.surface,
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Icon(
                  Icons.science_outlined,
                  size: 16,
                  color: enabled ? AppColors.primary : AppColors.textTertiary,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      isRu ? 'AI Тест-режим' : 'AI Test Mode',
                      style: AppTextStyles.labelLarge.copyWith(fontSize: 14),
                    ),
                    Text(
                      enabled
                          ? (isRu
                              ? 'Активен · ${ctrl.testModeInterval}с · ${ctrl.testModeEventsPerMin.toStringAsFixed(1)} соб/мин'
                              : 'Active · ${ctrl.testModeInterval}s · ${ctrl.testModeEventsPerMin.toStringAsFixed(1)} ev/min')
                          : (isRu
                              ? 'Полный AI-пайплайн каждые N секунд'
                              : 'Full AI pipeline every N seconds'),
                      style: AppTextStyles.caption.copyWith(
                        color: enabled ? AppColors.primary : AppColors.textTertiary,
                      ),
                    ),
                  ],
                ),
              ),
              if (loading)
                const SizedBox(
                  width: 20,
                  height: 20,
                  child: CircularProgressIndicator(strokeWidth: 2, color: AppColors.primary),
                )
              else
                Switch.adaptive(
                  value: enabled,
                  activeThumbColor: AppColors.primary,
                  onChanged: (v) async {
                    if (v) {
                      final ok = await ctrl.startTestMode(_interval);
                      if (!ok && context.mounted) {
                        final err = ctrl.testModeError;
                        ScaffoldMessenger.of(context).showSnackBar(SnackBar(
                          content: Text(
                            err != null && err.isNotEmpty
                                ? err
                                : (isRu
                                    ? 'Не удалось включить тест-режим. Проверьте подключение к серверу.'
                                    : 'Failed to enable test mode. Check server connection.'),
                          ),
                          backgroundColor: Colors.red.shade700,
                          duration: const Duration(seconds: 4),
                        ));
                      }
                    } else {
                      await ctrl.stopTestMode();
                    }
                  },
                ),
            ],
          ),
          if (!enabled) ...[
            const SizedBox(height: 12),
            Row(
              children: [
                Text(
                  isRu ? 'Интервал:' : 'Interval:',
                  style: AppTextStyles.caption.copyWith(color: AppColors.textTertiary),
                ),
                const SizedBox(width: 10),
                ...([3, 5, 10, 30]).map((s) {
                  final active = _interval == s;
                  return GestureDetector(
                    onTap: () => setState(() => _interval = s),
                    child: Container(
                      margin: const EdgeInsets.only(right: 6),
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                      decoration: BoxDecoration(
                        color: active ? AppColors.primary : AppColors.surface,
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(
                          color: active ? AppColors.primary : AppColors.line,
                        ),
                      ),
                      child: Text(
                        '${s}s',
                        style: TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                          color: active ? Colors.white : AppColors.textSecondary,
                        ),
                      ),
                    ),
                  );
                }),
              ],
            ),
          ],
          if (enabled) ...[
            const SizedBox(height: 10),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
              decoration: BoxDecoration(
                color: AppColors.blueSoft,
                borderRadius: BorderRadius.circular(8),
              ),
              child: Row(
                children: [
                  const Icon(Icons.circle, size: 8, color: AppColors.primary),
                  const SizedBox(width: 6),
                  Text(
                    isRu
                        ? 'Пайплайн: контекст → риск → ИИ → приоритет → голос → SSE → память'
                        : 'Pipeline: context → risk → AI → priority → voice → SSE → memory',
                    style: AppTextStyles.caption.copyWith(
                      color: AppColors.primary,
                      fontSize: 10.5,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _IconBox extends StatelessWidget {
  const _IconBox({required this.icon});
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 30,
      height: 30,
      decoration: BoxDecoration(
        color: AppColors.blueSoft,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Icon(icon, size: 16, color: AppColors.blue2),
    );
  }
}

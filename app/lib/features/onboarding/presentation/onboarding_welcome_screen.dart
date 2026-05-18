import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:app/features/settings/application/app_settings_controller.dart';
import 'package:app/core/localization/app_strings.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_text_styles.dart';
import 'package:app/shared/theme/app_spacing.dart';
import 'package:app/shared/widgets/app_button.dart';

class OnboardingWelcomeScreen extends StatelessWidget {
  const OnboardingWelcomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final lang = context.watch<AppSettingsController>().language;
    final strings = AppStrings(lang);
    final welcomeSubtitle = lang == AppLanguage.russian
        ? 'CareNestAI следит за погодой, режимом ваших детей и мягко напоминает — чтобы вы не держали всё в голове.'
        : 'CareNestAI watches the weather, your kids\' routines, and gentle nudges — so you don\'t have to hold it all.';
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(
            AppSpacing.screenPadding + 8,
            AppSpacing.xxxxl,
            AppSpacing.screenPadding + 8,
            AppSpacing.xxl,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Container(
                      width: 88,
                      height: 88,
                      decoration: BoxDecoration(
                        gradient: const LinearGradient(
                          begin: Alignment.topLeft,
                          end: Alignment.bottomRight,
                          colors: [Color(0xFF5B8AFF), AppColors.primary],
                        ),
                        borderRadius: BorderRadius.circular(28),
                        boxShadow: [
                          BoxShadow(
                            color: AppColors.primary.withValues(alpha: 0.35),
                            blurRadius: 50,
                            offset: const Offset(0, 20),
                          ),
                        ],
                      ),
                      child: const Icon(
                        Icons.auto_awesome,
                        size: 42,
                        color: Colors.white,
                      ),
                    ),
                    const SizedBox(height: 32),
                    Text(
                      'A second pair of\neyes for your\nfamily.',
                      style: AppTextStyles.display.copyWith(
                        fontSize: 36,
                        height: 1.05,
                      ),
                    ),
                    const SizedBox(height: 14),
                    Text(
                      welcomeSubtitle,
                      style: AppTextStyles.bodyLarge.copyWith(
                        color: AppColors.textTertiary,
                        height: 1.4,
                      ),
                    ),
                  ],
                ),
              ),
              Column(
                children: [
                  AppButton(
                    label: strings.getStarted,
                    onPressed: () => context.go('/onboarding/family'),
                    icon: const Icon(
                      Icons.chevron_right,
                      color: Colors.white,
                      size: 18,
                    ),
                  ),
                  const SizedBox(height: 10),
                  AppButton(
                    label: strings.alreadyHaveAccountShort,
                    onPressed: () => context.go('/login'),
                    variant: AppButtonVariant.ghost,
                    height: 52,
                  ),
                  const SizedBox(height: 16),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Icon(
                        Icons.shield_outlined,
                        size: 14,
                        color: AppColors.textTertiary,
                      ),
                      const SizedBox(width: 6),
                      Text(
                        'Private by default · UNICEF-informed',
                        style: AppTextStyles.caption,
                      ),
                    ],
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

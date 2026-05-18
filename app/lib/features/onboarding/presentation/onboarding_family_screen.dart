import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:app/features/onboarding/application/onboarding_controller.dart';
import 'package:app/features/settings/application/app_settings_controller.dart';
import 'package:app/core/localization/app_strings.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_text_styles.dart';
import 'package:app/shared/theme/app_spacing.dart';
import 'package:app/shared/widgets/app_button.dart';
import 'package:app/shared/widgets/section_label.dart';

class OnboardingFamilyScreen extends StatefulWidget {
  const OnboardingFamilyScreen({super.key});

  @override
  State<OnboardingFamilyScreen> createState() => _OnboardingFamilyScreenState();
}

class _OnboardingFamilyScreenState extends State<OnboardingFamilyScreen> {
  final _nameController = TextEditingController();

  @override
  void dispose() {
    _nameController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final controller = context.read<OnboardingController>();
    final ok = await controller.createFamily(_nameController.text);
    if (!ok) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(controller.error ?? AppStrings(context.read<AppSettingsController>().language).couldNotCreateFamily),
          ),
        );
      }
      return;
    }
    if (mounted) context.go('/onboarding/kids');
  }

  @override
  Widget build(BuildContext context) {
    final isLoading = context.watch<OnboardingController>().isLoading;
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Column(
          children: [
            _Header(step: strings.welcome, onBack: () => context.go('/onboarding')),
            Expanded(
              child: SingleChildScrollView(
                padding: const EdgeInsets.fromLTRB(
                  AppSpacing.xxl,
                  8,
                  AppSpacing.xxl,
                  AppSpacing.xxl,
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const SizedBox(height: 14),
                    Text(
                      strings.setUpFamily,
                      style: AppTextStyles.titleLarge.copyWith(fontSize: 26),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      'A shared space for you, a co-parent, and your kids\' info.',
                      style: AppTextStyles.bodySmall.copyWith(
                        color: AppColors.textTertiary,
                        height: 1.4,
                      ),
                    ),
                    const SizedBox(height: 24),
                    SectionLabel(strings.familyName),
                    const SizedBox(height: 8),
                    _FocusedInput(
                      controller: _nameController,
                      hint: 'e.g. The Smith family',
                    ),
                    const SizedBox(height: 32),
                    AppButton(
                      label: strings.continueBtn,
                      onPressed: isLoading ? null : _submit,
                      isLoading: isLoading,
                      icon: const Icon(
                        Icons.chevron_right,
                        color: Colors.white,
                        size: 18,
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

class _FocusedInput extends StatelessWidget {
  const _FocusedInput({required this.controller, this.hint});
  final TextEditingController controller;
  final String? hint;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 52,
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
        border: Border.all(color: AppColors.primary, width: 1.5),
        boxShadow: [
          BoxShadow(
            color: AppColors.primary.withValues(alpha: 0.12),
            blurRadius: 0,
            spreadRadius: 4,
          ),
        ],
      ),
      alignment: Alignment.centerLeft,
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: TextField(
        controller: controller,
        style: AppTextStyles.bodyMedium.copyWith(fontWeight: FontWeight.w500),
        decoration: InputDecoration(
          border: InputBorder.none,
          isDense: true,
          hintText: hint,
          hintStyle: AppTextStyles.bodyMedium.copyWith(
            color: AppColors.textQuaternary,
          ),
        ),
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.step, required this.onBack});

  final String step;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        AppSpacing.xxl,
        AppSpacing.xl,
        AppSpacing.xxl,
        6,
      ),
      child: Row(
        children: [
          GestureDetector(
            onTap: onBack,
            child: const SizedBox(
              width: 36,
              height: 36,
              child: Icon(
                Icons.arrow_back_ios_new,
                size: 20,
                color: AppColors.textSecondary,
              ),
            ),
          ),
          Expanded(
            child: Center(child: Text(step, style: AppTextStyles.caption)),
          ),
          const SizedBox(width: 36),
        ],
      ),
    );
  }
}

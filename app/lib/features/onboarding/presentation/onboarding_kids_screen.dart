import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:app/features/auth/data/token_storage.dart';
import 'package:app/features/settings/application/app_settings_controller.dart';
import 'package:app/core/localization/app_strings.dart';
import 'package:app/features/children/application/children_controller.dart';
import 'package:app/features/children/domain/child_models.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_spacing.dart';
import 'package:app/shared/theme/app_text_styles.dart';
import 'package:app/shared/widgets/app_avatar.dart';
import 'package:app/shared/widgets/app_button.dart';
import 'package:app/shared/widgets/app_text_field.dart';

class OnboardingKidsScreen extends StatefulWidget {
  const OnboardingKidsScreen({super.key});

  @override
  State<OnboardingKidsScreen> createState() => _OnboardingKidsScreenState();
}

class _OnboardingKidsScreenState extends State<OnboardingKidsScreen> {
  final _nameController = TextEditingController();
  final _ageController = TextEditingController();
  int? _familyId;
  String? _localError;

  @override
  void initState() {
    super.initState();
    TokenStorage().getFamilyId().then((id) {
      if (mounted) setState(() => _familyId = id);
    });
    // Make sure the children list is fresh in case the user added some earlier.
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      final id = await TokenStorage().getFamilyId();
      if (id != null && mounted) {
        await context.read<ChildrenController>().loadForFamily(id);
      }
    });
  }

  @override
  void dispose() {
    _nameController.dispose();
    _ageController.dispose();
    super.dispose();
  }

  Future<void> _addChild() async {
    final name = _nameController.text.trim();
    final age = int.tryParse(_ageController.text.trim());
    if (name.isEmpty) {
      setState(() => _localError = AppStrings(context.read<AppSettingsController>().language).nameRequired);
      return;
    }
    if (age == null || age < 0 || age > 25) {
      setState(() => _localError = AppStrings(context.read<AppSettingsController>().language).invalidAge);
      return;
    }
    if (_familyId == null) {
      setState(() => _localError = AppStrings(context.read<AppSettingsController>().language).familyNotReady);
      return;
    }
    setState(() => _localError = null);

    final controller = context.read<ChildrenController>();
    final created = await controller.addChild(
      familyId: _familyId!,
      name: name,
      ageYears: age,
    );
    if (!mounted) return;
    if (created == null) {
      setState(() => _localError = controller.error ?? AppStrings(context.read<AppSettingsController>().language).couldNotAdd);
      return;
    }
    _nameController.clear();
    _ageController.clear();
  }

  @override
  Widget build(BuildContext context) {
    final ctrl = context.watch<ChildrenController>();
    final strings = AppStrings(context.watch<AppSettingsController>().language);

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Column(
          children: [
            _StepHeader(
              step: strings.step3of4,
              onBack: () => context.go('/onboarding/family'),
              onSkip: () => context.go('/home'),
            ),
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
                      strings.tellAboutKids,
                      style: AppTextStyles.titleLarge.copyWith(fontSize: 26),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      strings.onboardingVoiceHint,
                      style: AppTextStyles.bodySmall.copyWith(
                        color: AppColors.textTertiary,
                        height: 1.4,
                      ),
                    ),
                    const SizedBox(height: 18),
                    GestureDetector(
                      onTap: () => context.push('/voice'),
                      child: Container(
                        padding: const EdgeInsets.fromLTRB(16, 16, 16, 16),
                        decoration: BoxDecoration(
                          gradient: const LinearGradient(
                            begin: Alignment.topLeft,
                            end: Alignment.bottomRight,
                            colors: [Color(0xFFEFF3FF), Color(0xFFE3EAFF)],
                          ),
                          borderRadius:
                              BorderRadius.circular(AppSpacing.radiusLg),
                          border: Border.all(
                            color: AppColors.primary.withValues(alpha: 0.18),
                          ),
                        ),
                        child: Row(
                          children: [
                            Container(
                              width: 52,
                              height: 52,
                              decoration: BoxDecoration(
                                shape: BoxShape.circle,
                                gradient: const RadialGradient(
                                  colors: [
                                    Color(0xFF5B8AFF),
                                    AppColors.primary,
                                    Color(0xFF1D4ED8),
                                  ],
                                  stops: [0.0, 0.6, 1.0],
                                ),
                                boxShadow: [
                                  BoxShadow(
                                    color: AppColors.primary
                                        .withValues(alpha: 0.35),
                                    blurRadius: 14,
                                  ),
                                ],
                              ),
                              child: const Icon(
                                Icons.mic,
                                color: Colors.white,
                                size: 26,
                              ),
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    strings.onboardingJustTalk,
                                    style: AppTextStyles.labelLarge.copyWith(
                                      fontSize: 16,
                                    ),
                                  ),
                                  const SizedBox(height: 2),
                                  Text(
                                    strings.onboardingTellAboutKids,
                                    style: AppTextStyles.caption.copyWith(
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
                    const SizedBox(height: 18),
                    Row(
                      children: [
                        const Expanded(
                          child: Divider(color: AppColors.line, height: 1),
                        ),
                        Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 10),
                          child: Text(
                            strings.orAddManually,
                            style: AppTextStyles.caption.copyWith(
                              color: AppColors.textTertiary,
                            ),
                          ),
                        ),
                        const Expanded(
                          child: Divider(color: AppColors.line, height: 1),
                        ),
                      ],
                    ),
                    const SizedBox(height: 18),
                    AppTextField(
                      label: strings.name,
                      hint: 'e.g. Mila',
                      controller: _nameController,
                      textInputAction: TextInputAction.next,
                    ),
                    const SizedBox(height: 14),
                    AppTextField(
                      label: strings.ageInYears,
                      hint: 'e.g. 3',
                      controller: _ageController,
                      keyboardType: TextInputType.number,
                      onFieldSubmitted: (_) => _addChild(),
                    ),
                    if (_localError != null) ...[
                      const SizedBox(height: 10),
                      Text(
                        _localError!,
                        style: AppTextStyles.caption.copyWith(
                          color: AppColors.red,
                        ),
                      ),
                    ],
                    const SizedBox(height: 16),
                    AppButton(
                      label: strings.addChild,
                      onPressed: ctrl.isCreating ? null : _addChild,
                      isLoading: ctrl.isCreating,
                      variant: AppButtonVariant.outlined,
                      icon: const Icon(
                        Icons.add,
                        size: 18,
                        color: AppColors.primary,
                      ),
                    ),
                    if (ctrl.children.isNotEmpty) ...[
                      const SizedBox(height: 28),
                      Text(
                        strings.added,
                        style: AppTextStyles.labelSmall.copyWith(
                          color: AppColors.textTertiary,
                          letterSpacing: 0.6,
                        ),
                      ),
                      const SizedBox(height: 8),
                      ...ctrl.children.asMap().entries.map((e) {
                        final i = e.key;
                        final child = e.value;
                        return Padding(
                          padding: const EdgeInsets.only(bottom: 8),
                          child: _AddedChildRow(
                            child: child,
                            childColor: ChildAvatarColor.forIndex(i),
                          ),
                        );
                      }),
                    ],
                  ],
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(
                AppSpacing.xxl,
                0,
                AppSpacing.xxl,
                AppSpacing.xxl,
              ),
              child: AppButton(
                label: ctrl.children.isEmpty
                    ? strings.skipForNow
                    : 'That\'s everyone — continue',
                onPressed: () => context.go('/home'),
                icon: const Icon(
                  Icons.chevron_right,
                  size: 18,
                  color: Colors.white,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _AddedChildRow extends StatelessWidget {
  const _AddedChildRow({required this.child, required this.childColor});
  final ChildModel child;
  final ChildAvatarColor childColor;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppSpacing.radiusMd),
        border: Border.all(color: AppColors.line),
      ),
      child: Row(
        children: [
          AppAvatar(
            initial: child.displayName.isEmpty
                ? '?'
                : child.displayName[0].toUpperCase(),
            size: 36,
            childColor: childColor,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(child.displayName, style: AppTextStyles.labelLarge),
                Text(child.ageLabel, style: AppTextStyles.caption),
              ],
            ),
          ),
          const Icon(Icons.check_circle, color: AppColors.primary, size: 20),
        ],
      ),
    );
  }
}

class _StepHeader extends StatelessWidget {
  const _StepHeader({
    required this.step,
    required this.onBack,
    required this.onSkip,
  });
  final String step;
  final VoidCallback onBack;
  final VoidCallback onSkip;

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
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
          GestureDetector(
            onTap: onSkip,
            child: SizedBox(
              width: 44,
              height: 36,
              child: Center(
                child: Text(
                  strings.skip,
                  style: AppTextStyles.caption.copyWith(
                    color: AppColors.primary,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

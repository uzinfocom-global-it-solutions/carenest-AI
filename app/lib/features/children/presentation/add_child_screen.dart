import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:app/features/auth/data/token_storage.dart';
import 'package:app/features/children/application/children_controller.dart';
import 'package:app/features/recommendations/application/recommendations_controller.dart';
import 'package:app/features/settings/application/app_settings_controller.dart';
import 'package:app/core/localization/app_strings.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_spacing.dart';
import 'package:app/shared/theme/app_text_styles.dart';
import 'package:app/shared/widgets/app_button.dart';
import 'package:app/shared/widgets/app_text_field.dart';

class AddChildScreen extends StatefulWidget {
  const AddChildScreen({super.key});

  @override
  State<AddChildScreen> createState() => _AddChildScreenState();
}

class _AddChildScreenState extends State<AddChildScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _ageController = TextEditingController();

  @override
  void dispose() {
    _nameController.dispose();
    _ageController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;

    final name = _nameController.text.trim();
    final age = int.tryParse(_ageController.text.trim()) ?? 0;

    final familyId = await TokenStorage().getFamilyId();
    if (familyId == null) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(AppStrings(context.read<AppSettingsController>().language).createFamilyFirst)),
      );
      return;
    }

    if (!mounted) return;
    final controller = context.read<ChildrenController>();
    final created = await controller.addChild(
      familyId: familyId,
      name: name,
      ageYears: age,
    );

    if (!mounted) return;
    if (created == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(controller.error ?? AppStrings(context.read<AppSettingsController>().language).couldNotAddChild)),
      );
      return;
    }

    // Refresh recommendations so the new child shows up.
    await context
        .read<RecommendationsController>()
        .loadForChildren(controller.children);

    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text('${created.displayName} added to your family.')),
    );
    context.pop(created.id);
  }

  @override
  Widget build(BuildContext context) {
    final isCreating = context.watch<ChildrenController>().isCreating;
    final strings = AppStrings(context.watch<AppSettingsController>().language);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        backgroundColor: AppColors.background,
        elevation: 0,
        leading: IconButton(
          icon: const Icon(
            Icons.arrow_back_ios_new,
            size: 20,
            color: AppColors.textSecondary,
          ),
          onPressed: () => context.pop(),
        ),
        centerTitle: true,
        title: Text(strings.addChildTitle, style: AppTextStyles.labelLarge),
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(
            AppSpacing.xl,
            12,
            AppSpacing.xl,
            AppSpacing.xl,
          ),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  strings.tellAboutChild,
                  style: AppTextStyles.titleLarge.copyWith(fontSize: 24),
                ),
                const SizedBox(height: 8),
                Text(
                  strings.addChildDescription,
                  style: AppTextStyles.bodySmall.copyWith(
                    color: AppColors.textTertiary,
                    height: 1.4,
                  ),
                ),
                const SizedBox(height: 28),
                AppTextField(
                  label: strings.name,
                  hint: 'e.g. Mila',
                  controller: _nameController,
                  textInputAction: TextInputAction.next,
                  autofocus: true,
                  validator: (v) {
                    if (v == null || v.trim().isEmpty) return strings.nameIsRequired;
                    if (v.trim().length > 60) return strings.nameTooLongShort;
                    return null;
                  },
                ),
                const SizedBox(height: 18),
                AppTextField(
                  label: strings.ageInYears,
                  hint: 'e.g. 3',
                  controller: _ageController,
                  keyboardType: TextInputType.number,
                  textInputAction: TextInputAction.done,
                  onFieldSubmitted: (_) => _submit(),
                  validator: (v) {
                    final n = int.tryParse((v ?? '').trim());
                    if (n == null) return strings.enterAgeInYears;
                    if (n < 0 || n > 25) return strings.invalidAge;
                    return null;
                  },
                ),
                const SizedBox(height: 28),
                AppButton(
                  label: strings.addChildTitle,
                  onPressed: isCreating ? null : _submit,
                  isLoading: isCreating,
                  icon: const Icon(Icons.check, size: 18, color: Colors.white),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

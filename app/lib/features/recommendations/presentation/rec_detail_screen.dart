import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:app/features/settings/application/app_settings_controller.dart';
import 'package:app/core/localization/app_strings.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_text_styles.dart';
import 'package:app/shared/theme/app_spacing.dart';
import 'package:app/shared/widgets/app_button.dart';
import 'package:app/features/chats/application/chat_controller.dart';
import 'package:app/features/children/application/children_controller.dart';
import 'package:app/features/onboarding/domain/onboarding_models.dart';
import 'package:app/features/recommendations/application/recommendations_controller.dart';

class RecDetailScreen extends StatelessWidget {
  const RecDetailScreen({super.key, required this.recId});

  final String recId;

  @override
  Widget build(BuildContext context) {
    final recsCtrl = context.watch<RecommendationsController>();
    final childrenCtrl = context.watch<ChildrenController>();
    final strings = AppStrings(context.watch<AppSettingsController>().language);

    final id = int.tryParse(recId);
    final rec = id == null
        ? null
        : recsCtrl.recommendations.cast<RecommendationModel?>().firstWhere(
            (r) => r?.id == id,
            orElse: () => null,
          );

    final childName = rec == null
        ? ''
        : childrenCtrl.children
                  .where((c) => c.id == rec.childId)
                  .map((c) => c.displayName)
                  .firstOrNull ??
              '';

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(
                AppSpacing.lg,
                AppSpacing.lg,
                AppSpacing.lg,
                8,
              ),
              child: Row(
                children: [
                  GestureDetector(
                    onTap: () =>
                        context.canPop() ? context.pop() : context.go('/home'),
                    child: Container(
                      width: 36,
                      height: 36,
                      decoration: BoxDecoration(
                        color: AppColors.card,
                        shape: BoxShape.circle,
                        border: Border.all(color: AppColors.line),
                        boxShadow: [
                          BoxShadow(
                            color: Colors.black.withValues(alpha: 0.05),
                            blurRadius: 8,
                            offset: const Offset(0, 2),
                          ),
                        ],
                      ),
                      child: const Icon(
                        Icons.arrow_back_ios_new,
                        size: 16,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ),
                  Expanded(
                    child: Center(
                      child: Text(
                        strings.recommendation,
                        style: AppTextStyles.labelLarge,
                      ),
                    ),
                  ),
                  const SizedBox(width: 36),
                ],
              ),
            ),
            Expanded(
              child: rec == null
                  ? Center(
                      child: Text(
                        strings.recommendationNotFound,
                        style: AppTextStyles.bodyMedium.copyWith(
                          color: AppColors.textTertiary,
                        ),
                      ),
                    )
                  : SingleChildScrollView(
                      padding: const EdgeInsets.fromLTRB(
                        AppSpacing.lg,
                        8,
                        AppSpacing.lg,
                        AppSpacing.xl,
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          if (childName.isNotEmpty || rec.type.isNotEmpty)
                            Row(
                              children: [
                                Container(
                                  padding: const EdgeInsets.symmetric(
                                    horizontal: 10,
                                    vertical: 4,
                                  ),
                                  decoration: BoxDecoration(
                                    gradient: const LinearGradient(
                                      begin: Alignment.topLeft,
                                      end: Alignment.bottomRight,
                                      colors: [Color(0xFFD6E4FF), AppColors.blueSoft],
                                    ),
                                    borderRadius: BorderRadius.circular(8),
                                    boxShadow: [
                                      BoxShadow(
                                        color: AppColors.primary.withValues(alpha: 0.10),
                                        blurRadius: 6,
                                        offset: const Offset(0, 2),
                                      ),
                                    ],
                                  ),
                                  child: Text(
                                    rec.type.toUpperCase(),
                                    style: AppTextStyles.caption.copyWith(
                                      color: AppColors.blue2,
                                      fontWeight: FontWeight.w700,
                                      letterSpacing: 0.5,
                                    ),
                                  ),
                                ),
                                if (childName.isNotEmpty) ...[
                                  const SizedBox(width: 8),
                                  Text(
                                    'for $childName',
                                    style: AppTextStyles.caption.copyWith(
                                      color: AppColors.textTertiary,
                                    ),
                                  ),
                                ],
                              ],
                            ),
                          const SizedBox(height: 14),
                          Text(rec.title, style: AppTextStyles.titleLarge),
                          const SizedBox(height: 12),
                          Text(
                            rec.message,
                            style: AppTextStyles.bodyMedium.copyWith(
                              height: 1.5,
                              color: AppColors.textSecondary,
                            ),
                          ),
                          if (rec.reason != null && rec.reason!.isNotEmpty) ...[
                            const SizedBox(height: 18),
                            Container(
                              padding: const EdgeInsets.all(14),
                              decoration: BoxDecoration(
                                gradient: const LinearGradient(
                                  begin: Alignment.topLeft,
                                  end: Alignment.bottomRight,
                                  colors: [Color(0xFFFAFBFF), Color(0xFFF4F7FF)],
                                ),
                                borderRadius: BorderRadius.circular(
                                  AppSpacing.radiusMd,
                                ),
                                border: Border.all(color: AppColors.primary.withValues(alpha: 0.10)),
                                boxShadow: [
                                  BoxShadow(
                                    color: Colors.black.withValues(alpha: 0.04),
                                    blurRadius: 8,
                                    offset: const Offset(0, 2),
                                  ),
                                ],
                              ),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    strings.why,
                                    style: AppTextStyles.labelSmall.copyWith(
                                      color: AppColors.textTertiary,
                                      letterSpacing: 0.6,
                                    ),
                                  ),
                                  const SizedBox(height: 6),
                                  Text(
                                    rec.reason!,
                                    style: AppTextStyles.bodySmall.copyWith(
                                      color: AppColors.textSecondary,
                                      height: 1.5,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ],
                          const SizedBox(height: 18),
                          OutlinedButton.icon(
                            onPressed: () async {
                              final chatCtrl = context.read<ChatController>();
                              final question =
                                  'Why are you suggesting "${rec.title}" right now?';
                              context.go('/chat');
                              await Future<void>.delayed(
                                const Duration(milliseconds: 200),
                              );
                              await chatCtrl.sendMessage(question);
                            },
                            icon: const Icon(
                              Icons.chat_bubble_outline,
                              size: 16,
                            ),
                            label: Text(strings.askFollowUp),
                            style: OutlinedButton.styleFrom(
                              minimumSize: const Size(double.infinity, 48),
                              side: const BorderSide(color: AppColors.line),
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(
                                  AppSpacing.radiusMd,
                                ),
                              ),
                            ),
                          ),
                          const SizedBox(height: 14),
                          Row(
                            children: [
                              Expanded(
                                child: AppButton(
                                  label: strings.helpful,
                                  onPressed: () async {
                                    await recsCtrl.submitFeedback(
                                      rec.id,
                                      'Helpful',
                                    );
                                    if (context.mounted) {
                                      ScaffoldMessenger.of(
                                        context,
                                      ).showSnackBar(
                                        SnackBar(
                                          content: Text(
                                            strings.thanksFeedback,
                                          ),
                                        ),
                                      );
                                    }
                                  },
                                  icon: const Icon(
                                    Icons.thumb_up_outlined,
                                    color: Colors.white,
                                    size: 16,
                                  ),
                                ),
                              ),
                              const SizedBox(width: 10),
                              Expanded(
                                child: OutlinedButton.icon(
                                  onPressed: () async {
                                    await recsCtrl.submitFeedback(
                                      rec.id,
                                      'NotRelevant',
                                    );
                                    if (context.mounted) {
                                      ScaffoldMessenger.of(
                                        context,
                                      ).showSnackBar(
                                        SnackBar(
                                          content: Text(strings.gotItIllLearn),
                                        ),
                                      );
                                    }
                                  },
                                  icon: const Icon(
                                    Icons.thumb_down_outlined,
                                    size: 16,
                                  ),
                                  label: Text(strings.notRelevant),
                                  style: OutlinedButton.styleFrom(
                                    padding: const EdgeInsets.symmetric(
                                      vertical: 14,
                                    ),
                                    side: const BorderSide(
                                      color: AppColors.line,
                                    ),
                                    shape: RoundedRectangleBorder(
                                      borderRadius: BorderRadius.circular(
                                        AppSpacing.radiusMd,
                                      ),
                                    ),
                                  ),
                                ),
                              ),
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

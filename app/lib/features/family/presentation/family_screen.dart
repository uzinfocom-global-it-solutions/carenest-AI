import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_text_styles.dart';
import 'package:app/shared/theme/app_spacing.dart';
import 'package:app/shared/widgets/app_avatar.dart';
import 'package:app/features/children/application/children_controller.dart';
import 'package:app/features/children/domain/child_models.dart';
import 'package:app/core/localization/app_strings.dart';
import 'package:app/features/settings/application/app_settings_controller.dart';

class FamilyScreen extends StatefulWidget {
  const FamilyScreen({super.key});

  @override
  State<FamilyScreen> createState() => _FamilyScreenState();
}

class _FamilyScreenState extends State<FamilyScreen> {
  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    final childrenCtrl = context.watch<ChildrenController>();

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(
            AppSpacing.xl,
            AppSpacing.xl,
            AppSpacing.xl,
            0,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                strings.familyTitle,
                style: AppTextStyles.display.copyWith(fontSize: 28),
              ),
              const SizedBox(height: 20),

              // Children
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    strings.childrenSection,
                    style: AppTextStyles.labelSmall.copyWith(
                      color: AppColors.textTertiary,
                      letterSpacing: 0.6,
                    ),
                  ),
                  GestureDetector(
                    onTap: () async {
                      await context.push('/children/add');
                    },
                    child: Row(
                      children: [
                        const Icon(
                          Icons.add_circle_outline,
                          size: 16,
                          color: AppColors.primary,
                        ),
                        const SizedBox(width: 4),
                        Text(
                          strings.addChild,
                          style: TextStyle(
                            color: AppColors.primary,
                            fontWeight: FontWeight.w600,
                            fontSize: 13,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),

              if (childrenCtrl.isLoading)
                const Center(
                  child: Padding(
                    padding: EdgeInsets.all(16),
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: AppColors.primary,
                    ),
                  ),
                )
              else if (childrenCtrl.children.isEmpty)
                Expanded(
                  child: Center(
                    child: Padding(
                      padding: const EdgeInsets.all(24),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Container(
                            width: 80,
                            height: 80,
                            decoration: BoxDecoration(
                              color: AppColors.blueSoft,
                              shape: BoxShape.circle,
                            ),
                            child: const Icon(Icons.child_care,
                                size: 36, color: AppColors.primary),
                          ),
                          const SizedBox(height: 18),
                          Text(strings.noChildrenYet,
                              style: AppTextStyles.titleLarge
                                  .copyWith(fontSize: 18)),
                          const SizedBox(height: 6),
                          Text(
                            strings.addChildDescription,
                            textAlign: TextAlign.center,
                            style: AppTextStyles.bodySmall.copyWith(
                              color: AppColors.textTertiary,
                              height: 1.45,
                            ),
                          ),
                          const SizedBox(height: 20),
                          GestureDetector(
                            onTap: () => context.push('/children/add'),
                            child: Container(
                              padding: const EdgeInsets.symmetric(
                                  horizontal: 18, vertical: 12),
                              decoration: BoxDecoration(
                                color: AppColors.primary,
                                borderRadius: BorderRadius.circular(
                                    AppSpacing.radiusMd),
                              ),
                              child: Row(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  const Icon(Icons.add,
                                      color: Colors.white, size: 18),
                                  const SizedBox(width: 8),
                                  Text(strings.addFirstChild,
                                      style: const TextStyle(
                                          color: Colors.white,
                                          fontWeight: FontWeight.w600,
                                          fontSize: 14.5)),
                                ],
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                )
              else
                Expanded(
                  child: RefreshIndicator(
                    color: AppColors.primary,
                    onRefresh: () async {
                      final fid = childrenCtrl.familyId;
                      if (fid != null) {
                        await childrenCtrl.loadForFamily(fid);
                      }
                    },
                    child: ListView(
                      padding: EdgeInsets.zero,
                      children: childrenCtrl.children
                          .asMap()
                          .entries
                          .map((entry) {
                        final i = entry.key;
                        final child = entry.value;
                        final routineCount = childrenCtrl.routines
                            .where((r) => r.childId == child.id)
                            .length;
                        return Padding(
                          padding: const EdgeInsets.only(bottom: 8),
                          child: _ChildCard(
                            child: child,
                            childColor: ChildAvatarColor.forIndex(i),
                            routineCount: routineCount,
                            onTap: () => context.push('/child/${child.id}'),
                          ),
                        );
                      }).toList(),
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ChildCard extends StatelessWidget {
  const _ChildCard({
    required this.child,
    required this.childColor,
    required this.onTap,
    this.routineCount = 0,
  });
  final ChildModel child;
  final ChildAvatarColor childColor;
  final VoidCallback onTap;
  final int routineCount;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: AppColors.card,
          borderRadius: BorderRadius.circular(AppSpacing.radiusLg),
          border: Border.all(color: AppColors.line),
        ),
        child: Row(
          children: [
            AppAvatar(
              initial: child.displayName.isEmpty
                  ? '?'
                  : child.displayName[0].toUpperCase(),
              size: 44,
              childColor: childColor,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(child.displayName, style: AppTextStyles.labelLarge),
                  const SizedBox(height: 2),
                  Row(
                    children: [
                      Text(
                        child.ageLabel,
                        style: AppTextStyles.caption,
                      ),
                      if (routineCount > 0) ...[
                        const SizedBox(width: 8),
                        Container(
                          padding: const EdgeInsets.symmetric(
                              horizontal: 6, vertical: 2),
                          decoration: BoxDecoration(
                            color: AppColors.greenSoft,
                            borderRadius: BorderRadius.circular(6),
                          ),
                          child: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              const Icon(Icons.alarm,
                                  size: 10, color: Color(0xFF0A6E44)),
                              const SizedBox(width: 3),
                              Text(
                                '$routineCount',
                                style: const TextStyle(
                                    fontSize: 10.5,
                                    fontWeight: FontWeight.w700,
                                    color: Color(0xFF0A6E44)),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ],
                  ),
                ],
              ),
            ),
            const Icon(
              Icons.chevron_right,
              size: 18,
              color: AppColors.textQuaternary,
            ),
          ],
        ),
      ),
    );
  }
}

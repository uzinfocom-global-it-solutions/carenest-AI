import 'package:flutter/material.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_spacing.dart';

class AppCard extends StatelessWidget {
  const AppCard({
    super.key,
    required this.child,
    this.padding,
    this.onTap,
    this.borderRadius = AppSpacing.radiusLg,
    this.color = AppColors.card,
    this.borderColor = AppColors.line,
    this.elevation = 0,
    this.shadow = true,
  });

  final Widget child;
  final EdgeInsetsGeometry? padding;
  final VoidCallback? onTap;
  final double borderRadius;
  final Color color;
  final Color borderColor;
  final double elevation;
  final bool shadow;

  @override
  Widget build(BuildContext context) {
    final shadowList = shadow && elevation == 0
        ? [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.04),
              blurRadius: 10,
              offset: const Offset(0, 3),
            ),
          ]
        : null;

    if (onTap != null) {
      return Container(
        decoration: BoxDecoration(
          color: color,
          borderRadius: BorderRadius.circular(borderRadius),
          border: Border.all(color: borderColor),
          boxShadow: shadowList,
        ),
        clipBehavior: Clip.antiAlias,
        child: Material(
          color: Colors.transparent,
          child: InkWell(
            onTap: onTap,
            child: Padding(
              padding: padding ?? const EdgeInsets.all(AppSpacing.cardPadding),
              child: child,
            ),
          ),
        ),
      );
    }

    return Container(
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(borderRadius),
        border: Border.all(color: borderColor),
        boxShadow: shadowList,
      ),
      child: Padding(
        padding: padding ?? const EdgeInsets.all(AppSpacing.cardPadding),
        child: child,
      ),
    );
  }
}

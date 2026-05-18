import 'package:flutter/material.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_text_styles.dart';

enum AppButtonVariant { filled, outlined, ghost }

class AppButton extends StatelessWidget {
  const AppButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.variant = AppButtonVariant.filled,
    this.isLoading = false,
    this.icon,
    this.height = 52,
    this.width = double.infinity,
    this.borderRadius = 14,
  });

  final String label;
  final VoidCallback? onPressed;
  final AppButtonVariant variant;
  final bool isLoading;
  final Widget? icon;
  final double height;
  final double? width;
  final double borderRadius;

  @override
  Widget build(BuildContext context) {
    final child = isLoading
        ? SizedBox(
            width: 20,
            height: 20,
            child: CircularProgressIndicator(
              strokeWidth: 2,
              color: variant == AppButtonVariant.filled
                  ? Colors.white
                  : AppColors.primary,
            ),
          )
        : Row(
            mainAxisSize: MainAxisSize.min,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              if (icon != null) ...[icon!, const SizedBox(width: 8)],
              Text(label),
            ],
          );

    final shape = RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(borderRadius),
    );

    Widget button;
    switch (variant) {
      case AppButtonVariant.filled:
        button = ElevatedButton(
          onPressed: isLoading ? null : onPressed,
          style: ElevatedButton.styleFrom(
            backgroundColor: AppColors.primary,
            foregroundColor: AppColors.textOnPrimary,
            elevation: 0,
            minimumSize: Size(width ?? double.infinity, height),
            shape: shape,
            textStyle: AppTextStyles.buttonLarge,
          ),
          child: child,
        );
      case AppButtonVariant.outlined:
        button = OutlinedButton(
          onPressed: isLoading ? null : onPressed,
          style: OutlinedButton.styleFrom(
            foregroundColor: AppColors.primary,
            side: const BorderSide(color: AppColors.primary),
            minimumSize: Size(width ?? double.infinity, height),
            shape: shape,
            textStyle: AppTextStyles.buttonLarge.copyWith(
              color: AppColors.primary,
            ),
          ),
          child: child,
        );
      case AppButtonVariant.ghost:
        button = TextButton(
          onPressed: isLoading ? null : onPressed,
          style: TextButton.styleFrom(
            foregroundColor: AppColors.primary,
            minimumSize: Size(width ?? double.infinity, height),
            shape: shape,
            textStyle: AppTextStyles.buttonLarge.copyWith(
              color: AppColors.primary,
            ),
          ),
          child: child,
        );
    }

    if (width != null) {
      return SizedBox(width: width, height: height, child: button);
    }
    return SizedBox(height: height, child: button);
  }
}

import 'package:flutter/material.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_spacing.dart';

enum ChildAvatarColor {
  gold,
  violet,
  ai;

  /// Stable rotation of avatar colors by index so kids in a list each get a
  /// distinct, repeating palette without per-child color storage.
  static ChildAvatarColor forIndex(int index) =>
      index.isEven ? ChildAvatarColor.gold : ChildAvatarColor.violet;
}

class AppAvatar extends StatelessWidget {
  const AppAvatar({
    super.key,
    required this.initial,
    this.size = 40,
    this.backgroundColor,
    this.textColor,
    this.childColor,
  });

  const AppAvatar.child({
    super.key,
    required this.initial,
    required this.childColor,
    this.size = 40,
    this.backgroundColor,
    this.textColor,
  });

  final String initial;
  final double size;
  final Color? backgroundColor;
  final Color? textColor;
  final ChildAvatarColor? childColor;

  Color get _bg {
    if (backgroundColor != null) return backgroundColor!;
    return switch (childColor) {
      ChildAvatarColor.gold => AppColors.avatarGold,
      ChildAvatarColor.violet => AppColors.avatarViolet,
      ChildAvatarColor.ai => AppColors.avatarAi,
      null => AppColors.surface,
    };
  }

  Color get _fg {
    if (textColor != null) return textColor!;
    return switch (childColor) {
      ChildAvatarColor.gold => AppColors.avatarGoldText,
      ChildAvatarColor.violet => AppColors.avatarVioletText,
      ChildAvatarColor.ai => AppColors.textOnPrimary,
      null => AppColors.textPrimary,
    };
  }

  @override
  Widget build(BuildContext context) {
    final fontSize = size * 0.38;
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: _bg,
        borderRadius: BorderRadius.circular(AppSpacing.radiusFull),
      ),
      alignment: Alignment.center,
      child: Text(
        initial.toUpperCase(),
        style: TextStyle(
          fontSize: fontSize,
          fontWeight: FontWeight.w700,
          color: _fg,
          height: 1,
        ),
      ),
    );
  }
}

class AiAvatar extends StatelessWidget {
  const AiAvatar({super.key, this.size = 36});

  final double size;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xFF5B8AFF), AppColors.primary],
        ),
        borderRadius: BorderRadius.circular(size / 2),
        boxShadow: [
          BoxShadow(
            color: AppColors.primary.withValues(alpha: 0.3),
            blurRadius: 12,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      alignment: Alignment.center,
      child: Icon(Icons.auto_awesome, size: size * 0.45, color: Colors.white),
    );
  }
}

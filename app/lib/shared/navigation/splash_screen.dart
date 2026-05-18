import 'package:flutter/material.dart';
import 'package:app/shared/theme/app_colors.dart';

/// Shown while the auth bootstrap asks the backend whether the signed-in user
/// already has a family. The router holds here instead of flashing the
/// onboarding flow (which would scare returning users) until /families/mine
/// answers.
class SplashScreen extends StatelessWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      backgroundColor: AppColors.background,
      body: Center(
        child: SizedBox(
          width: 28,
          height: 28,
          child: CircularProgressIndicator(
            color: AppColors.primary,
            strokeWidth: 2.5,
          ),
        ),
      ),
    );
  }
}

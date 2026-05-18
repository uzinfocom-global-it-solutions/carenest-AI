import 'package:flutter/material.dart';

abstract final class AppColors {
  // Surfaces (from tokens.css)
  static const Color background = Color(0xFFFFFFFF); // --p-bg
  static const Color backgroundElevated = Color(0xFFFAFAFB); // --p-bg-elev
  static const Color surface = Color(0xFFF5F5F7); // --p-surface
  static const Color surface2 = Color(0xFFECECEF); // --p-surface-2
  static const Color card = Color(0xFFFFFFFF); // --p-card

  // Text
  static const Color ink = Color(0xFF0A0A0C); // --p-ink
  static const Color ink2 = Color(0xFF3A3A40); // --p-ink-2
  static const Color ink3 = Color(0xFF6E6E75); // --p-ink-3
  static const Color ink4 = Color(0xFFA1A1A8); // --p-ink-4

  // Borders
  static const Color line = Color(0x12000000); // rgba(0,0,0,0.07)
  static const Color lineStrong = Color(0x24000000); // rgba(0,0,0,0.14)

  // Brand blue
  static const Color blue = Color(0xFF2563FF); // --p-blue
  static const Color blue2 = Color(0xFF1D4ED8); // --p-blue-2
  static const Color blueSoft = Color(0xFFE8EFFF); // --p-blue-soft
  static const Color blueInk = Color(0xFF0A2A6B); // --p-blue-ink

  // Accents
  static const Color green = Color(0xFF1AAA6B); // --p-green
  static const Color greenSoft = Color(0xFFE3F6ED); // --p-green-soft
  static const Color amber = Color(0xFFE0A400); // --p-amber
  static const Color amberSoft = Color(0xFFFBF3DA); // --p-amber-soft
  static const Color red = Color(0xFFE0392C); // --p-red
  static const Color redSoft = Color(0xFFFBE5E3); // --p-red-soft
  static const Color violet = Color(0xFF7C5CFF); // --p-violet
  static const Color violetSoft = Color(0xFFECE7FF); // --p-violet-soft

  // Semantic aliases
  static const Color primary = blue;
  static const Color primaryDark = blue2;
  static const Color primarySoft = blueSoft;
  static const Color textPrimary = ink;
  static const Color textSecondary = ink2;
  static const Color textTertiary = ink3;
  static const Color textQuaternary = ink4;
  static const Color textOnPrimary = Color(0xFFFFFFFF);
  static const Color border = line;
  static const Color divider = line;

  // Child avatar palette (neutral by-index, not name-bound)
  static const Color avatarGold = Color(0xFFF4CF6E);
  static const Color avatarGoldText = Color(0xFF5B4400);
  static const Color avatarViolet = Color(0xFFA09EFE);
  static const Color avatarVioletText = Color(0xFF1F1665);
  static const Color avatarGreen = Color(0xFF1AAA6B);
  static const Color avatarAi = blue;

  // Nav
  static const Color navActive = blue;
  static const Color navInactive = ink4;
  static const Color navBackground = Color(0xFFFFFFFF);
}

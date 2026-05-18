import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:app/core/localization/app_strings.dart';
import 'package:app/features/settings/application/app_settings_controller.dart';
import 'package:app/shared/theme/app_colors.dart';
import 'package:app/shared/theme/app_spacing.dart';

enum NavTab { home, plan, voice, chat, family, settings }

class AppBottomNavBar extends StatelessWidget {
  const AppBottomNavBar({
    super.key,
    required this.currentTab,
    required this.onTabSelected,
  });

  final NavTab currentTab;
  final ValueChanged<NavTab> onTabSelected;

  static const _items = [
    _NavItem(
      tab: NavTab.home,
      icon: Icons.home_outlined,
      activeIcon: Icons.home,
      label: 'Home',
    ),
    _NavItem(
      tab: NavTab.plan,
      icon: Icons.calendar_month_outlined,
      activeIcon: Icons.calendar_month,
      label: 'Plan',
    ),
    _NavItem(
      tab: NavTab.voice,
      icon: Icons.mic_none,
      activeIcon: Icons.mic,
      label: 'Talk',
      isAccent: true,
    ),
    _NavItem(
      tab: NavTab.family,
      icon: Icons.people_outline,
      activeIcon: Icons.people,
      label: 'Family',
    ),
    _NavItem(
      tab: NavTab.settings,
      icon: Icons.settings_outlined,
      activeIcon: Icons.settings,
      label: 'Settings',
    ),
  ];

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    final labels = {
      NavTab.home: strings.navHome,
      NavTab.plan: strings.navPlan,
      NavTab.voice: strings.navTalk,
      NavTab.chat: strings.navChat,
      NavTab.family: strings.navFamily,
      NavTab.settings: strings.navSettings,
    };
    return Container(
      height: AppSpacing.navBarHeight,
      decoration: const BoxDecoration(
        color: AppColors.navBackground,
        border: Border(top: BorderSide(color: AppColors.line)),
      ),
      child: SafeArea(
        top: false,
        child: Row(
          children: _items.map((item) {
            final isActive = item.tab == currentTab;
            if (item.isAccent) {
              return Expanded(
                child: GestureDetector(
                  behavior: HitTestBehavior.opaque,
                  onTap: () => onTabSelected(item.tab),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Container(
                        width: 44,
                        height: 44,
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
                              color: AppColors.primary.withValues(alpha: 0.45),
                              blurRadius: 12,
                              offset: const Offset(0, 4),
                            ),
                          ],
                        ),
                        child: Icon(
                          item.activeIcon,
                          size: 22,
                          color: Colors.white,
                        ),
                      ),
                    ],
                  ),
                ),
              );
            }
            return Expanded(
              child: GestureDetector(
                behavior: HitTestBehavior.opaque,
                onTap: () => onTabSelected(item.tab),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(
                      isActive ? item.activeIcon : item.icon,
                      size: 22,
                      color: isActive
                          ? AppColors.navActive
                          : AppColors.navInactive,
                    ),
                    const SizedBox(height: 3),
                    Text(
                      labels[item.tab]!,
                      style: TextStyle(
                        fontSize: 10,
                        fontWeight: isActive
                            ? FontWeight.w500
                            : FontWeight.w400,
                        color: isActive
                            ? AppColors.navActive
                            : AppColors.navInactive,
                      ),
                    ),
                  ],
                ),
              ),
            );
          }).toList(),
        ),
      ),
    );
  }
}

class _NavItem {
  const _NavItem({
    required this.tab,
    required this.icon,
    required this.activeIcon,
    required this.label,
    this.isAccent = false,
  });

  final NavTab tab;
  final IconData icon;
  final IconData activeIcon;
  final String label;
  final bool isAccent;
}

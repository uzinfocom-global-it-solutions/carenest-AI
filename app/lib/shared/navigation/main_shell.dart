import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:app/shared/widgets/bottom_nav_bar.dart';

class MainShell extends StatelessWidget {
  const MainShell({super.key, required this.child});

  final Widget child;

  static const _tabPaths = {
    NavTab.home: '/home',
    NavTab.plan: '/plan',
    NavTab.chat: '/chat',
    NavTab.family: '/family',
    NavTab.settings: '/settings',
  };

  NavTab _currentTab(BuildContext context) {
    final location = GoRouterState.of(context).matchedLocation;
    if (location.startsWith('/plan')) return NavTab.plan;
    if (location.startsWith('/chat')) return NavTab.chat;
    if (location.startsWith('/family')) return NavTab.family;
    if (location.startsWith('/settings')) return NavTab.settings;
    return NavTab.home;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: child,
      bottomNavigationBar: AppBottomNavBar(
        currentTab: _currentTab(context),
        onTabSelected: (tab) {
          if (tab == NavTab.voice) {
            context.push('/voice');
            return;
          }
          final path = _tabPaths[tab]!;
          context.go(path);
        },
      ),
    );
  }
}

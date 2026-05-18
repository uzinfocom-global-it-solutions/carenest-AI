import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:app/features/auth/application/auth_controller.dart';
import 'package:app/features/auth/presentation/login_screen.dart';
import 'package:app/features/auth/presentation/register_screen.dart';
import 'package:app/features/onboarding/presentation/onboarding_welcome_screen.dart';
import 'package:app/features/onboarding/presentation/onboarding_family_screen.dart';
import 'package:app/features/onboarding/presentation/onboarding_kids_screen.dart';
import 'package:app/features/chats/presentation/chat_screen.dart';
import 'package:app/features/home/presentation/home_screen.dart';
import 'package:app/features/plan/presentation/plan_screen.dart';
import 'package:app/features/family/presentation/family_screen.dart';
import 'package:app/features/settings/presentation/settings_screen.dart';
import 'package:app/features/voice/presentation/voice_modal.dart';
import 'package:app/features/child_profile/presentation/child_profile_screen.dart';
import 'package:app/features/children/presentation/add_child_screen.dart';
import 'package:app/features/recommendations/presentation/rec_detail_screen.dart';
import 'package:app/features/notifications/presentation/notifications_screen.dart';
import 'package:app/features/notifications/presentation/notification_center_screen.dart';
import 'package:app/features/monitoring/presentation/active_monitoring_screen.dart';
import 'package:app/features/monitoring/presentation/family_risk_dashboard.dart';
import 'package:app/features/monitoring/presentation/escalation_timeline_screen.dart';
import 'main_shell.dart';
import 'splash_screen.dart';

final _rootKey = GlobalKey<NavigatorState>();
final _shellKey = GlobalKey<NavigatorState>();

GoRouter buildRouter(AuthController auth) {
  return GoRouter(
    navigatorKey: _rootKey,
    refreshListenable: auth,
    redirect: (context, state) {
      if (auth.isLoading) return null;

      final isLoggedIn = auth.isAuthenticated;
      final hasFamily = auth.hasFamily;
      final familyResolved = auth.familyResolved;
      final loc = state.matchedLocation;
      final isOnboarding = loc.startsWith('/onboarding');
      final isSplash = loc == '/splash';
      final isAuth = loc == '/login' || loc == '/register';

      if (!isLoggedIn) {
        return isAuth ? null : '/login';
      }

      if (!familyResolved) {
        return isSplash ? null : '/splash';
      }

      if (isAuth || isSplash) {
        return hasFamily ? '/home' : '/onboarding';
      }
      if (!hasFamily && !isOnboarding) return '/onboarding';
      if (hasFamily && isOnboarding) return '/home';
      return null;
    },
    initialLocation: '/login',
    routes: [
      GoRoute(path: '/splash', builder: (_, _) => const SplashScreen()),
      GoRoute(path: '/login', builder: (_, _) => const LoginScreen()),
      GoRoute(path: '/register', builder: (_, _) => const RegisterScreen()),
      GoRoute(
        path: '/onboarding',
        builder: (_, _) => const OnboardingWelcomeScreen(),
        routes: [
          GoRoute(
            path: 'family',
            builder: (_, _) => const OnboardingFamilyScreen(),
          ),
          GoRoute(
            path: 'kids',
            builder: (_, _) => const OnboardingKidsScreen(),
          ),
        ],
      ),
      ShellRoute(
        navigatorKey: _shellKey,
        builder: (context, state, child) => MainShell(child: child),
        routes: [
          GoRoute(path: '/home', builder: (_, _) => const HomeScreen()),
          GoRoute(path: '/plan', builder: (_, _) => const PlanScreen()),
          GoRoute(path: '/chat', builder: (_, _) => const ChatScreen()),
          GoRoute(path: '/family', builder: (_, _) => const FamilyScreen()),
          GoRoute(path: '/settings', builder: (_, _) => const SettingsScreen()),
        ],
      ),
      GoRoute(
        path: '/voice',
        parentNavigatorKey: _rootKey,
        pageBuilder: (_, _) =>
            const MaterialPage(fullscreenDialog: true, child: VoiceModal()),
      ),
      GoRoute(
        path: '/children/add',
        parentNavigatorKey: _rootKey,
        pageBuilder: (_, _) =>
            const MaterialPage(fullscreenDialog: true, child: AddChildScreen()),
      ),
      GoRoute(
        path: '/child/:id',
        parentNavigatorKey: _rootKey,
        builder: (_, state) =>
            ChildProfileScreen(childId: state.pathParameters['id']!),
      ),
      GoRoute(
        path: '/rec/:id',
        parentNavigatorKey: _rootKey,
        builder: (_, state) =>
            RecDetailScreen(recId: state.pathParameters['id']!),
      ),
      GoRoute(
        path: '/notifications',
        parentNavigatorKey: _rootKey,
        builder: (_, _) => const NotificationsScreen(),
      ),
      GoRoute(
        path: '/voice-notifications',
        parentNavigatorKey: _rootKey,
        builder: (_, _) => const NotificationCenterScreen(),
      ),
      GoRoute(
        path: '/monitoring',
        parentNavigatorKey: _rootKey,
        builder: (_, _) => const ActiveMonitoringScreen(),
      ),
      GoRoute(
        path: '/monitoring/dashboard',
        parentNavigatorKey: _rootKey,
        builder: (_, _) => const FamilyRiskDashboard(),
      ),
      GoRoute(
        path: '/monitoring/escalations',
        parentNavigatorKey: _rootKey,
        builder: (_, state) {
          final extra = state.extra;
          final sessionId = extra is int ? extra : null;
          return EscalationTimelineScreen(sessionId: sessionId);
        },
      ),
    ],
  );
}

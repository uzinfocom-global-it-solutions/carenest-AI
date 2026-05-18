import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:app/features/auth/application/auth_controller.dart';
import 'package:app/features/home/presentation/home_screen.dart';
import 'login_screen.dart';

class AuthWrapper extends StatefulWidget {
  const AuthWrapper({super.key});

  @override
  State<AuthWrapper> createState() => _AuthWrapperState();
}

class _AuthWrapperState extends State<AuthWrapper> {
  @override
  void initState() {
    super.initState();
    context.read<AuthController>().tryAutoLogin();
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<AuthController>(
      builder: (_, auth, _) {
        if (auth.isLoading) {
          return const Scaffold(
            body: Center(child: CircularProgressIndicator()),
          );
        }
        return auth.isAuthenticated ? const HomeScreen() : const LoginScreen();
      },
    );
  }
}

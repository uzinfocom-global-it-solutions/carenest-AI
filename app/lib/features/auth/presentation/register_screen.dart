import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:app/core/localization/app_strings.dart';
import 'package:app/features/auth/application/auth_controller.dart';
import 'package:app/features/settings/application/app_settings_controller.dart';
import 'package:app/shared/validators/auth_validators.dart';
import 'package:app/shared/widgets/app_button.dart';
import 'package:app/shared/widgets/app_text_field.dart';
import 'widgets/auth_form_wrapper.dart';

class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _displayNameController = TextEditingController();

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    _displayNameController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    final controller = context.read<AuthController>();
    final success = await controller.register(
      email: _emailController.text.trim(),
      password: _passwordController.text,
      displayName: _displayNameController.text.trim().isEmpty
          ? null
          : _displayNameController.text.trim(),
    );

    if (!success && mounted) {
      final strings = AppStrings(context.read<AppSettingsController>().language);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(controller.error ?? strings.registerFailed)),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final strings = AppStrings(context.watch<AppSettingsController>().language);
    return AuthFormWrapper(
      title: strings.registerTitle,
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            AppTextField(
              label: strings.email,
              controller: _emailController,
              validator: AuthValidators.email,
              keyboardType: TextInputType.emailAddress,
              textInputAction: TextInputAction.next,
            ),
            const SizedBox(height: 16),
            AppTextField(
              label: strings.password,
              controller: _passwordController,
              validator: AuthValidators.password,
              obscureText: true,
              textInputAction: TextInputAction.next,
            ),
            const SizedBox(height: 16),
            AppTextField(
              label: strings.displayNameHint,
              controller: _displayNameController,
              validator: AuthValidators.displayName,
              textInputAction: TextInputAction.done,
              onFieldSubmitted: (_) => _submit(),
            ),
            const SizedBox(height: 28),
            Consumer<AuthController>(
              builder: (_, auth, _) => AppButton(
                label: strings.register,
                onPressed: _submit,
                isLoading: auth.isLoading,
              ),
            ),
            const SizedBox(height: 16),
            TextButton(
              onPressed: () => Navigator.of(context).pop(),
              child: Text(strings.alreadyHaveAccount),
            ),
          ],
        ),
      ),
    );
  }
}

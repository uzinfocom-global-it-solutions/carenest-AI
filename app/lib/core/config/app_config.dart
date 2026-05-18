import 'dart:convert';
import 'package:flutter/services.dart';

class AppConfig {
  AppConfig._();

  static String _baseUrl = '';

  static String get baseUrl => _baseUrl;

  static Future<void> load() async {
    try {
      final raw = await rootBundle.loadString('env.json');
      final map = jsonDecode(raw) as Map<String, dynamic>;
      final url = map['API_BASE_URL'] as String? ?? '';
      _baseUrl = url.endsWith('/') ? url.substring(0, url.length - 1) : url;
    } catch (_) {
      _baseUrl = '';
    }
  }
}

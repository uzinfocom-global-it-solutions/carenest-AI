import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

enum InputMode { voice, text }

enum AppLanguage { english, russian }

class AppSettingsController extends ChangeNotifier {
  AppSettingsController({FlutterSecureStorage? storage})
    : _storage = storage ?? const FlutterSecureStorage();

  static const _defaultInputKey = 'default_input_mode';
  static const _languageKey = 'app_language';
  static const _notificationsEnabledKey = 'notifications_enabled';
  static const _voiceWithNotificationsKey = 'voice_with_notifications';

  final FlutterSecureStorage _storage;

  InputMode _defaultInput = InputMode.voice;
  AppLanguage _language = AppLanguage.english;
  bool _notificationsEnabled = true;
  bool _voiceWithNotifications = true;
  bool _loaded = false;

  InputMode get defaultInput => _defaultInput;
  AppLanguage get language => _language;
  bool get notificationsEnabled => _notificationsEnabled;
  bool get voiceWithNotifications => _voiceWithNotifications;
  bool get isLoaded => _loaded;

  String get ttsLocale => _language == AppLanguage.russian ? 'ru-RU' : 'en-US';
  String get sttLocale => _language == AppLanguage.russian ? 'ru_RU' : 'en_US';
  String get apiLocale => _language == AppLanguage.russian ? 'ru' : 'en';

  Future<void> load() async {
    final rawInput = await _storage.read(key: _defaultInputKey);
    _defaultInput = rawInput == 'text' ? InputMode.text : InputMode.voice;

    final rawLang = await _storage.read(key: _languageKey);
    _language = rawLang == 'ru' ? AppLanguage.russian : AppLanguage.english;

    final rawNotif = await _storage.read(key: _notificationsEnabledKey);
    _notificationsEnabled = rawNotif != 'false';

    final rawVoice = await _storage.read(key: _voiceWithNotificationsKey);
    _voiceWithNotifications = rawVoice != 'false';

    _loaded = true;
    notifyListeners();
  }

  Future<void> setDefaultInput(InputMode mode) async {
    if (_defaultInput == mode) return;
    _defaultInput = mode;
    await _storage.write(
      key: _defaultInputKey,
      value: mode == InputMode.text ? 'text' : 'voice',
    );
    notifyListeners();
  }

  Future<void> setLanguage(AppLanguage lang) async {
    if (_language == lang) return;
    _language = lang;
    await _storage.write(
      key: _languageKey,
      value: lang == AppLanguage.russian ? 'ru' : 'en',
    );
    notifyListeners();
  }

  Future<void> setNotificationsEnabled(bool value) async {
    if (_notificationsEnabled == value) return;
    _notificationsEnabled = value;
    await _storage.write(key: _notificationsEnabledKey, value: value ? 'true' : 'false');
    notifyListeners();
  }

  Future<void> setVoiceWithNotifications(bool value) async {
    if (_voiceWithNotifications == value) return;
    _voiceWithNotifications = value;
    await _storage.write(key: _voiceWithNotificationsKey, value: value ? 'true' : 'false');
    notifyListeners();
  }
}

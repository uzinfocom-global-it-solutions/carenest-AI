import 'dart:async';
import 'package:collection/collection.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_tts/flutter_tts.dart';

class VoicePlaybackOrchestrator extends ChangeNotifier {
  VoicePlaybackOrchestrator._();
  static final instance = VoicePlaybackOrchestrator._();

  final FlutterTts _tts = FlutterTts();
  final PriorityQueue<_TtsJob> _queue = PriorityQueue<_TtsJob>();

  bool _initialized = false;
  bool _playing = false;
  String _currentLocale = 'ru-RU';
  _TtsJob? _currentJob;

  // кеш голосов: locale-prefix → {name, locale}
  final Map<String, Map<String, String>> _voiceCache = {};
  bool _voicesFetched = false;

  bool get isPlaying => _playing;
  String? get currentText => _currentJob?.text;

  Future<void> _init() async {
    if (_initialized) return;
    _initialized = true;

    // хендлеры регистрируются ДО прогрева
    _tts.setStartHandler(() {
      _playing = true;
      notifyListeners();
    });

    _tts.setCompletionHandler(() {
      _playing = false;
      _currentJob = null;
      notifyListeners();
      _processNext();
    });

    _tts.setCancelHandler(() {
      _playing = false;
      notifyListeners();
      _processNext();
    });

    _tts.setErrorHandler((_) {
      _playing = false;
      _currentJob = null;
      notifyListeners();
      _processNext();
    });

    await _tts.setLanguage('ru-RU');
    await _tts.setSpeechRate(0.88);
    await _tts.setVolume(1.0);
    await _tts.setPitch(1.0);
    await _setVoiceForLocale('ru-RU');
    // прогрев движка — устраняет задержку первого воспроизведения
    await _tts.speak(' ');
    await _tts.stop();
  }

  /// Вызвать при старте приложения чтобы прогреть TTS до первого уведомления.
  Future<void> warmUp() => _init();

  Future<void> enqueue(
    String text, {
    int priority = 10,
    String locale = 'ru-RU',
    bool interruptIfHigher = true,
  }) async {
    await _init();

    final job = _TtsJob(
      text: text,
      priority: priority,
      locale: locale,
      enqueuedAt: DateTime.now(),
    );

    if (interruptIfHigher && _playing && _currentJob != null) {
      if (job.priority > _currentJob!.priority) {
        await _tts.stop();
        _queue.add(job);
        return;
      }
    }

    _queue.add(job);

    if (!_playing) {
      _processNext();
    }
  }

  Future<void> stopAll() async {
    await _init();
    _queue.clear();
    _currentJob = null;
    await _tts.stop();
    _playing = false;
    notifyListeners();
  }

  Future<void> skip() async {
    await _init();
    await _tts.stop();
  }

  void _processNext() {
    if (_queue.isEmpty || _playing) return;

    final job = _queue.removeFirst();
    _currentJob = job;
    _speak(job);
  }

  Future<void> _setVoiceForLocale(String locale) async {
    final langCode = locale.split('-').first.toLowerCase();
    if (_voiceCache.containsKey(langCode)) {
      final cached = _voiceCache[langCode];
      if (cached != null) await _tts.setVoice(cached);
      return;
    }
    try {
      if (!_voicesFetched) {
        _voicesFetched = true;
        final voices = await _tts.getVoices;
        if (voices != null) {
          for (final v in voices as List) {
            final voice = v as Map<Object?, Object?>;
            final vLocale = (voice['locale'] as String? ?? '').toLowerCase();
            final vName = voice['name'] as String?;
            if (vName == null) continue;
            final code = vLocale.split('-').first;
            if (!_voiceCache.containsKey(code)) {
              _voiceCache[code] = {'name': vName, 'locale': voice['locale'] as String};
            }
          }
        }
      }
      final selected = _voiceCache[langCode];
      if (selected != null) await _tts.setVoice(selected);
    } catch (_) {}
  }

  Future<void> _speak(_TtsJob job) async {
    if (job.locale != _currentLocale) {
      _currentLocale = job.locale;
      await _tts.setLanguage(job.locale);
      await _tts.setSpeechRate(job.locale.startsWith('ru') ? 0.88 : 0.9);
      await _setVoiceForLocale(job.locale);
    }

    final cleaned = _cleanForTts(job.text);
    if (cleaned.isEmpty) {
      _currentJob = null;
      _processNext();
      return;
    }

    debugPrint('[TTS] Speaking (p=${job.priority}): ${cleaned.substring(0, cleaned.length.clamp(0, 60))}…');
    await _tts.speak(cleaned);
  }

  static String _cleanForTts(String text) {
    return text
        .replaceAll(RegExp(r'[*_`~#]'), '')
        .replaceAll(RegExp(r'[\u{1F600}-\u{1F64F}]', unicode: true), '')
        .replaceAll(RegExp(r'[\u{1F300}-\u{1F9FF}]', unicode: true), '')
        .replaceAll(RegExp(r'\s+'), ' ')
        .trim();
  }

  @override
  void dispose() {
    _tts.stop();
    super.dispose();
  }
}

class _TtsJob implements Comparable<_TtsJob> {
  final String text;
  final int priority;
  final String locale;
  final DateTime enqueuedAt;

  const _TtsJob({
    required this.text,
    required this.priority,
    required this.locale,
    required this.enqueuedAt,
  });

  @override
  int compareTo(_TtsJob other) {
    final cmp = other.priority.compareTo(priority);
    if (cmp != 0) return cmp;
    return enqueuedAt.compareTo(other.enqueuedAt);
  }
}

import 'dart:async';
import 'package:collection/collection.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_tts/flutter_tts.dart';

/// Priority-ordered TTS queue.
/// Higher priority values interrupt lower-priority playback.
///
/// Usage:
///   VoicePlaybackOrchestrator.instance.enqueue('Доброе утро!', priority: 0);
///   VoicePlaybackOrchestrator.instance.enqueue('🚨 Срочно!', priority: 10);
class VoicePlaybackOrchestrator extends ChangeNotifier {
  VoicePlaybackOrchestrator._();
  static final instance = VoicePlaybackOrchestrator._();

  final FlutterTts _tts = FlutterTts();
  final PriorityQueue<_TtsJob> _queue = PriorityQueue<_TtsJob>();

  bool _initialized = false;
  bool _playing = false;
  String _currentLocale = 'ru-RU';
  _TtsJob? _currentJob;

  bool get isPlaying => _playing;
  String? get currentText => _currentJob?.text;

  Future<void> _init() async {
    if (_initialized) return;
    _initialized = true;

    await _tts.setLanguage('ru-RU');
    await _tts.setSpeechRate(0.88);
    await _tts.setVolume(1.0);
    await _tts.setPitch(1.0);

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
  }

  /// Enqueue text for playback.
  ///
  /// [priority]: higher = more urgent. Emergency = 100, High = 50, Normal = 10, Low = 1.
  /// [interruptIfHigher]: if true, stops current playback when this job's priority
  ///   is strictly higher than the current job's priority.
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

    // Emergency (priority >= 100) always interrupts
    if (interruptIfHigher && _playing && _currentJob != null) {
      if (job.priority > _currentJob!.priority) {
        await _tts.stop();
        _queue.add(job);
        return; // completion handler will trigger _processNext with new job at front
      }
    }

    _queue.add(job);

    if (!_playing) {
      _processNext();
    }
  }

  /// Immediately stop all playback and clear the queue.
  Future<void> stopAll() async {
    await _init();
    _queue.clear();
    _currentJob = null;
    await _tts.stop();
    _playing = false;
    notifyListeners();
  }

  /// Stop current only, continue with queue.
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

  Future<void> _speak(_TtsJob job) async {
    if (job.locale != _currentLocale) {
      _currentLocale = job.locale;
      await _tts.setLanguage(job.locale);
      await _tts.setSpeechRate(job.locale.startsWith('ru') ? 0.88 : 0.9);
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
    // Remove markdown, emojis that TTS reads as symbols, leading/trailing whitespace
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
    // Higher priority first; break ties by enqueue time (earlier first)
    final cmp = other.priority.compareTo(priority);
    if (cmp != 0) return cmp;
    return enqueuedAt.compareTo(other.enqueuedAt);
  }
}

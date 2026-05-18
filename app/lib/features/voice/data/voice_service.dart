import 'package:flutter/foundation.dart';
import 'package:flutter_tts/flutter_tts.dart';
import 'package:speech_to_text/speech_to_text.dart' as stt;
import 'package:speech_to_text/speech_recognition_result.dart';

class VoiceService extends ChangeNotifier {
  VoiceService() {
    _initTts('en-US');
  }

  final stt.SpeechToText _stt = stt.SpeechToText();
  final FlutterTts _tts = FlutterTts();

  bool _sttReady = false;
  bool _isListening = false;
  bool _isSpeaking = false;
  String _transcript = '';
  String _currentTtsLocale = 'en-US';

  bool get isListening => _isListening;
  bool get isSpeaking => _isSpeaking;
  String get transcript => _transcript;

  Future<void> _initTts(String locale) async {
    _currentTtsLocale = locale;
    await _tts.setLanguage(locale);
    await _tts.setSpeechRate(locale.startsWith('ru') ? 0.45 : 0.48);
    await _tts.setVolume(1.0);
    await _tts.setPitch(locale.startsWith('ru') ? 1.0 : 1.05);

    _tts.setStartHandler(() {
      _isSpeaking = true;
      notifyListeners();
    });
    _tts.setCompletionHandler(() {
      _isSpeaking = false;
      notifyListeners();
    });
    _tts.setCancelHandler(() {
      _isSpeaking = false;
      notifyListeners();
    });
    _tts.setErrorHandler((_) {
      _isSpeaking = false;
      notifyListeners();
    });
  }

  Future<void> ensureLocale(String ttsLocale) async {
    if (_currentTtsLocale == ttsLocale) return;
    _currentTtsLocale = ttsLocale;
    await _tts.setLanguage(ttsLocale);
    await _tts.setSpeechRate(ttsLocale.startsWith('ru') ? 0.45 : 0.48);
    await _tts.setPitch(ttsLocale.startsWith('ru') ? 1.0 : 1.05);
  }

  Future<bool> ensureReady() async {
    if (_sttReady) return true;
    _sttReady = await _stt.initialize(
      onError: (_) {
        _isListening = false;
        notifyListeners();
      },
      onStatus: (status) {
        _isListening = status == 'listening';
        notifyListeners();
      },
    );
    return _sttReady;
  }

  Future<bool> startListening({
    required void Function(String text, bool isFinal) onResult,
    String sttLocale = 'en_US',
  }) async {
    if (!await ensureReady()) return false;
    if (_isListening) await _stt.stop();

    _transcript = '';
    await _stt.listen(
      localeId: sttLocale,
      listenOptions: stt.SpeechListenOptions(
        partialResults: true,
        cancelOnError: true,
        listenMode: stt.ListenMode.confirmation,
      ),
      onResult: (SpeechRecognitionResult result) {
        _transcript = result.recognizedWords;
        notifyListeners();
        onResult(_transcript, result.finalResult);
      },
    );
    return true;
  }

  Future<String> stopListening() async {
    if (_isListening) await _stt.stop();
    _isListening = false;
    notifyListeners();
    return _transcript;
  }

  Future<void> cancel() async {
    if (_isListening) await _stt.cancel();
    _isListening = false;
    _transcript = '';
    notifyListeners();
  }

  // ── English number → words ─────────────────────────────────────────────────

  static const _ones = [
    '', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight',
    'nine', 'ten', 'eleven', 'twelve', 'thirteen', 'fourteen', 'fifteen',
    'sixteen', 'seventeen', 'eighteen', 'nineteen',
  ];
  static const _tens = [
    '', '', 'twenty', 'thirty', 'forty', 'fifty',
    'sixty', 'seventy', 'eighty', 'ninety',
  ];

  static String _numToWordsEn(int n) {
    if (n < 0) return 'minus ${_numToWordsEn(-n)}';
    if (n == 0) return 'zero';
    if (n < 20) return _ones[n];
    if (n < 100) {
      final rem = n % 10;
      return rem == 0 ? _tens[n ~/ 10] : '${_tens[n ~/ 10]} ${_ones[rem]}';
    }
    if (n < 1000) {
      final rem = n % 100;
      final base = '${_ones[n ~/ 100]} hundred';
      return rem == 0 ? base : '$base ${_numToWordsEn(rem)}';
    }
    if (n < 1000000) {
      final rem = n % 1000;
      final base = '${_numToWordsEn(n ~/ 1000)} thousand';
      return rem == 0 ? base : '$base ${_numToWordsEn(rem)}';
    }
    return n.toString();
  }

  // ── TTS text cleanup ───────────────────────────────────────────────────────

  static String _cleanForTts(String text, {required bool isRussian}) {
    // Remove emojis and non-ASCII (but keep Cyrillic if Russian)
    if (!isRussian) {
      text = text.replaceAll(RegExp(r'[^\x00-\x7F]'), '');
    } else {
      // For Russian: remove emojis but keep Cyrillic
      text = text.replaceAll(
        RegExp(
          r'[\u{1F600}-\u{1F64F}\u{1F300}-\u{1F5FF}\u{1F680}-\u{1F6FF}'
          r'\u{1F1E0}-\u{1F1FF}\u{2600}-\u{26FF}\u{2700}-\u{27BF}]',
          unicode: true,
        ),
        '',
      );
    }
    // Remove markdown symbols
    text = text.replaceAll(RegExp(r'[*_~`#>\[\]|\\]'), '');
    // Replace bullet dashes with pause
    text = text.replaceAll(RegExp(r'^\s*-\s+', multiLine: true), ', ');
    // For English: convert numbers to words (Russian TTS reads digits natively)
    if (!isRussian) {
      text = text.replaceAllMapped(RegExp(r'\b(\d+)\b'), (m) {
        final v = int.tryParse(m.group(1)!);
        return v != null ? _numToWordsEn(v) : m.group(0)!;
      });
    }
    // Collapse whitespace
    text = text.replaceAll(RegExp(r'\s+'), ' ').trim();
    text = text.replaceAll(RegExp(r'^,\s*'), '').replaceAll(RegExp(r',\s*$'), '');
    return text;
  }

  Future<void> speak(String text, {String ttsLocale = 'en-US'}) async {
    if (text.trim().isEmpty) return;
    await ensureLocale(ttsLocale);
    await _tts.stop();
    final isRu = ttsLocale.startsWith('ru');
    await _tts.speak(_cleanForTts(text, isRussian: isRu));
  }

  Future<void> stopSpeaking() async {
    await _tts.stop();
    _isSpeaking = false;
    notifyListeners();
  }

  @override
  void dispose() {
    _stt.cancel();
    _tts.stop();
    super.dispose();
  }
}

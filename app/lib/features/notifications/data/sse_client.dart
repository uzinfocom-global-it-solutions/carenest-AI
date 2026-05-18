import 'dart:async';
import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import '../../../core/constants/api_constants.dart';

/// Production-grade SSE client with:
/// - Exponential backoff reconnect (1s → 2s → 4s → … → 30s cap)
/// - Event deduplication by Last-Event-ID
/// - App lifecycle awareness (caller connects/disconnects)
/// - Clean resource management (single http.Client per connection)
class SseClient {
  SseClient({required this.tokenProvider});

  final Future<String?> Function() tokenProvider;

  StreamSubscription<String>? _sub;
  http.Client? _httpClient;
  final _controller = StreamController<SseMessage>.broadcast();

  Stream<SseMessage> get events => _controller.stream;

  bool _connected = false;
  bool _disposed = false;
  bool get connected => _connected;

  int _reconnectAttempt = 0;
  static const int _maxBackoffSeconds = 30;

  /// Last received event ID — sent to server on reconnect for replay
  String? _lastEventId;

  /// Deduplication: ignore events we've seen in this session
  final _seenIds = <String>{};

  Future<void> connect() async {
    if (_disposed) return;
    await _closeCurrentConnection();

    final token = await tokenProvider();
    if (token == null) {
      debugPrint('[SSE] No auth token — skipping connect');
      return;
    }

    var uriStr = '${ApiConstants.baseUrl}/api/v1/sse';
    // Also append token as query param (matches server's JwtBearer OnMessageReceived)
    uriStr += '?access_token=${Uri.encodeComponent(token)}';
    if (_lastEventId != null) {
      uriStr += '&lastEventId=${Uri.encodeComponent(_lastEventId!)}';
    }

    final uri = Uri.parse(uriStr);
    final request = http.Request('GET', uri);
    request.headers['Accept'] = 'text/event-stream';
    request.headers['Cache-Control'] = 'no-cache';
    if (_lastEventId != null) {
      request.headers['Last-Event-ID'] = _lastEventId!;
    }

    try {
      _httpClient = http.Client();
      final response = await _httpClient!.send(request);

      if (response.statusCode == 401) {
        debugPrint('[SSE] Unauthorized — not reconnecting');
        _httpClient?.close();
        return;
      }

      if (response.statusCode != 200) {
        debugPrint('[SSE] Unexpected status ${response.statusCode}');
        _httpClient?.close();
        _scheduleReconnect();
        return;
      }

      _connected = true;
      _reconnectAttempt = 0;
      debugPrint('[SSE] Connected (attempt $_reconnectAttempt, lastId=$_lastEventId)');

      _sub = response.stream
          .transform(utf8.decoder)
          .transform(const LineSplitter())
          .transform(_SseLineAggregator())
          .listen(
            _onBlock,
            onError: (dynamic e) {
              debugPrint('[SSE] Stream error: $e');
              _onDisconnect();
            },
            onDone: () {
              debugPrint('[SSE] Stream closed');
              _onDisconnect();
            },
            cancelOnError: false,
          );
    } catch (e) {
      debugPrint('[SSE] Connection error: $e');
      _httpClient?.close();
      _httpClient = null;
      _onDisconnect();
    }
  }

  void _onBlock(String block) {
    final msg = _parseBlock(block);
    if (msg == null) return;

    // Track last event ID for reconnect
    if (msg.id != null) _lastEventId = msg.id;

    // Deduplication: skip if we've seen this event ID in this session
    if (msg.id != null) {
      if (_seenIds.contains(msg.id)) return;
      _seenIds.add(msg.id!);
      // Keep set bounded (last 1000 IDs)
      if (_seenIds.length > 1000) _seenIds.remove(_seenIds.first);
    }

    // Skip infrastructure events
    if (msg.eventType == 'heartbeat' || msg.eventType == 'connected') return;

    _controller.add(msg);
  }

  void _onDisconnect() {
    _connected = false;
    if (!_disposed) _scheduleReconnect();
  }

  Timer? _reconnectTimer;

  void _scheduleReconnect() {
    if (_disposed) return;
    _reconnectTimer?.cancel();

    // Exponential backoff: 1s, 2s, 4s, 8s, 16s, 30s (capped)
    final delaySeconds = (_reconnectAttempt == 0
            ? 1
            : (1 << _reconnectAttempt).clamp(1, _maxBackoffSeconds))
        .clamp(1, _maxBackoffSeconds);
    _reconnectAttempt++;

    debugPrint('[SSE] Reconnect in ${delaySeconds}s (attempt $_reconnectAttempt)');
    _reconnectTimer = Timer(Duration(seconds: delaySeconds), connect);
  }

  Future<void> _closeCurrentConnection() async {
    _reconnectTimer?.cancel();
    _reconnectTimer = null;
    await _sub?.cancel();
    _sub = null;
    _httpClient?.close();
    _httpClient = null;
    _connected = false;
  }

  Future<void> disconnect() async {
    await _closeCurrentConnection();
    _reconnectAttempt = 0;
  }

  SseMessage? _parseBlock(String block) {
    String? eventType;
    String? data;
    String? id;

    for (final line in block.split('\n')) {
      if (line.startsWith('event:')) {
        eventType = line.substring(6).trim();
      } else if (line.startsWith('data:')) {
        final chunk = line.substring(5).trim();
        data = data == null ? chunk : '$data\n$chunk';
      } else if (line.startsWith('id:')) {
        id = line.substring(3).trim();
      }
    }

    if (data == null) return null;
    return SseMessage(eventType: eventType ?? 'message', data: data, id: id);
  }

  void dispose() {
    _disposed = true;
    disconnect();
    _controller.close();
  }
}

class SseMessage {
  const SseMessage({required this.eventType, required this.data, this.id});
  final String eventType;
  final String data;
  final String? id;

  Map<String, dynamic>? get json {
    try {
      return jsonDecode(data) as Map<String, dynamic>;
    } catch (_) {
      return null;
    }
  }
}

/// Accumulates SSE lines into complete event blocks (blank-line-separated).
class _SseLineAggregator extends StreamTransformerBase<String, String> {
  @override
  Stream<String> bind(Stream<String> stream) async* {
    final buffer = <String>[];
    await for (final line in stream) {
      if (line.isEmpty) {
        if (buffer.isNotEmpty) {
          yield buffer.join('\n');
          buffer.clear();
        }
      } else {
        buffer.add(line);
      }
    }
    // Flush any remaining buffered lines on stream close
    if (buffer.isNotEmpty) yield buffer.join('\n');
  }
}

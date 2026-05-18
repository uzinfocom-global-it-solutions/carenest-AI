import 'package:flutter/foundation.dart';
import '../../../shared/api/api_client.dart';
import '../data/local_notification_service.dart';
import '../data/push_notification_handler.dart';

enum VoiceActionStatus { pending, delivered, confirmed, skipped, failed }

class VoiceActionModel {
  final int id;
  final String type;
  final String priority;
  final String text;
  final String status;
  final bool requiresConfirmation;
  final bool escalationEnabled;
  final int escalationStep;
  final DateTime? deliveredAt;
  final DateTime? confirmedAt;
  final DateTime createdAt;

  const VoiceActionModel({
    required this.id,
    required this.type,
    required this.priority,
    required this.text,
    required this.status,
    required this.requiresConfirmation,
    required this.escalationEnabled,
    required this.escalationStep,
    this.deliveredAt,
    this.confirmedAt,
    required this.createdAt,
  });

  factory VoiceActionModel.fromJson(Map<String, dynamic> j) => VoiceActionModel(
        id: j['id'] as int,
        type: j['type'] as String? ?? '',
        priority: j['priority'] as String? ?? 'Normal',
        text: j['text'] as String? ?? '',
        status: j['status'] as String? ?? 'Pending',
        requiresConfirmation: j['requiresConfirmation'] as bool? ?? false,
        escalationEnabled: j['escalationEnabled'] as bool? ?? false,
        escalationStep: j['escalationStep'] as int? ?? 0,
        deliveredAt: j['deliveredAt'] != null ? DateTime.tryParse(j['deliveredAt'] as String) : null,
        confirmedAt: j['confirmedAt'] != null ? DateTime.tryParse(j['confirmedAt'] as String) : null,
        createdAt: DateTime.tryParse(j['createdAt'] as String? ?? '') ?? DateTime.now(),
      );
}

class VoiceNotificationController extends ChangeNotifier {
  VoiceNotificationController(this._api) {
    NotificationActionRouter.stream.listen(_onNotificationAction);
    NotificationTapRouter.listen(_onNotificationTap);
  }

  final ApiClient _api;

  final List<VoiceActionModel> _actions = [];
  bool _loading = false;
  String? _error;

  List<VoiceActionModel> get actions => List.unmodifiable(_actions);
  bool get loading => _loading;
  String? get error => _error;

  List<VoiceActionModel> get pending =>
      _actions.where((a) => a.status == 'Pending' || a.status == 'Delivered').toList();

  Future<void> loadPending() async {
    _loading = true;
    _error = null;
    notifyListeners();
    try {
      final result = await _api.get('/api/v1/voice-actions/pending');
      final list = result as List;
      _actions
        ..clear()
        ..addAll(list.map((e) => VoiceActionModel.fromJson(e as Map<String, dynamic>)));
    } catch (e) {
      _error = e.toString();
      debugPrint('[VoiceNotif] loadPending error: $e');
    } finally {
      _loading = false;
      notifyListeners();
    }
  }

  Future<void> loadTimeline({DateTime? from, DateTime? to}) async {
    _loading = true;
    notifyListeners();
    try {
      final fromStr = (from ?? DateTime.now().subtract(const Duration(days: 7))).toIso8601String();
      final toStr = (to ?? DateTime.now()).toIso8601String();
      final result = await _api.get('/api/v1/voice-actions/timeline?from=$fromStr&to=$toStr');
      final list = result as List;
      _actions
        ..clear()
        ..addAll(list.map((e) => VoiceActionModel.fromJson(e as Map<String, dynamic>)));
    } catch (e) {
      _error = e.toString();
    } finally {
      _loading = false;
      notifyListeners();
    }
  }

  Future<void> confirm(int actionId) async {
    try {
      await _api.post('/api/v1/voice-actions/$actionId/confirm');
      await LocalNotificationService.instance.cancel(actionId);
      _updateLocalStatus(actionId, 'Confirmed');
      notifyListeners();
    } catch (e) {
      debugPrint('[VoiceNotif] confirm error: $e');
    }
  }

  Future<void> skip(int actionId) async {
    try {
      await _api.post('/api/v1/voice-actions/$actionId/skip');
      await LocalNotificationService.instance.cancel(actionId);
      _updateLocalStatus(actionId, 'Skipped');
      notifyListeners();
    } catch (e) {
      debugPrint('[VoiceNotif] skip error: $e');
    }
  }

  void onSseEvent(String eventType, Map<String, dynamic> data) {
    switch (eventType) {
      case 'voice_action_created':
      case 'voice_action_dispatch':
        loadPending();
      case 'voice_action_confirmed':
        final id = data['actionId'] as int?;
        if (id != null) _updateLocalStatus(id, 'Confirmed');
        notifyListeners();
      case 'voice_action_skipped':
        final id = data['actionId'] as int?;
        if (id != null) _updateLocalStatus(id, 'Skipped');
        notifyListeners();
      case 'escalation_step1':
      case 'escalation_step2':
      case 'emergency_escalation':
        loadPending();
    }
  }

  void _onNotificationAction(NotificationAction action) {
    final payload = action.payload ?? '';
    final parts = payload.split(':');
    if (parts.length < 2) return;

    final actionId = int.tryParse(parts[1]);
    if (actionId == null) return;

    switch (action.actionId) {
      case 'confirm':
        confirm(actionId);
      case 'skip':
        skip(actionId);
      case 'remind_later':
        Future.delayed(const Duration(minutes: 15), () => loadPending());
    }
  }

  void _onNotificationTap(Map<String, dynamic> data) {
    final type = data['type'] as String? ?? '';
    if (type == 'voice_action' || type == 'escalation') {
      loadPending();
    }
  }

  void _updateLocalStatus(int id, String status) {
    final idx = _actions.indexWhere((a) => a.id == id);
    if (idx == -1) return;
    final old = _actions[idx];
    _actions[idx] = VoiceActionModel(
      id: old.id,
      type: old.type,
      priority: old.priority,
      text: old.text,
      status: status,
      requiresConfirmation: old.requiresConfirmation,
      escalationEnabled: old.escalationEnabled,
      escalationStep: old.escalationStep,
      deliveredAt: old.deliveredAt,
      confirmedAt: status == 'Confirmed' ? DateTime.now() : old.confirmedAt,
      createdAt: old.createdAt,
    );
  }
}

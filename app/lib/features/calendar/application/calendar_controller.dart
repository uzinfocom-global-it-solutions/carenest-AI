import 'package:flutter/foundation.dart';
import 'package:app/features/calendar/data/calendar_service.dart';
import 'package:app/features/calendar/domain/calendar_models.dart';

class CalendarController extends ChangeNotifier {
  CalendarController({required CalendarService service}) : _service = service;

  final CalendarService _service;

  bool isLoading = false;
  List<CalendarEventModel> events = const [];
  String? error;
  DateTime _activeWeekStart = _weekStart(DateTime.now());
  int? _lastFamilyId;

  int _loadVersion = 0;

  DateTime get activeWeekStart => _activeWeekStart;
  int? get familyId => _lastFamilyId;

  static DateTime _weekStart(DateTime d) =>
      DateTime(d.year, d.month, d.day).subtract(Duration(days: d.weekday - 1));

  List<CalendarEventModel> eventsForDay(DateTime day) =>
      events
          .where(
            (e) =>
                e.startDatetime.year == day.year &&
                e.startDatetime.month == day.month &&
                e.startDatetime.day == day.day,
          )
          .toList()
        ..sort((a, b) => a.startDatetime.compareTo(b.startDatetime));

  Future<void> loadWeek(int familyId, {DateTime? weekStart}) async {
    _lastFamilyId = familyId;
    if (weekStart != null) {
      _activeWeekStart = _weekStart(weekStart);
    }
    final v = ++_loadVersion;
    isLoading = true;
    error = null;
    notifyListeners();
    debugPrint('[Calendar] Load started (v$v) family=$familyId week=$_activeWeekStart');
    try {
      final from = _activeWeekStart;
      final to = from.add(const Duration(days: 7));
      final result = await _service.listEvents(familyId, from, to);
      if (v != _loadVersion) {
        debugPrint('[Calendar] Stale response discarded (v$v, current v$_loadVersion)');
        return;
      }
      events = List.unmodifiable(result);
      debugPrint('[Calendar] State updated (v$v): ${events.length} events');
    } catch (e) {
      if (v != _loadVersion) return;
      error = e.toString();
      events = const [];
      debugPrint('[Calendar] Load error (v$v): $e');
    } finally {
      if (v == _loadVersion) {
        isLoading = false;
        notifyListeners();
        debugPrint('[Calendar] notifyListeners (v$v)');
      }
    }
  }

  Future<CalendarEventModel?> createEvent(Map<String, dynamic> body) async {
    if (_lastFamilyId == null) return null;
    try {
      final event = await _service.createEvent(_lastFamilyId!, body);
      events = List.unmodifiable([...events, event]);
      notifyListeners();
      return event;
    } catch (_) {
      return null;
    }
  }

  Future<bool> updateEvent(int eventId, Map<String, dynamic> body) async {
    try {
      final updated = await _service.updateEvent(eventId, body);
      events = List.unmodifiable(
        events.map((e) => e.id == eventId ? updated : e).toList(),
      );
      notifyListeners();
      return true;
    } catch (_) {
      return false;
    }
  }

  Future<bool> deleteEvent(int eventId) async {
    try {
      await _service.deleteEvent(eventId);
      events = List.unmodifiable(
        events.where((e) => e.id != eventId).toList(),
      );
      notifyListeners();
      return true;
    } catch (_) {
      return false;
    }
  }

  void clear() {
    _loadVersion++;
    events = const [];
    error = null;
    isLoading = false;
    notifyListeners();
  }
}

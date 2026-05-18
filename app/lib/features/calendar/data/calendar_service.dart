import 'package:app/core/constants/api_constants.dart';
import 'package:app/shared/api/api_client.dart';
import 'package:app/features/calendar/domain/calendar_models.dart';

class CalendarService {
  CalendarService({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  Future<List<CalendarEventModel>> listEvents(
    int familyId,
    DateTime from,
    DateTime to, {
    int? childId,
  }) async {
    var path =
        '${ApiConstants.calendarEvents(familyId)}?from=${from.toUtc().toIso8601String()}&to=${to.toUtc().toIso8601String()}';
    if (childId != null) path += '&childId=$childId';
    final result = await _apiClient.get(path);
    if (result is! List) return [];
    return result
        .map((e) => CalendarEventModel.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<CalendarEventModel> updateEvent(int eventId, Map<String, dynamic> body) async {
    final result = await _apiClient.put(ApiConstants.calendarEvent(eventId), body);
    return CalendarEventModel.fromJson(result as Map<String, dynamic>);
  }

  Future<void> deleteEvent(int eventId) async {
    await _apiClient.delete(ApiConstants.deleteCalendarEvent(eventId));
  }

  Future<CalendarEventModel> createEvent(
    int familyId,
    Map<String, dynamic> body,
  ) async {
    final result = await _apiClient.post(
      ApiConstants.calendarEvents(familyId),
      body,
    );
    return CalendarEventModel.fromJson(result as Map<String, dynamic>);
  }
}

import 'package:app/core/config/app_config.dart';

class ApiConstants {
  ApiConstants._();

  static String get baseUrl => AppConfig.baseUrl;

  static const String register = '/auth/register';
  static const String login = '/auth/login';
  static const String refresh = '/auth/refresh';
  static const String logout = '/auth/logout';

  static const String createFamily = '/families';
  static const String myFamily = '/families/mine';

  static const String createChild = '/children';
  static String getChild(int childId) => '/children/$childId';
  static String listChildrenByFamily(int familyId) =>
      '/children/by-family/$familyId';
  static String routinesByFamily(int familyId) =>
      '/children/by-family/$familyId/routines';
  static String childSensitivity(int childId) =>
      '/children/$childId/sensitivity';
  static String childNotes(int childId) =>
      '/children/$childId/notes';

  static String generateRecs(int childId) =>
      '/recommendations/generate/$childId';
  static String submitFeedback(int recId) => '/recommendations/$recId/feedback';

  static String calendarEvents(int familyId) =>
      '/calendar/families/$familyId/events';
  static String calendarEvent(int eventId) => '/calendar/events/$eventId';
  static String deleteCalendarEvent(int eventId) => '/calendar/events/$eventId';

  static const String weatherCurrent = '/weather/current';

  static const String notifications = '/notifications';
  static String notificationRead(int id) => '/notifications/$id/read';
  static const String notificationsReadAll = '/notifications/read-all';

  static const String createChat = '/chats';
  static String sendMessage(int chatId) => '/chats/$chatId/messages';
  static String getHistory(int chatId) => '/chats/$chatId/messages';
  static String confirmProposal(int chatId, int messageId) =>
      '/chats/$chatId/messages/$messageId/confirm';

}

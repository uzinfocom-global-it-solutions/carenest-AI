import 'package:flutter/widgets.dart';
import 'package:provider/provider.dart';
import 'package:app/features/children/application/children_controller.dart';
import 'package:app/features/calendar/application/calendar_controller.dart';
import 'package:app/features/notifications/application/notifications_controller.dart';
import 'package:app/features/recommendations/application/recommendations_controller.dart';

Future<void> refreshAfterProposal(
  BuildContext context,
  String proposalType,
) async {
  final children = context.read<ChildrenController>();
  final calendar = context.read<CalendarController>();
  final notifications = context.read<NotificationsController>();
  final recs = context.read<RecommendationsController>();
  final familyId = children.familyId ?? calendar.familyId;

  debugPrint('[Refresh] refreshAfterProposal started: type=$proposalType familyId=$familyId');

  switch (proposalType) {
    case 'add_child':
      if (familyId != null) {
        await children.loadForFamily(familyId);
        await recs.loadForChildren(children.children);
      }
      break;
    case 'update_sensitivity':
      if (familyId != null) {
        await children.refresh();
        await recs.loadForChildren(children.children);
      }
      break;
    case 'add_routine':
      if (familyId != null) {
        await children.loadRoutinesForFamily(familyId);
        await calendar.loadWeek(familyId);
      }
      break;
    case 'add_event':
      if (familyId != null) {
        await calendar.loadWeek(familyId);
      }
      break;
    case 'add_note':
      break;
  }

  await notifications.load();
  debugPrint('[Refresh] refreshAfterProposal completed: type=$proposalType');
}

DateTime? _lastGlobalRefresh;

Future<void> refreshAllAfterAiAction(BuildContext context) async {
  final now = DateTime.now();
  final last = _lastGlobalRefresh;
  if (last != null && now.difference(last) < const Duration(seconds: 1)) {
    debugPrint('[Refresh] refreshAllAfterAiAction deduped — ran ${now.difference(last).inMilliseconds}ms ago');
    return;
  }
  _lastGlobalRefresh = now;

  if (!context.mounted) {
    debugPrint('[Refresh] refreshAllAfterAiAction skipped — context unmounted');
    return;
  }

  final childrenCtrl = context.read<ChildrenController>();
  final calendarCtrl = context.read<CalendarController>();
  final notificationsCtrl = context.read<NotificationsController>();
  final recsCtrl = context.read<RecommendationsController>();

  final familyId = childrenCtrl.familyId ?? calendarCtrl.familyId;
  if (familyId == null) {
    debugPrint('[Refresh] refreshAllAfterAiAction skipped — familyId is null');
    return;
  }

  debugPrint('[Refresh] refreshAllAfterAiAction started — family=$familyId');

  await Future.wait([
    childrenCtrl.loadForFamily(familyId),
    childrenCtrl.loadRoutinesForFamily(familyId),
    calendarCtrl.loadWeek(familyId),
    notificationsCtrl.load(),
  ]);

  if (!context.mounted) return;

  await recsCtrl.loadForChildren(childrenCtrl.children);

  debugPrint('[Refresh] refreshAllAfterAiAction completed — family=$familyId');
}

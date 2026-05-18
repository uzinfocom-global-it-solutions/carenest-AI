import 'package:flutter/widgets.dart';
import 'package:provider/provider.dart';
import 'package:app/features/children/application/children_controller.dart';
import 'package:app/features/calendar/application/calendar_controller.dart';
import 'package:app/features/notifications/application/notifications_controller.dart';
import 'package:app/features/recommendations/application/recommendations_controller.dart';

/// Re-fetches the controllers that may have changed server-side after the user
/// confirms an AI proposal. Called from both the text chat and the voice modal
/// so the relevant tabs (Family, Plan, Notifications) reflect the new state
/// without the user needing to pull-to-refresh.
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
        // Backend materialises the routine into 7 days of calendar events
        // immediately, so reload the week so the parent sees them on Plan.
        await calendar.loadWeek(familyId);
      }
      break;
    case 'add_event':
      if (familyId != null) {
        await calendar.loadWeek(familyId);
      }
      break;
    case 'add_note':
      // Notes don't drive any top-level list, so no refresh required.
      break;
  }

  // Recommendations / notifications can be produced for any proposal, so
  // always re-pull the notification feed.
  await notifications.load();
  debugPrint('[Refresh] refreshAfterProposal completed: type=$proposalType');
}

// Dedup guard: track the last time refreshAllAfterAiAction ran so SSE-triggered
// and explicit refreshes that fire within 1 second are collapsed into one.
DateTime? _lastGlobalRefresh;

/// Used after the AI auto-executes any action — refreshes all lists so the
/// Family and Plan tabs update with minimal delay.
///
/// Concurrent duplicate calls within 1 second are no-ops (the in-flight load
/// already owns the latest _loadVersion and the stale response will be
/// discarded by the controller's versioning).
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

  // Prefer familyId from ChildrenController, fall back to CalendarController.
  final familyId = childrenCtrl.familyId ?? calendarCtrl.familyId;
  if (familyId == null) {
    debugPrint('[Refresh] refreshAllAfterAiAction skipped — familyId is null');
    return;
  }

  debugPrint('[Refresh] refreshAllAfterAiAction started — family=$familyId');

  // Run children + routines + calendar + notifications in parallel for speed.
  // Controller versioning ensures concurrent SSE loads don't clobber this.
  await Future.wait([
    childrenCtrl.loadForFamily(familyId),
    childrenCtrl.loadRoutinesForFamily(familyId),
    calendarCtrl.loadWeek(familyId),
    notificationsCtrl.load(),
  ]);

  if (!context.mounted) return;

  // Recommendations need the updated children list, so run after.
  await recsCtrl.loadForChildren(childrenCtrl.children);

  debugPrint('[Refresh] refreshAllAfterAiAction completed — family=$familyId');
}

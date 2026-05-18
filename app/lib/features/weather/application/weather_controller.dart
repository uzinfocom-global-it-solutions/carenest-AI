import 'dart:async';
import 'package:flutter/foundation.dart';
import 'package:app/features/weather/data/weather_service.dart';
import 'package:app/features/weather/domain/weather_models.dart';

class WeatherController extends ChangeNotifier {
  WeatherController({required WeatherService service}) : _service = service;

  final WeatherService _service;

  bool isLoading = false;
  WeatherModel? weather;
  String? error;

  // Request versioning: only the latest load wins.
  int _loadVersion = 0;

  // Periodic refresh — keeps weather current without user pull-to-refresh.
  Timer? _refreshTimer;
  String? _lastLocationKey;
  double? _lastLat;
  double? _lastLon;

  Future<void> load(String locationKey, {double? lat, double? lon}) async {
    _lastLocationKey = locationKey;
    _lastLat = lat;
    _lastLon = lon;

    final v = ++_loadVersion;
    isLoading = true;
    error = null;
    notifyListeners();
    debugPrint('[Weather] Load started (v$v) key=$locationKey lat=$lat lon=$lon');
    try {
      final result = await _service.getCurrent(locationKey, latitude: lat, longitude: lon);
      if (v != _loadVersion) {
        debugPrint('[Weather] Stale response discarded (v$v, current v$_loadVersion)');
        return;
      }
      weather = result;
      debugPrint('[Weather] State updated (v$v): ${result.tempLabel} ${result.condition}');
    } catch (e) {
      if (v != _loadVersion) return;
      error = e.toString();
      debugPrint('[Weather] Load error (v$v): $e');
    } finally {
      if (v == _loadVersion) {
        isLoading = false;
        notifyListeners();
        debugPrint('[Weather] notifyListeners (v$v)');
      }
    }
  }

  /// Silently refresh in the background — does not set isLoading.
  Future<void> _silentRefresh() async {
    final key = _lastLocationKey;
    if (key == null) return;
    final v = ++_loadVersion;
    debugPrint('[Weather] Silent refresh (v$v) key=$key');
    try {
      final result = await _service.getCurrent(key, latitude: _lastLat, longitude: _lastLon);
      if (v != _loadVersion) return;
      weather = result;
      notifyListeners();
      debugPrint('[Weather] Silent refresh done (v$v): ${result.tempLabel}');
    } catch (e) {
      debugPrint('[Weather] Silent refresh error (v$v): $e');
    }
  }

  /// Starts a periodic background refresh every [interval] (default 30 min).
  /// Call once after the first successful load.
  void startPeriodicRefresh({Duration interval = const Duration(minutes: 30)}) {
    _refreshTimer?.cancel();
    _refreshTimer = Timer.periodic(interval, (_) => _silentRefresh());
    debugPrint('[Weather] Periodic refresh started: every ${interval.inMinutes}min');
  }

  void stopPeriodicRefresh() {
    _refreshTimer?.cancel();
    _refreshTimer = null;
  }

  @override
  void dispose() {
    stopPeriodicRefresh();
    super.dispose();
  }
}

import 'package:geolocator/geolocator.dart';

class LocationResult {
  const LocationResult({
    required this.latitude,
    required this.longitude,
    required this.locationKey,
  });

  final double latitude;
  final double longitude;
  final String locationKey;
}

class LocationService {
  Future<LocationResult?> getLocation() async {
    final serviceEnabled = await Geolocator.isLocationServiceEnabled();
    if (!serviceEnabled) return null;

    var permission = await Geolocator.checkPermission();
    if (permission == LocationPermission.denied) {
      permission = await Geolocator.requestPermission();
      if (permission == LocationPermission.denied) return null;
    }
    if (permission == LocationPermission.deniedForever) return null;

    try {
      final position = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.medium,
          timeLimit: Duration(seconds: 12),
        ),
      );

      final lat = (position.latitude * 10).round() / 10;
      final lon = (position.longitude * 10).round() / 10;
      final latStr = lat.toStringAsFixed(1).replaceAll('-', 'n').replaceAll('.', 'p');
      final lonStr = lon.toStringAsFixed(1).replaceAll('-', 'n').replaceAll('.', 'p');
      final locationKey = 'device_${latStr}_$lonStr';

      return LocationResult(
        latitude: position.latitude,
        longitude: position.longitude,
        locationKey: locationKey,
      );
    } catch (_) {
      return null;
    }
  }

  Future<bool> hasPermission() async {
    final permission = await Geolocator.checkPermission();
    return permission == LocationPermission.always ||
        permission == LocationPermission.whileInUse;
  }
}

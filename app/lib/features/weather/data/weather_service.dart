import 'package:app/core/constants/api_constants.dart';
import 'package:app/shared/api/api_client.dart';
import 'package:app/features/weather/domain/weather_models.dart';

class WeatherService {
  WeatherService({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  Future<WeatherModel> getCurrent(
    String locationKey, {
    double? latitude,
    double? longitude,
  }) async {
    var path =
        '${ApiConstants.weatherCurrent}?locationKey=${Uri.encodeQueryComponent(locationKey)}';
    if (latitude != null) path += '&latitude=$latitude';
    if (longitude != null) path += '&longitude=$longitude';
    final result = await _apiClient.get(path);
    return WeatherModel.fromJson(result as Map<String, dynamic>);
  }
}

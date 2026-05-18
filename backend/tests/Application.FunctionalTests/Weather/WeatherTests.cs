using Backend.Application.Weather.Queries.GetCurrentWeather;
using Backend.Domain.Entities;

namespace Backend.Application.FunctionalTests.Weather;

public class WeatherTests : TestBase
{
    [Test]
    public async Task GetCurrent_FetchesAndStoresSnapshot_WhenNoneExists()
    {
        var location = $"test-loc-{Guid.NewGuid():N}";

        var before = await TestApp.CountAsync<WeatherSnapshot>();

        var result = await TestApp.SendAsync(new GetCurrentWeatherQuery(location, 41.31, 69.24));

        result.LocationKey.ShouldBe(location);
        result.Id.ShouldBeGreaterThan(0);

        var after = await TestApp.CountAsync<WeatherSnapshot>();
        after.ShouldBe(before + 1);
    }

    [Test]
    public async Task GetCurrent_ReusesFreshSnapshot_DoesNotDuplicate()
    {
        var location = $"test-loc-{Guid.NewGuid():N}";

        var first = await TestApp.SendAsync(new GetCurrentWeatherQuery(location, 41.31, 69.24));
        var second = await TestApp.SendAsync(new GetCurrentWeatherQuery(location, 41.31, 69.24));

        // Same row served from cache / DB; no second fetch within 30-min staleness window.
        second.Id.ShouldBe(first.Id);

        var rows = await TestApp.CountWhereAsync<WeatherSnapshot>(w => w.LocationKey == location);
        rows.ShouldBe(1);
    }
}

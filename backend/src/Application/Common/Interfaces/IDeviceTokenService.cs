using Backend.Domain.Entities;

namespace Backend.Application.Common.Interfaces;

public interface IDeviceTokenService
{
    Task<DeviceToken> RegisterAsync(string userId, string token, string platform, string? deviceName, CancellationToken ct = default);
    Task UnregisterAsync(string userId, string token, CancellationToken ct = default);
    Task<IReadOnlyList<DeviceToken>> GetUserTokensAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<DeviceToken>> GetFamilyTokensAsync(int familyId, CancellationToken ct = default);
    Task TouchAsync(string token, CancellationToken ct = default);
}

using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public sealed class DeviceTokenService : IDeviceTokenService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<DeviceTokenService> _logger;

    public DeviceTokenService(IApplicationDbContext db, ILogger<DeviceTokenService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DeviceToken> RegisterAsync(
        string userId,
        string token,
        string platform,
        string? deviceName,
        CancellationToken ct = default)
    {
        var existing = await _db.DeviceTokens
            .FirstOrDefaultAsync(t => t.Token == token, ct);

        if (existing is not null)
        {
            existing.UserId = userId;
            existing.Platform = platform;
            existing.DeviceName = deviceName;
            existing.LastSeenAt = DateTimeOffset.UtcNow;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.IsActive = true;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Device token updated for user {UserId}", userId);
            return existing;
        }

        var entity = new DeviceToken
        {
            UserId = userId,
            Token = token,
            Platform = platform,
            DeviceName = deviceName,
            LastSeenAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsActive = true,
        };

        _db.DeviceTokens.Add(entity);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Device token registered for user {UserId} platform {Platform}", userId, platform);
        return entity;
    }

    public async Task UnregisterAsync(string userId, string token, CancellationToken ct = default)
    {
        var entity = await _db.DeviceTokens
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == token, ct);

        if (entity is null) return;

        entity.IsActive = false;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Device token unregistered for user {UserId}", userId);
    }

    public async Task<IReadOnlyList<DeviceToken>> GetUserTokensAsync(string userId, CancellationToken ct = default) =>
        await _db.DeviceTokens
            .Where(t => t.UserId == userId && t.IsActive)
            .OrderByDescending(t => t.LastSeenAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DeviceToken>> GetFamilyTokensAsync(int familyId, CancellationToken ct = default)
    {
        var userIds = await _db.FamilyMembers
            .Where(m => m.FamilyId == familyId && m.Status == Domain.Enums.FamilyMemberStatusEnum.Active)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        return await _db.DeviceTokens
            .Where(t => userIds.Contains(t.UserId) && t.IsActive)
            .ToListAsync(ct);
    }

    public async Task TouchAsync(string token, CancellationToken ct = default)
    {
        var entity = await _db.DeviceTokens
            .FirstOrDefaultAsync(t => t.Token == token, ct);

        if (entity is not null)
        {
            entity.LastSeenAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}

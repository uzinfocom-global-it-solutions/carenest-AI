using System.Security.Cryptography;
using System.Text;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Backend.Application.Common.Interfaces;

namespace Backend.Infrastructure.Services;

/// <summary>
/// Generates and checks idempotency keys for VoiceActions to prevent duplicate
/// reminders from parallel background workers or reconnect storms.
/// </summary>
public sealed class VoiceActionDeduplicationService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<VoiceActionDeduplicationService> _logger;

    public VoiceActionDeduplicationService(IApplicationDbContext db, ILogger<VoiceActionDeduplicationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Generates a deterministic key for a given trigger.
    /// sourceKey: a stable identifier for the trigger source (e.g. "medication:42", "weather:aqi-high").
    /// Window: 1-hour buckets — same trigger fires at most once per hour per user.
    /// </summary>
    public static string BuildKey(string userId, VoiceActionType type, string sourceKey, DateTimeOffset? at = null)
    {
        var bucket = (at ?? DateTimeOffset.UtcNow).ToString("yyyyMMddHH");
        var raw = $"{userId}:{type}:{sourceKey}:{bucket}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    /// <summary>
    /// Returns true if a VoiceAction with this idempotency key was already created
    /// (i.e. this is a duplicate trigger — skip creation).
    /// </summary>
    public async Task<bool> IsDuplicateAsync(string idempotencyKey, CancellationToken ct = default)
    {
        var exists = await _db.VoiceActions
            .AnyAsync(a => a.IdempotencyKey == idempotencyKey, ct);

        if (exists)
            _logger.LogDebug("VoiceAction duplicate suppressed: key={Key}", idempotencyKey);

        return exists;
    }
}

using System.Security.Cryptography;
using System.Text;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Backend.Application.Common.Interfaces;

namespace Backend.Infrastructure.Services;

public sealed class VoiceActionDeduplicationService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<VoiceActionDeduplicationService> _logger;

    public VoiceActionDeduplicationService(IApplicationDbContext db, ILogger<VoiceActionDeduplicationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public static string BuildKey(string userId, VoiceActionType type, string sourceKey, DateTimeOffset? at = null)
    {
        var bucket = (at ?? DateTimeOffset.UtcNow).ToString("yyyyMMddHH");
        var raw = $"{userId}:{type}:{sourceKey}:{bucket}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    public async Task<bool> IsDuplicateAsync(string idempotencyKey, CancellationToken ct = default)
    {
        var exists = await _db.VoiceActions
            .AnyAsync(a => a.IdempotencyKey == idempotencyKey, ct);

        if (exists)
            _logger.LogDebug("VoiceAction duplicate suppressed: key={Key}", idempotencyKey);

        return exists;
    }
}

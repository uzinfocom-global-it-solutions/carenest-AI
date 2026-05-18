using System.Text;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public sealed class AIMemoryService : IAIMemoryService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<AIMemoryService> _logger;

    private static readonly TimeSpan MemoryTtl = TimeSpan.FromDays(30);
    private const int MaxRelevanceScore = 200;
    private const int MinRelevanceScore = 0;
    private const int RelevanceIncrement = 10;
    private const int IgnoreDecrement = 15;
    private const int ConfirmBoost = 20;

    public AIMemoryService(IApplicationDbContext db, ILogger<AIMemoryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RecordObservationAsync(
        int familyId,
        string key,
        string value,
        AIMemoryType type,
        CancellationToken ct)
    {
        var existing = await _db.AIMemories
            .FirstOrDefaultAsync(m => m.FamilyId == familyId && m.Key == key, ct);

        var now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            existing.Value = value;
            existing.ObservationCount++;
            existing.LastObservedAt = now;
            existing.RelevanceScore = Math.Min(MaxRelevanceScore, existing.RelevanceScore + RelevanceIncrement);
            existing.ExpiresAt = now.Add(MemoryTtl);
        }
        else
        {
            var memory = new AIMemory
            {
                FamilyId = familyId,
                Key = key,
                Value = value,
                MemoryType = type,
                RelevanceScore = 100,
                ObservationCount = 1,
                LastObservedAt = now,
                ExpiresAt = now.Add(MemoryTtl),
                CreatedAt = now,
            };
            _db.AIMemories.Add(memory);
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record AI memory observation for family {FamilyId} key {Key}", familyId, key);
        }
    }

    public async Task<List<AIMemory>> GetFamilyMemoryAsync(int familyId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.AIMemories
            .Where(m => m.FamilyId == familyId && (m.ExpiresAt == null || m.ExpiresAt > now))
            .OrderByDescending(m => m.RelevanceScore)
            .ThenByDescending(m => m.LastObservedAt)
            .ToListAsync(ct);
    }

    public async Task<string> BuildMemoryContextAsync(int familyId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var memories = await _db.AIMemories
            .Where(m => m.FamilyId == familyId && (m.ExpiresAt == null || m.ExpiresAt > now))
            .OrderByDescending(m => m.RelevanceScore)
            .ThenByDescending(m => m.LastObservedAt)
            .Take(10)
            .ToListAsync(ct);

        if (memories.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("Память AI о семье:");
        foreach (var m in memories)
        {
            sb.AppendLine($"- [{m.MemoryType}] {m.Key}: {m.Value} (релевантность: {m.RelevanceScore}, наблюдений: {m.ObservationCount})");
        }
        return sb.ToString().TrimEnd();
    }

    public async Task RecordIgnoredReminderAsync(int familyId, string reminderType, CancellationToken ct)
    {
        var key = $"ignored:{reminderType}";

        var existing = await _db.AIMemories
            .FirstOrDefaultAsync(m => m.FamilyId == familyId && m.Key == key, ct);

        var now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            existing.ObservationCount++;
            existing.LastObservedAt = now;
            existing.RelevanceScore = Math.Max(MinRelevanceScore, existing.RelevanceScore - IgnoreDecrement);
            existing.Value = $"Напоминание '{reminderType}' проигнорировано {existing.ObservationCount} раз(а)";
            existing.ExpiresAt = now.Add(MemoryTtl);
        }
        else
        {
            _db.AIMemories.Add(new AIMemory
            {
                FamilyId = familyId,
                Key = key,
                Value = $"Напоминание '{reminderType}' проигнорировано 1 раз",
                MemoryType = AIMemoryType.IgnoredReminder,
                RelevanceScore = 100 - IgnoreDecrement,
                ObservationCount = 1,
                LastObservedAt = now,
                ExpiresAt = now.Add(MemoryTtl),
                CreatedAt = now,
            });
        }

        try
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogDebug("Recorded ignored reminder '{Type}' for family {FamilyId}", reminderType, familyId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record ignored reminder for family {FamilyId}", familyId);
        }
    }

    public async Task RecordConfirmedReminderAsync(int familyId, string reminderType, CancellationToken ct)
    {
        var key = $"confirmed:{reminderType}";

        var existing = await _db.AIMemories
            .FirstOrDefaultAsync(m => m.FamilyId == familyId && m.Key == key, ct);

        var now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            existing.ObservationCount++;
            existing.LastObservedAt = now;
            existing.RelevanceScore = Math.Min(MaxRelevanceScore, existing.RelevanceScore + ConfirmBoost);
            existing.Value = $"Напоминание '{reminderType}' подтверждено {existing.ObservationCount} раз(а)";
            existing.ExpiresAt = now.Add(MemoryTtl);
        }
        else
        {
            _db.AIMemories.Add(new AIMemory
            {
                FamilyId = familyId,
                Key = key,
                Value = $"Напоминание '{reminderType}' подтверждено 1 раз",
                MemoryType = AIMemoryType.EffectiveReminder,
                RelevanceScore = 100 + ConfirmBoost,
                ObservationCount = 1,
                LastObservedAt = now,
                ExpiresAt = now.Add(MemoryTtl),
                CreatedAt = now,
            });
        }

        try
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogDebug("Recorded confirmed reminder '{Type}' for family {FamilyId}", reminderType, familyId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record confirmed reminder for family {FamilyId}", familyId);
        }
    }
}

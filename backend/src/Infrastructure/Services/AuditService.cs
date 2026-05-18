using System.Text.Json;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly IApplicationDbContext _context;

    public AuditService(IApplicationDbContext context) => _context = context;

    public async Task LogAsync(string userId, string action, string entityType, int? entityId, object? metadata = null, CancellationToken ct = default)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Metadata = metadata != null ? JsonSerializer.Serialize(metadata) : null,
        });

        await _context.SaveChangesAsync(ct);
    }
}

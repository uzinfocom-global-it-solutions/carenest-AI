using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public sealed class FamilyItemService : IFamilyItemService
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<FamilyItemService> _logger;

    public FamilyItemService(IApplicationDbContext db, ILogger<FamilyItemService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<FamilyItem>> GetForFamilyAsync(int familyId, CancellationToken ct)
    {
        return await _db.FamilyItems
            .Where(i => i.FamilyId == familyId)
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name)
            .ToListAsync(ct);
    }

    public async Task<FamilyItem> CreateAsync(
        int familyId,
        int? childId,
        string name,
        FamilyItemCategory category,
        bool isRequired,
        string? notes,
        CancellationToken ct)
    {
        var item = new FamilyItem
        {
            FamilyId = familyId,
            ChildId = childId,
            Name = name,
            Category = category,
            IsRequired = isRequired,
            IsActive = true,
            Notes = notes,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.FamilyItems.Add(item);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("FamilyItem {Id} created for family {FamilyId}: {Name}", item.Id, familyId, name);
        return item;
    }

    public async Task<FamilyItem?> UpdateAsync(
        int id,
        int familyId,
        string name,
        FamilyItemCategory category,
        bool isRequired,
        bool isActive,
        string? notes,
        CancellationToken ct)
    {
        var item = await _db.FamilyItems
            .FirstOrDefaultAsync(i => i.Id == id && i.FamilyId == familyId, ct);

        if (item is null) return null;

        item.Name = name;
        item.Category = category;
        item.IsRequired = isRequired;
        item.IsActive = isActive;
        item.Notes = notes;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("FamilyItem {Id} updated for family {FamilyId}", id, familyId);
        return item;
    }

    public async Task DeleteAsync(int id, int familyId, CancellationToken ct)
    {
        var item = await _db.FamilyItems
            .FirstOrDefaultAsync(i => i.Id == id && i.FamilyId == familyId, ct);

        if (item is null) return;

        _db.FamilyItems.Remove(item);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("FamilyItem {Id} deleted from family {FamilyId}", id, familyId);
    }

    public async Task<List<FamilyItem>> GetChecklistItemsAsync(int familyId, int? childId, CancellationToken ct)
    {
        var query = _db.FamilyItems
            .Where(i => i.FamilyId == familyId && i.IsActive && i.IsRequired);

        if (childId.HasValue)
            query = query.Where(i => i.ChildId == null || i.ChildId == childId.Value);
        else
            query = query.Where(i => i.ChildId == null);

        return await query
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name)
            .ToListAsync(ct);
    }
}

using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Common.Interfaces;

public interface IFamilyItemService
{
    Task<List<FamilyItem>> GetForFamilyAsync(int familyId, CancellationToken ct);
    Task<FamilyItem> CreateAsync(int familyId, int? childId, string name, FamilyItemCategory category, bool isRequired, string? notes, CancellationToken ct);
    Task<FamilyItem?> UpdateAsync(int id, int familyId, string name, FamilyItemCategory category, bool isRequired, bool isActive, string? notes, CancellationToken ct);
    Task DeleteAsync(int id, int familyId, CancellationToken ct);
    Task<List<FamilyItem>> GetChecklistItemsAsync(int familyId, int? childId, CancellationToken ct);
}

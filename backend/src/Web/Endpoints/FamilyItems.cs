using Backend.Application.Common.Interfaces;
using Backend.Domain.Enums;
using Backend.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Backend.Web.Endpoints;

[Authorize]
public class FamilyItems : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/family-items";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();
        groupBuilder.MapGet(List, "");
        groupBuilder.MapPost(Create, "");
        groupBuilder.MapPut(Update, "{id:int}");
        groupBuilder.MapDelete(Delete, "{id:int}");
        groupBuilder.MapGet(GetChecklist, "checklist");
    }

    [EndpointSummary("List Family Items")]
    [EndpointDescription("Returns all checklist items for the current user's family.")]
    public static async Task<IResult> List(
        IApplicationDbContext db,
        IFamilyItemService service,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var familyId = await GetFamilyIdAsync(db, userId, ct);
        if (familyId is null) return Results.NotFound("Family not found");

        var items = await service.GetForFamilyAsync(familyId.Value, ct);
        return Results.Ok(items.Select(ToDto));
    }

    [EndpointSummary("Create Family Item")]
    [EndpointDescription("Creates a new checklist item for the current user's family.")]
    public static async Task<IResult> Create(
        CreateFamilyItemRequest req,
        IApplicationDbContext db,
        IFamilyItemService service,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var familyId = await GetFamilyIdAsync(db, userId, ct);
        if (familyId is null) return Results.NotFound("Family not found");

        if (!Enum.TryParse<FamilyItemCategory>(req.Category, ignoreCase: true, out var category))
            return Results.BadRequest($"Invalid category '{req.Category}'.");

        var item = await service.CreateAsync(
            familyId.Value, req.ChildId, req.Name, category, req.IsRequired, req.Notes, ct);

        return Results.Created($"/api/v1/family-items/{item.Id}", ToDto(item));
    }

    [EndpointSummary("Update Family Item")]
    [EndpointDescription("Updates an existing checklist item.")]
    public static async Task<IResult> Update(
        int id,
        UpdateFamilyItemRequest req,
        IApplicationDbContext db,
        IFamilyItemService service,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var familyId = await GetFamilyIdAsync(db, userId, ct);
        if (familyId is null) return Results.NotFound("Family not found");

        if (!Enum.TryParse<FamilyItemCategory>(req.Category, ignoreCase: true, out var category))
            return Results.BadRequest($"Invalid category '{req.Category}'.");

        var item = await service.UpdateAsync(
            id, familyId.Value, req.Name, category, req.IsRequired, req.IsActive, req.Notes, ct);

        return item is null ? Results.NotFound() : Results.Ok(ToDto(item));
    }

    [EndpointSummary("Delete Family Item")]
    [EndpointDescription("Deletes a checklist item from the current user's family.")]
    public static async Task<IResult> Delete(
        int id,
        IApplicationDbContext db,
        IFamilyItemService service,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var familyId = await GetFamilyIdAsync(db, userId, ct);
        if (familyId is null) return Results.NotFound("Family not found");

        await service.DeleteAsync(id, familyId.Value, ct);
        return Results.NoContent();
    }

    [EndpointSummary("Get Checklist Items")]
    [EndpointDescription("Returns active required items for leave-home checklist, optionally filtered by child.")]
    public static async Task<IResult> GetChecklist(
        int? childId,
        IApplicationDbContext db,
        IFamilyItemService service,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var familyId = await GetFamilyIdAsync(db, userId, ct);
        if (familyId is null) return Results.NotFound("Family not found");

        var items = await service.GetChecklistItemsAsync(familyId.Value, childId, ct);
        return Results.Ok(items.Select(ToDto));
    }

    private static async Task<int?> GetFamilyIdAsync(IApplicationDbContext db, string userId, CancellationToken ct)
    {
        return await db.FamilyMembers
            .Where(m => m.UserId == userId)
            .Select(m => (int?)m.FamilyId)
            .FirstOrDefaultAsync(ct);
    }

    private static FamilyItemDto ToDto(Domain.Entities.FamilyItem i) => new(
        i.Id, i.FamilyId, i.ChildId, i.Name,
        i.Category.ToString(), i.IsRequired, i.IsActive,
        i.Notes, i.CreatedAt, i.UpdatedAt);
}

public record CreateFamilyItemRequest(
    string Name,
    string Category,
    int? ChildId = null,
    bool IsRequired = true,
    string? Notes = null);

public record UpdateFamilyItemRequest(
    string Name,
    string Category,
    bool IsRequired,
    bool IsActive,
    string? Notes);

public record FamilyItemDto(
    int Id,
    int FamilyId,
    int? ChildId,
    string Name,
    string Category,
    bool IsRequired,
    bool IsActive,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

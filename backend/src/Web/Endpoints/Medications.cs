using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Backend.Web.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Backend.Web.Endpoints;

public class Medications : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/medications";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();
        groupBuilder.MapPost(Create, "");
        groupBuilder.MapGet(List, "");
        groupBuilder.MapDelete(Delete, "{id:int}");
        groupBuilder.MapPut(Toggle, "{id:int}/toggle");
    }

    [EndpointSummary("Create Medication Schedule")]
    public static async Task<IResult> Create(
        CreateMedicationRequest req,
        IApplicationDbContext db,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var child = await db.Children
            .Include(c => c.Family)
            .ThenInclude(f => f.Members)
            .FirstOrDefaultAsync(c => c.Id == req.ChildId, ct);

        if (child is null) return Results.NotFound("Child not found");
        if (!child.Family.Members.Any(m => m.UserId == userId))
            return Results.Forbid();

        var schedule = new MedicationSchedule
        {
            ChildId = req.ChildId,
            MedicationName = req.MedicationName,
            Dosage = req.Dosage,
            ScheduleTime = req.ScheduleTime,
            Timezone = req.Timezone,
            RepeatPattern = req.RepeatPattern,
            VoiceEnabled = req.VoiceEnabled,
            PushEnabled = req.PushEnabled,
            RequiresConfirmation = req.RequiresConfirmation,
            EscalationEnabled = req.EscalationEnabled,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.MedicationSchedules.Add(schedule);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/medications/{schedule.Id}", ToDto(schedule));
    }

    [EndpointSummary("List Medication Schedules")]
    public static async Task<IResult> List(
        IApplicationDbContext db,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var familyIds = await db.FamilyMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.FamilyId)
            .ToListAsync(ct);

        var childIds = await db.Children
            .Where(c => familyIds.Contains(c.FamilyId))
            .Select(c => c.Id)
            .ToListAsync(ct);

        var schedules = await db.MedicationSchedules
            .Where(s => childIds.Contains(s.ChildId))
            .OrderBy(s => s.ScheduleTime)
            .ToListAsync(ct);

        return Results.Ok(schedules.Select(ToDto));
    }

    [EndpointSummary("Delete Medication Schedule")]
    public static async Task<IResult> Delete(
        int id,
        IApplicationDbContext db,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var schedule = await db.MedicationSchedules
            .Include(s => s.Child).ThenInclude(c => c.Family).ThenInclude(f => f.Members)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (schedule is null) return Results.NotFound();
        if (!schedule.Child.Family.Members.Any(m => m.UserId == userId)) return Results.Forbid();

        db.MedicationSchedules.Remove(schedule);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    [EndpointSummary("Toggle Medication Schedule")]
    public static async Task<IResult> Toggle(
        int id,
        IApplicationDbContext db,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var schedule = await db.MedicationSchedules
            .Include(s => s.Child).ThenInclude(c => c.Family).ThenInclude(f => f.Members)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (schedule is null) return Results.NotFound();
        if (!schedule.Child.Family.Members.Any(m => m.UserId == userId)) return Results.Forbid();

        schedule.IsActive = !schedule.IsActive;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { schedule.Id, schedule.IsActive });
    }

    private static MedicationScheduleDto ToDto(MedicationSchedule s) => new(
        s.Id, s.ChildId, s.MedicationName, s.Dosage,
        s.ScheduleTime, s.Timezone, s.RepeatPattern,
        s.VoiceEnabled, s.PushEnabled, s.RequiresConfirmation,
        s.EscalationEnabled, s.IsActive, s.LastTriggeredAt, s.CreatedAt);
}

public record CreateMedicationRequest(
    int ChildId,
    string MedicationName,
    string Dosage,
    TimeOnly ScheduleTime,
    string Timezone,
    string RepeatPattern = "daily",
    bool VoiceEnabled = true,
    bool PushEnabled = true,
    bool RequiresConfirmation = true,
    bool EscalationEnabled = true);

public record MedicationScheduleDto(
    int Id,
    int ChildId,
    string MedicationName,
    string Dosage,
    TimeOnly ScheduleTime,
    string Timezone,
    string RepeatPattern,
    bool VoiceEnabled,
    bool PushEnabled,
    bool RequiresConfirmation,
    bool EscalationEnabled,
    bool IsActive,
    DateTimeOffset? LastTriggeredAt,
    DateTimeOffset CreatedAt);

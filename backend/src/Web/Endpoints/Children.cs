using Backend.Application.Children.Commands.AddChildNote;
using Backend.Application.Children.Commands.AddChildRoutine;
using Backend.Application.Children.Commands.ConfirmChildNote;
using Backend.Application.Children.Commands.CreateChild;
using Backend.Application.Children.Commands.ExtractFromChatMessage;
using Backend.Application.Children.Commands.UpdateChild;
using Backend.Application.Children.Commands.UpdateSensitivity;
using Backend.Application.Children.Models;
using Backend.Application.Children.Queries.GetChild;
using Backend.Application.Children.Queries.ListChildrenByFamily;
using Backend.Application.Common.Interfaces;
using Backend.Application.Common.Security;
using Backend.Web.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Backend.Web.Endpoints;

public class Children : IEndpointGroup
{
    public static string? RoutePrefix => "/children";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();
        groupBuilder.MapPost(Create, "");
        groupBuilder.MapGet(Get, "{childId:int}");
        groupBuilder.MapGet(ListByFamily, "by-family/{familyId:int}");
        groupBuilder.MapGet(ListRoutinesByFamily, "by-family/{familyId:int}/routines");
        groupBuilder.MapPut(Update, "{childId:int}");
        groupBuilder.MapGet(GetSensitivity, "{childId:int}/sensitivity");
        groupBuilder.MapPut(UpdateSensitivity, "{childId:int}/sensitivity");
        groupBuilder.MapGet(ListNotes, "{childId:int}/notes");
        groupBuilder.MapPost(AddNote, "{childId:int}/notes");
        groupBuilder.MapPost(AddRoutine, "{childId:int}/routines");
        groupBuilder.MapPost(ConfirmNote, "notes/{noteId:int}/confirm");
        groupBuilder.MapPost(ExtractFromMessage, "{childId:int}/extract-from-message");
    }

    [EndpointSummary("Create Child")]
    [EndpointDescription("Creates a child profile under a family. Auto-assigns AgeGroup and default sensitivity profile.")]
    public static async Task<IResult> Create(
        CreateChildRequest req,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await mediator.Send(
            new CreateChildCommand(req.FamilyId, req.DisplayName, req.AgeYears, userId), ct);
        return Results.Created($"/children/{result.Id}", result);
    }

    [EndpointSummary("Get Child")]
    [EndpointDescription("Returns a single child by id. Caller must be an active member of the child's family.")]
    public static async Task<IResult> Get(
        int childId,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await mediator.Send(new GetChildQuery(childId, userId), ct);
        return Results.Ok(result);
    }

    [EndpointSummary("List Children by Family")]
    [EndpointDescription("Returns all children belonging to a family. Caller must be an active member.")]
    public static async Task<IResult> ListByFamily(
        int familyId,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var results = await mediator.Send(new ListChildrenByFamilyQuery(familyId, userId), ct);
        return Results.Ok(results);
    }

    [EndpointSummary("Update Child")]
    [EndpointDescription("Updates a child's display name and age. AgeGroup is recomputed automatically.")]
    public static async Task<IResult> Update(
        int childId,
        UpdateChildRequest req,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await mediator.Send(
            new UpdateChildCommand(childId, req.DisplayName, req.AgeYears, userId), ct);
        return Results.Ok(result);
    }

    [EndpointSummary("Get Child Sensitivity")]
    [EndpointDescription("Returns the current sensitivity profile for a child.")]
    public static async Task<IResult> GetSensitivity(
        int childId,
        IApplicationDbContext db,
        IFamilyAuthorization auth,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var child = await db.Children.FindAsync([childId], ct);
        if (child is null) return Results.NotFound();
        await auth.AssertMemberAsync(child.FamilyId, userId, ct);

        var s = await db.ChildSensitivities
            .FirstOrDefaultAsync(x => x.ChildId == childId, ct);

        return Results.Ok(new ChildSensitivityDto(
            s?.HeatSensitive ?? false,
            s?.ColdSensitive ?? false,
            s?.AirQualitySensitive ?? false,
            s?.PollenSensitive ?? false,
            s?.UvSensitive ?? false,
            s?.SkinSensitive ?? false,
            s?.ActivitySensitive ?? false,
            s?.RespiratorySensitive ?? false));
    }

    [EndpointSummary("Update Child Sensitivity")]
    [EndpointDescription("Updates the sensitivity profile for a child.")]
    public static async Task<IResult> UpdateSensitivity(
        int childId,
        ChildSensitivityDto sensitivity,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        await mediator.Send(new UpdateSensitivityCommand(childId, sensitivity, userId), ct);
        return Results.Ok();
    }

    [EndpointSummary("List Child Notes")]
    [EndpointDescription("Returns all notes for a child, newest first. Optionally filter by noteType.")]
    public static async Task<IResult> ListNotes(
        int childId,
        IApplicationDbContext db,
        IFamilyAuthorization auth,
        HttpContext ctx,
        CancellationToken ct,
        string? noteType = null)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var child = await db.Children.FindAsync([childId], ct);
        if (child is null) return Results.NotFound();
        await auth.AssertMemberAsync(child.FamilyId, userId, ct);

        var query = db.ChildNotes.Where(n => n.ChildId == childId);
        if (!string.IsNullOrWhiteSpace(noteType)
            && Enum.TryParse<Backend.Domain.Enums.NoteTypeEnum>(noteType, ignoreCase: true, out var nt))
            query = query.Where(n => n.NoteType == nt);

        var notes = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new ChildNoteResult(
                n.Id, n.ChildId,
                n.NoteType.ToString(),
                n.Note,
                n.Source.ToString(),
                n.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(notes);
    }

    [EndpointSummary("Add Child Note")]
    [EndpointDescription("Adds a note to a child's profile.")]
    public static async Task<IResult> AddNote(
        int childId,
        ChildNoteDto note,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await mediator.Send(new AddChildNoteCommand(childId, note, userId), ct);
        return Results.Created($"/children/{childId}/notes/{result.Id}", result);
    }

    [EndpointSummary("Add Child Routine")]
    [EndpointDescription("Adds a recurring routine (sleep, meal, sport, etc.) to a child's profile.")]
    public static async Task<IResult> AddRoutine(
        int childId,
        ChildRoutineDto routine,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await mediator.Send(new AddChildRoutineCommand(childId, routine, userId), ct);
        return Results.Created($"/children/{childId}/routines/{result.Id}", result);
    }

    [EndpointSummary("Confirm Child Note")]
    [EndpointDescription("Confirms an AI-extracted note that was flagged for human review. Idempotent.")]
    public static async Task<IResult> ConfirmNote(
        int noteId,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        await mediator.Send(new ConfirmChildNoteCommand(noteId, userId), ct);
        return Results.Ok();
    }

    [EndpointSummary("List Routines by Family")]
    [EndpointDescription("Returns all active routines for all children in a family, ordered by start time.")]
    public static async Task<IResult> ListRoutinesByFamily(
        int familyId,
        IApplicationDbContext db,
        IFamilyAuthorization auth,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        await auth.AssertMemberAsync(familyId, userId, ct);

        var raw = await db.ChildRoutines
            .Where(r => r.Child.FamilyId == familyId && r.Active)
            .OrderBy(r => r.StartTime)
            .Select(r => new {
                r.Id, r.ChildId, r.Title,
                r.RoutineType, r.StartTime, r.EndTime,
                r.RepeatPattern, r.LocationType,
                r.WeatherSensitive, r.Active
            })
            .ToListAsync(ct);

        var result = raw.Select(r => new ChildRoutineListItem(
            r.Id, r.ChildId, r.Title,
            r.RoutineType.ToString(),
            r.StartTime, r.EndTime,
            r.RepeatPattern.ToString(),
            r.LocationType.ToString(),
            r.WeatherSensitive, r.Active));

        return Results.Ok(result);
    }

    [EndpointSummary("Extract Child Info from Chat Message")]
    [EndpointDescription("Runs the AI extraction pipeline against a chat message. High-confidence insights are auto-applied; lower-confidence ones land as notes awaiting confirmation.")]
    public static async Task<IResult> ExtractFromMessage(
        int childId,
        ExtractFromMessageRequest req,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await mediator.Send(
            new ExtractFromChatMessageCommand(childId, req.ChatMessageId, userId), ct);
        return Results.Ok(result);
    }
}

public record CreateChildRequest(int FamilyId, string DisplayName, int AgeYears);
public record UpdateChildRequest(string DisplayName, int AgeYears);
public record ExtractFromMessageRequest(int ChatMessageId);

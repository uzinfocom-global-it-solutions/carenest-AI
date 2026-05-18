using Backend.Application.Calendar.Commands.CreateCalendarEvent;
using Backend.Application.Calendar.Commands.DeleteCalendarEvent;
using Backend.Application.Calendar.Commands.UpdateCalendarEvent;
using Backend.Application.Calendar.Models;
using Backend.Application.Calendar.Queries.GetCalendarEvent;
using Backend.Application.Calendar.Queries.ListCalendarEvents;
using Backend.Web.Infrastructure;
using MediatR;

namespace Backend.Web.Endpoints;

public class Calendar : IEndpointGroup
{
    public static string? RoutePrefix => "/calendar";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();
        groupBuilder.MapPost(Create, "families/{familyId:int}/events");
        groupBuilder.MapGet(List, "families/{familyId:int}/events");
        groupBuilder.MapGet(Get, "events/{eventId:int}");
        groupBuilder.MapPut(Update, "events/{eventId:int}");
        groupBuilder.MapDelete(Delete, "events/{eventId:int}");
    }

    [EndpointSummary("Create Calendar Event")]
    [EndpointDescription("Creates a new calendar event for a family. If childId is supplied, child must belong to the same family.")]
    public static async Task<IResult> Create(
        int familyId,
        CalendarEventDto body,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await mediator.Send(new CreateCalendarEventCommand(familyId, body, userId), ct);
        return Results.Created($"/calendar/events/{result.Id}", result);
    }

    [EndpointSummary("List Calendar Events")]
    [EndpointDescription("Returns events for a family between [from, to). Optional childId filter.")]
    public static async Task<IResult> List(
        int familyId,
        DateTimeOffset from,
        DateTimeOffset to,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct,
        int? childId = null)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var results = await mediator.Send(
            new ListCalendarEventsQuery(familyId, from, to, childId, userId), ct);
        return Results.Ok(results);
    }

    [EndpointSummary("Get Calendar Event")]
    public static async Task<IResult> Get(
        int eventId,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await mediator.Send(new GetCalendarEventQuery(eventId, userId), ct);
        return Results.Ok(result);
    }

    [EndpointSummary("Update Calendar Event")]
    public static async Task<IResult> Update(
        int eventId,
        CalendarEventDto body,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        var result = await mediator.Send(new UpdateCalendarEventCommand(eventId, body, userId), ct);
        return Results.Ok(result);
    }

    [EndpointSummary("Delete Calendar Event")]
    public static async Task<IResult> Delete(
        int eventId,
        ISender mediator,
        HttpContext ctx,
        CancellationToken ct)
    {
        var userId = ctx.User.FindFirst("sub")?.Value ?? string.Empty;
        await mediator.Send(new DeleteCalendarEventCommand(eventId, userId), ct);
        return Results.NoContent();
    }
}

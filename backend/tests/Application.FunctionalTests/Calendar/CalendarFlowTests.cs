using Backend.Application.Auth.Commands.Register;
using Backend.Application.Calendar.Commands.CreateCalendarEvent;
using Backend.Application.Calendar.Commands.DeleteCalendarEvent;
using Backend.Application.Calendar.Commands.UpdateCalendarEvent;
using Backend.Application.Calendar.Models;
using Backend.Application.Calendar.Queries.GetCalendarEvent;
using Backend.Application.Calendar.Queries.ListCalendarEvents;
using Backend.Application.Children.Commands.CreateChild;
using Backend.Application.Common.Exceptions;
using Backend.Application.Families.Commands.CreateFamily;
using Backend.Domain.Entities;

namespace Backend.Application.FunctionalTests.Calendar;

public class CalendarFlowTests : TestBase
{
    [Test]
    public async Task CreateEvent_PersistsAndIsRetrievable()
    {
        var (userId, familyId) = await SetupFamilyAsync("cal-create@local");

        var dto = NewEventDto(
            start: DateTimeOffset.UtcNow.AddDays(1),
            end: DateTimeOffset.UtcNow.AddDays(1).AddHours(2));

        var created = await TestApp.SendAsync(new CreateCalendarEventCommand(familyId, dto, userId));

        var fetched = await TestApp.SendAsync(new GetCalendarEventQuery(created.Id, userId));
        fetched.Title.ShouldBe(dto.Title);
        fetched.FamilyId.ShouldBe(familyId);
    }

    [Test]
    public async Task CreateEvent_LinkedChildMustBelongToSameFamily()
    {
        var (aliceId, familyA) = await SetupFamilyAsync("cal-iso-a@local");
        var (_, familyB) = await SetupFamilyAsync("cal-iso-b@local");
        var foreignChild = await TestApp.SendAsync(new CreateChildCommand(familyB, "Foreign", 6, "noop"));

        // Alice tries to create an event in HER family but linked to a child from family B.
        // Authorization must fail before we even get to the cross-family check (Alice isn't a member of B's child path),
        // but the validator-level check is the safety net we expect to fire.
        var dto = NewEventDto(
            start: DateTimeOffset.UtcNow.AddDays(1),
            end: DateTimeOffset.UtcNow.AddDays(1).AddHours(1)) with
        { ChildId = foreignChild.Id };

        await Should.ThrowAsync<ValidationException>(
            () => TestApp.SendAsync(new CreateCalendarEventCommand(familyA, dto, aliceId)));
    }

    [Test]
    public async Task CreateEvent_RejectsEndBeforeStart()
    {
        var (userId, familyId) = await SetupFamilyAsync("cal-bad-time@local");

        var dto = NewEventDto(
            start: DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            end: DateTimeOffset.UtcNow.AddDays(1));

        await Should.ThrowAsync<ValidationException>(
            () => TestApp.SendAsync(new CreateCalendarEventCommand(familyId, dto, userId)));
    }

    [Test]
    public async Task ListEvents_FiltersByDateRange()
    {
        var (userId, familyId) = await SetupFamilyAsync("cal-range@local");

        await TestApp.SendAsync(new CreateCalendarEventCommand(familyId, NewEventDto(
            start: DateTimeOffset.UtcNow.AddDays(1),
            end: DateTimeOffset.UtcNow.AddDays(1).AddHours(1)), userId));

        await TestApp.SendAsync(new CreateCalendarEventCommand(familyId, NewEventDto(
            start: DateTimeOffset.UtcNow.AddDays(10),
            end: DateTimeOffset.UtcNow.AddDays(10).AddHours(1)), userId));

        var inWindow = await TestApp.SendAsync(new ListCalendarEventsQuery(
            familyId,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(5),
            ChildId: null,
            userId));

        inWindow.Count.ShouldBe(1);
    }

    [Test]
    public async Task UpdateEvent_PersistsChanges()
    {
        var (userId, familyId) = await SetupFamilyAsync("cal-update@local");

        var created = await TestApp.SendAsync(new CreateCalendarEventCommand(familyId, NewEventDto(
            start: DateTimeOffset.UtcNow.AddDays(1),
            end: DateTimeOffset.UtcNow.AddDays(1).AddHours(1)), userId));

        var updateDto = NewEventDto(
            start: DateTimeOffset.UtcNow.AddDays(2),
            end: DateTimeOffset.UtcNow.AddDays(2).AddHours(3)) with
        {
            Title = "Renamed",
            WeatherSensitive = true,
        };

        var updated = await TestApp.SendAsync(new UpdateCalendarEventCommand(created.Id, updateDto, userId));

        updated.Title.ShouldBe("Renamed");
        updated.WeatherSensitive.ShouldBeTrue();
    }

    [Test]
    public async Task DeleteEvent_RemovesFromDb()
    {
        var (userId, familyId) = await SetupFamilyAsync("cal-delete@local");

        var created = await TestApp.SendAsync(new CreateCalendarEventCommand(familyId, NewEventDto(
            start: DateTimeOffset.UtcNow.AddDays(1),
            end: DateTimeOffset.UtcNow.AddDays(1).AddHours(1)), userId));

        await TestApp.SendAsync(new DeleteCalendarEventCommand(created.Id, userId));

        var stored = await TestApp.FindAsync<CalendarEvent>(created.Id);
        stored.ShouldBeNull();
    }

    [Test]
    public async Task NonMember_CannotCreateEvent()
    {
        var (_, familyId) = await SetupFamilyAsync("cal-rbac-owner@local");
        var outsider = await TestApp.SendAsync(
            new RegisterCommand("cal-rbac-out@local", "Password123!", null, null));

        var dto = NewEventDto(
            start: DateTimeOffset.UtcNow.AddDays(1),
            end: DateTimeOffset.UtcNow.AddDays(1).AddHours(1));

        await Should.ThrowAsync<ForbiddenAccessException>(
            () => TestApp.SendAsync(new CreateCalendarEventCommand(familyId, dto, outsider.UserId)));
    }

    private static CalendarEventDto NewEventDto(DateTimeOffset start, DateTimeOffset end) => new(
        ChildId: null,
        Title: "Doctor visit",
        Description: "Annual checkup",
        StartDatetime: start,
        EndDatetime: end,
        LocationLabel: "Clinic",
        LocationKey: "clinic-1",
        Latitude: 41.31,
        Longitude: 69.24,
        LocationType: "Indoor",
        ActivityIntensity: "Low",
        WeatherSensitive: false);

    private static async Task<(string UserId, int FamilyId)> SetupFamilyAsync(string email)
    {
        var auth = await TestApp.SendAsync(new RegisterCommand(email, "Password123!", null, null));
        var family = await TestApp.SendAsync(
            new CreateFamilyCommand("Family", auth.UserId, null, null, null));
        return (auth.UserId, family.Id);
    }
}

using Backend.Application.Auth.Commands.Register;
using Backend.Application.Children.Commands.AddChildNote;
using Backend.Application.Children.Commands.AddChildRoutine;
using Backend.Application.Children.Commands.CreateChild;
using Backend.Application.Children.Commands.UpdateChild;
using Backend.Application.Children.Commands.UpdateSensitivity;
using Backend.Application.Children.Models;
using Backend.Application.Children.Queries.GetChild;
using Backend.Application.Children.Queries.ListChildrenByFamily;
using Backend.Application.Common.Exceptions;
using Backend.Application.Families.Commands.CreateFamily;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.FunctionalTests.Children;

public class ChildWorkflowTests : TestBase
{
    [Test]
    public async Task CreateChild_AssignsAgeGroupAndDefaultSensitivity()
    {
        var (userId, familyId) = await SetupFamilyAsync("child-create@local");

        var child = await TestApp.SendAsync(new CreateChildCommand(familyId, "Tim", 5, userId));

        child.AgeGroup.ShouldBe(AgeGroupEnum.Child);

        var sensitivity = await TestApp.FindFirstAsync<ChildSensitivity>(s => s.ChildId == child.Id);
        sensitivity.ShouldNotBeNull();
        sensitivity!.HeatSensitive.ShouldBeFalse();
    }

    [Test]
    public async Task UpdateChild_RecomputesAgeGroupAndPersists()
    {
        var (userId, familyId) = await SetupFamilyAsync("child-update@local");
        var child = await TestApp.SendAsync(new CreateChildCommand(familyId, "Anna", 2, userId));
        child.AgeGroup.ShouldBe(AgeGroupEnum.Toddler);

        var updated = await TestApp.SendAsync(new UpdateChildCommand(child.Id, "Anna B.", 11, userId));

        updated.DisplayName.ShouldBe("Anna B.");
        updated.AgeYears.ShouldBe(11);
        updated.AgeGroup.ShouldBe(AgeGroupEnum.Preteen);
    }

    [Test]
    public async Task GetChild_ReturnsForFamilyMember()
    {
        var (userId, familyId) = await SetupFamilyAsync("child-get@local");
        var child = await TestApp.SendAsync(new CreateChildCommand(familyId, "Sam", 8, userId));

        var fetched = await TestApp.SendAsync(new GetChildQuery(child.Id, userId));
        fetched.Id.ShouldBe(child.Id);
    }

    [Test]
    public async Task GetChild_RejectsNonMember()
    {
        var (ownerId, familyId) = await SetupFamilyAsync("child-isolation-a@local");
        var outsider = await TestApp.SendAsync(
            new RegisterCommand("child-isolation-b@local", "Password123!", null, null));

        var child = await TestApp.SendAsync(new CreateChildCommand(familyId, "Kate", 7, ownerId));

        await Should.ThrowAsync<ForbiddenAccessException>(
            () => TestApp.SendAsync(new GetChildQuery(child.Id, outsider.UserId)));
    }

    [Test]
    public async Task ListChildrenByFamily_ReturnsAllChildren()
    {
        var (userId, familyId) = await SetupFamilyAsync("child-list@local");
        await TestApp.SendAsync(new CreateChildCommand(familyId, "Lia", 3, userId));
        await TestApp.SendAsync(new CreateChildCommand(familyId, "Ben", 6, userId));
        await TestApp.SendAsync(new CreateChildCommand(familyId, "Eva", 9, userId));

        var children = await TestApp.SendAsync(new ListChildrenByFamilyQuery(familyId, userId));

        children.Count.ShouldBe(3);
        children.Select(c => c.DisplayName).ShouldContain("Lia");
        children.Select(c => c.DisplayName).ShouldContain("Eva");
    }

    [Test]
    public async Task UpdateSensitivity_OverwritesAllFlags()
    {
        var (userId, familyId) = await SetupFamilyAsync("child-sens@local");
        var child = await TestApp.SendAsync(new CreateChildCommand(familyId, "Max", 4, userId));

        var dto = new ChildSensitivityDto(
            HeatSensitive: true, ColdSensitive: false, AirQualitySensitive: true,
            PollenSensitive: false, UvSensitive: true, SkinSensitive: true,
            ActivitySensitive: false, RespiratorySensitive: true);

        await TestApp.SendAsync(new UpdateSensitivityCommand(child.Id, dto, userId));

        var sensitivity = await TestApp.FindFirstAsync<ChildSensitivity>(s => s.ChildId == child.Id);
        sensitivity!.HeatSensitive.ShouldBeTrue();
        sensitivity.UvSensitive.ShouldBeTrue();
        sensitivity.PollenSensitive.ShouldBeFalse();
    }

    [Test]
    public async Task AddNote_PersistsAndCascades()
    {
        var (userId, familyId) = await SetupFamilyAsync("child-note@local");
        var child = await TestApp.SendAsync(new CreateChildCommand(familyId, "Eli", 5, userId));

        var note = new ChildNoteDto(
            NoteType: "Health", Note: "Coughing at night",
            Source: "Voice", ConfidenceScore: 0.8);

        var result = await TestApp.SendAsync(new AddChildNoteCommand(child.Id, note, userId));

        result.NoteType.ShouldBe("Health");
        result.Source.ShouldBe("Voice");

        var stored = await TestApp.FindAsync<ChildNote>(result.Id);
        stored.ShouldNotBeNull();
        stored!.ChildId.ShouldBe(child.Id);
    }

    [Test]
    public async Task AddRoutine_PersistsAndIndexes()
    {
        var (userId, familyId) = await SetupFamilyAsync("child-routine@local");
        var child = await TestApp.SendAsync(new CreateChildCommand(familyId, "Nora", 7, userId));

        var routine = new ChildRoutineDto(
            Title: "Bedtime", RoutineType: "Sleep",
            StartTime: new TimeOnly(21, 0),
            EndTime: new TimeOnly(7, 0),
            RepeatPattern: "Daily",
            LocationType: "Indoor",
            ActivityIntensity: "Low",
            WeatherSensitive: false);

        var result = await TestApp.SendAsync(new AddChildRoutineCommand(child.Id, routine, userId));

        result.Title.ShouldBe("Bedtime");
        result.RoutineType.ShouldBe("Sleep");
        result.Active.ShouldBeTrue();
    }

    [Test]
    public async Task AddRoutine_RejectsInvalidEnum()
    {
        var (userId, familyId) = await SetupFamilyAsync("child-routine-bad@local");
        var child = await TestApp.SendAsync(new CreateChildCommand(familyId, "Rio", 7, userId));

        var routine = new ChildRoutineDto(
            "Bedtime", RoutineType: "Garbage",
            new TimeOnly(21, 0), null,
            "Daily", "Indoor", "Low", false);

        await Should.ThrowAsync<ValidationException>(
            () => TestApp.SendAsync(new AddChildRoutineCommand(child.Id, routine, userId)));
    }

    private static async Task<(string UserId, int FamilyId)> SetupFamilyAsync(string email)
    {
        var auth = await TestApp.SendAsync(new RegisterCommand(email, "Password123!", null, null));
        var family = await TestApp.SendAsync(
            new CreateFamilyCommand("Family", auth.UserId, null, null, null));
        return (auth.UserId, family.Id);
    }
}

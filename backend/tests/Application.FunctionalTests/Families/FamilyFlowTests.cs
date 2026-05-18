using Backend.Application.Auth.Commands.Register;
using Backend.Application.Children.Commands.CreateChild;
using Backend.Application.Common.Exceptions;
using Backend.Application.Families.Commands.AcceptInvitation;
using Backend.Application.Families.Commands.AddFamilyMember;
using Backend.Application.Families.Commands.CreateFamily;
using Backend.Application.Families.Commands.RemoveMember;
using Backend.Application.Families.Commands.UpdateMemberRole;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.FunctionalTests.Families;

public class FamilyFlowTests : TestBase
{
    [Test]
    public async Task CreateFamily_AutoAssignsCreatorAsActiveParent()
    {
        var auth = await TestApp.SendAsync(
            new RegisterCommand("fam-creator@local", "Password123!", null, null));

        var family = await TestApp.SendAsync(new CreateFamilyCommand(
            "Smiths", auth.UserId, null, null, null));

        var member = await TestApp.FindFirstAsync<FamilyMember>(
            m => m.FamilyId == family.Id && m.UserId == auth.UserId);

        member.ShouldNotBeNull();
        member!.Role.ShouldBe(RoleEnum.Parent);
        member.Status.ShouldBe(FamilyMemberStatusEnum.Active);
    }

    [Test]
    public async Task FamiliesAreIsolated_NonMemberCannotCreateChild()
    {
        var alice = await TestApp.SendAsync(new RegisterCommand("alice@local", "Password123!", null, null));
        var bob = await TestApp.SendAsync(new RegisterCommand("bob@local", "Password123!", null, null));

        var aliceFamily = await TestApp.SendAsync(new CreateFamilyCommand(
            "Alice's Family", alice.UserId, null, null, null));

        // Bob tries to create a child in Alice's family — must be rejected.
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => TestApp.SendAsync(new CreateChildCommand(aliceFamily.Id, "Charlie", 4, bob.UserId)));
    }

    [Test]
    public async Task AcceptInvitation_TransitionsInvitedToActive()
    {
        var alice = await TestApp.SendAsync(new RegisterCommand("invite-a@local", "Password123!", null, null));
        var bob = await TestApp.SendAsync(new RegisterCommand("invite-b@local", "Password123!", null, null));

        var family = await TestApp.SendAsync(new CreateFamilyCommand(
            "Invitable", alice.UserId, null, null, null));

        await TestApp.SendAsync(new AddFamilyMemberCommand(family.Id, bob.UserId, "Parent", alice.UserId));

        var memberBefore = await TestApp.FindFirstAsync<FamilyMember>(
            m => m.FamilyId == family.Id && m.UserId == bob.UserId);
        memberBefore!.Status.ShouldBe(FamilyMemberStatusEnum.Invited);

        await TestApp.SendAsync(new AcceptInvitationCommand(family.Id, bob.UserId));

        var memberAfter = await TestApp.FindFirstAsync<FamilyMember>(
            m => m.FamilyId == family.Id && m.UserId == bob.UserId);
        memberAfter!.Status.ShouldBe(FamilyMemberStatusEnum.Active);
    }

    [Test]
    public async Task UpdateMemberRole_BlocksDemotingLastParent()
    {
        var owner = await TestApp.SendAsync(new RegisterCommand("solo-parent@local", "Password123!", null, null));

        var family = await TestApp.SendAsync(new CreateFamilyCommand(
            "Solo Parent", owner.UserId, null, null, null));

        await Should.ThrowAsync<ConflictException>(
            () => TestApp.SendAsync(new UpdateMemberRoleCommand(family.Id, owner.UserId, "Child", owner.UserId)));
    }

    [Test]
    public async Task RemoveMember_BlocksRemovingLastParent()
    {
        var owner = await TestApp.SendAsync(new RegisterCommand("solo-parent2@local", "Password123!", null, null));

        var family = await TestApp.SendAsync(new CreateFamilyCommand(
            "Solo Parent 2", owner.UserId, null, null, null));

        await Should.ThrowAsync<ConflictException>(
            () => TestApp.SendAsync(new RemoveMemberCommand(family.Id, owner.UserId, owner.UserId)));
    }

    [Test]
    public async Task RemoveMember_DisablesNonLastParent()
    {
        var alice = await TestApp.SendAsync(new RegisterCommand("co-parent-a@local", "Password123!", null, null));
        var bob = await TestApp.SendAsync(new RegisterCommand("co-parent-b@local", "Password123!", null, null));

        var family = await TestApp.SendAsync(new CreateFamilyCommand(
            "Two Parents", alice.UserId, null, null, null));

        await TestApp.SendAsync(new AddFamilyMemberCommand(family.Id, bob.UserId, "Parent", alice.UserId));
        await TestApp.SendAsync(new AcceptInvitationCommand(family.Id, bob.UserId));

        await TestApp.SendAsync(new RemoveMemberCommand(family.Id, bob.UserId, alice.UserId));

        var bobMember = await TestApp.FindFirstAsync<FamilyMember>(
            m => m.FamilyId == family.Id && m.UserId == bob.UserId);
        bobMember!.Status.ShouldBe(FamilyMemberStatusEnum.Disabled);
    }

    [Test]
    public async Task NonParentCannotAddMember()
    {
        var alice = await TestApp.SendAsync(new RegisterCommand("rbac-a@local", "Password123!", null, null));
        var bob = await TestApp.SendAsync(new RegisterCommand("rbac-b@local", "Password123!", null, null));
        var carol = await TestApp.SendAsync(new RegisterCommand("rbac-c@local", "Password123!", null, null));

        var family = await TestApp.SendAsync(new CreateFamilyCommand(
            "RBAC Test", alice.UserId, null, null, null));

        // Bob is not a member at all; he tries to add Carol.
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => TestApp.SendAsync(new AddFamilyMemberCommand(family.Id, carol.UserId, "Parent", bob.UserId)));
    }
}

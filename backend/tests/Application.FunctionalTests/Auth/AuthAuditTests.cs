using Backend.Application.Auth.Commands.Login;
using Backend.Application.Auth.Commands.Logout;
using Backend.Application.Auth.Commands.RefreshToken;
using Backend.Application.Auth.Commands.Register;
using Backend.Domain.Entities;

namespace Backend.Application.FunctionalTests.Auth;

public class AuthAuditTests : TestBase
{
    [Test]
    public async Task Register_WritesUserRegisteredAuditEntry()
    {
        var auth = await TestApp.SendAsync(
            new RegisterCommand("audit-reg@local", "Password123!", null, null));

        var entry = await TestApp.FindFirstAsync<AuditLog>(
            a => a.UserId == auth.UserId && a.Action == "user.registered");

        entry.ShouldNotBeNull();
        entry!.EntityType.ShouldBe("user");
        entry.Metadata.ShouldNotBeNull();
        entry.Metadata!.ShouldContain("audit-reg@local");
    }

    [Test]
    public async Task Login_WritesLoggedInAuditEntry()
    {
        await TestApp.SendAsync(new RegisterCommand("audit-login@local", "Password123!", null, null));
        var login = await TestApp.SendAsync(new LoginCommand("audit-login@local", "Password123!", "device-z"));

        var entry = await TestApp.FindFirstAsync<AuditLog>(
            a => a.UserId == login.UserId && a.Action == "user.logged_in");

        entry.ShouldNotBeNull();
        entry!.EntityType.ShouldBe("auth_session");
        entry.EntityId.ShouldNotBeNull();
    }

    [Test]
    public async Task Login_WrongPassword_WritesLoginFailedAudit()
    {
        var reg = await TestApp.SendAsync(
            new RegisterCommand("audit-fail@local", "Password123!", null, null));

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => TestApp.SendAsync(new LoginCommand("audit-fail@local", "WrongPassword!", null)));

        var entry = await TestApp.FindFirstAsync<AuditLog>(
            a => a.UserId == reg.UserId && a.Action == "user.login_failed");

        entry.ShouldNotBeNull();
    }

    [Test]
    public async Task Refresh_WritesTokenRefreshedAudit()
    {
        var reg = await TestApp.SendAsync(
            new RegisterCommand("audit-refresh@local", "Password123!", null, null));

        await TestApp.SendAsync(new RefreshTokenCommand(reg.RefreshToken));

        var entry = await TestApp.FindFirstAsync<AuditLog>(
            a => a.UserId == reg.UserId && a.Action == "auth.token_refreshed");

        entry.ShouldNotBeNull();
    }

    [Test]
    public async Task Logout_WritesLoggedOutAudit()
    {
        var reg = await TestApp.SendAsync(
            new RegisterCommand("audit-logout@local", "Password123!", null, null));

        await TestApp.SendAsync(new LogoutCommand(reg.RefreshToken));

        var entry = await TestApp.FindFirstAsync<AuditLog>(
            a => a.UserId == reg.UserId && a.Action == "user.logged_out");

        entry.ShouldNotBeNull();
    }
}

using System.IdentityModel.Tokens.Jwt;
using Backend.Application.Auth.Commands.Login;
using Backend.Application.Auth.Commands.Logout;
using Backend.Application.Auth.Commands.RefreshToken;
using Backend.Application.Auth.Commands.Register;
using Backend.Domain.Entities;

namespace Backend.Application.FunctionalTests.Auth;

public class AuthFlowTests : TestBase
{
    [Test]
    public async Task Register_ReturnsUserIdAccessAndRefreshTokens()
    {
        var result = await TestApp.SendAsync(new RegisterCommand(
            "register@local", "Password123!", "Reg User", DeviceId: "test-device"));

        result.UserId.ShouldNotBeNullOrEmpty();
        result.AccessToken.ShouldNotBeNullOrEmpty();
        result.RefreshToken.ShouldNotBeNullOrEmpty();

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        token.Claims.First(c => c.Type == "sub").Value.ShouldBe(result.UserId);
        token.Claims.First(c => c.Type == "email").Value.ShouldBe("register@local");

        (await TestApp.CountAsync<AuthSession>()).ShouldBe(1);
    }

    [Test]
    public async Task Register_DuplicateEmailFails()
    {
        await TestApp.SendAsync(new RegisterCommand("dup@local", "Password123!", null, null));

        await Should.ThrowAsync<Application.Common.Exceptions.ConflictException>(
            () => TestApp.SendAsync(new RegisterCommand("dup@local", "Password123!", null, null)));
    }

    [Test]
    public async Task Login_WithCorrectCredentials_IssuesNewSession()
    {
        await TestApp.SendAsync(new RegisterCommand("login@local", "Password123!", null, null));

        var loginResult = await TestApp.SendAsync(new LoginCommand("login@local", "Password123!", "device-2"));

        loginResult.AccessToken.ShouldNotBeNullOrEmpty();
        loginResult.RefreshToken.ShouldNotBeNullOrEmpty();
        (await TestApp.CountAsync<AuthSession>()).ShouldBe(2);
    }

    [Test]
    public async Task Login_WithWrongPassword_Throws()
    {
        await TestApp.SendAsync(new RegisterCommand("badpw@local", "Password123!", null, null));

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => TestApp.SendAsync(new LoginCommand("badpw@local", "WrongPassword!", null)));
    }

    [Test]
    public async Task Refresh_RotatesTokensAndRevokesPrevious()
    {
        var initial = await TestApp.SendAsync(
            new RegisterCommand("refresh@local", "Password123!", null, "dev"));

        var refreshed = await TestApp.SendAsync(new RefreshTokenCommand(initial.RefreshToken));

        refreshed.RefreshToken.ShouldNotBe(initial.RefreshToken);
        refreshed.AccessToken.ShouldNotBe(initial.AccessToken);

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => TestApp.SendAsync(new RefreshTokenCommand(initial.RefreshToken)));

        var twiceRefreshed = await TestApp.SendAsync(new RefreshTokenCommand(refreshed.RefreshToken));
        twiceRefreshed.RefreshToken.ShouldNotBe(refreshed.RefreshToken);
    }

    [Test]
    public async Task Logout_RevokesSessionAndDisablesRefresh()
    {
        var auth = await TestApp.SendAsync(
            new RegisterCommand("logout@local", "Password123!", null, null));

        await TestApp.SendAsync(new LogoutCommand(auth.RefreshToken));

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => TestApp.SendAsync(new RefreshTokenCommand(auth.RefreshToken)));
    }
}

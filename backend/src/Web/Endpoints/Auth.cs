using Backend.Application.Auth.Commands.Login;
using Backend.Application.Auth.Commands.Logout;
using Backend.Application.Auth.Commands.RefreshToken;
using Backend.Application.Auth.Commands.Register;
using Backend.Web.Infrastructure;
using MediatR;

namespace Backend.Web.Endpoints;

public class Auth : IEndpointGroup
{
    public static string? RoutePrefix => "/auth";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireRateLimiting("auth-policy");

        groupBuilder.MapPost(Register, "register");
        groupBuilder.MapPost(Login, "login");
        groupBuilder.MapPost(Refresh, "refresh");
        groupBuilder.MapPost(Logout, "logout");
    }

    [EndpointSummary("Register")]
    [EndpointDescription("Creates a new user account and returns JWT + refresh token.")]
    public static async Task<IResult> Register(RegisterRequest req, ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(
            new RegisterCommand(req.Email, req.Password, req.DisplayName, req.DeviceId), ct);
        return Results.Created(string.Empty, result);
    }

    [EndpointSummary("Login")]
    [EndpointDescription("Authenticates user credentials and returns JWT + refresh token.")]
    public static async Task<IResult> Login(LoginRequest req, ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(
            new LoginCommand(req.Email, req.Password, req.DeviceId), ct);
        return Results.Ok(result);
    }

    [EndpointSummary("Refresh Token")]
    [EndpointDescription("Rotates the refresh token and issues a new access token.")]
    public static async Task<IResult> Refresh(RefreshRequest req, ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new RefreshTokenCommand(req.RefreshToken), ct);
        return Results.Ok(result);
    }

    [EndpointSummary("Logout")]
    [EndpointDescription("Revokes the session associated with the provided refresh token.")]
    public static async Task<IResult> Logout(LogoutRequest req, ISender mediator, CancellationToken ct)
    {
        await mediator.Send(new LogoutCommand(req.RefreshToken), ct);
        return Results.Ok();
    }
}

public record RegisterRequest(string Email, string Password, string? DisplayName, string? DeviceId);
public record LoginRequest(string Email, string Password, string? DeviceId);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);

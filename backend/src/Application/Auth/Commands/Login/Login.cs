using Backend.Application.Auth.Models;
using Backend.Application.Common.Interfaces;

namespace Backend.Application.Auth.Commands.Login;

public record LoginCommand(
    string Email,
    string Password,
    string? DeviceId) : IRequest<AuthResult>;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResult>
{
    private readonly IAuthService _authService;

    public LoginCommandHandler(IAuthService authService) => _authService = authService;

    public Task<AuthResult> Handle(LoginCommand request, CancellationToken cancellationToken) =>
        _authService.LoginAsync(request.Email, request.Password, request.DeviceId, cancellationToken);
}

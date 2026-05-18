using Backend.Application.Auth.Models;
using Backend.Application.Common.Interfaces;

namespace Backend.Application.Auth.Commands.Register;

public record RegisterCommand(
    string Email,
    string Password,
    string? DisplayName,
    string? DeviceId) : IRequest<AuthResult>;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResult>
{
    private readonly IAuthService _authService;

    public RegisterCommandHandler(IAuthService authService) => _authService = authService;

    public Task<AuthResult> Handle(RegisterCommand request, CancellationToken cancellationToken) =>
        _authService.RegisterAsync(request.Email, request.Password, request.DisplayName, request.DeviceId, cancellationToken);
}

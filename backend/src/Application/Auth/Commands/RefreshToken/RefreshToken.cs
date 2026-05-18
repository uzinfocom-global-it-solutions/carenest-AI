using Backend.Application.Auth.Models;
using Backend.Application.Common.Interfaces;

namespace Backend.Application.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResult>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResult>
{
    private readonly IAuthService _authService;

    public RefreshTokenCommandHandler(IAuthService authService) => _authService = authService;

    public Task<AuthResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken) =>
        _authService.RefreshAsync(request.RefreshToken, cancellationToken);
}

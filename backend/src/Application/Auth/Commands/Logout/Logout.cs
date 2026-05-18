using Backend.Application.Common.Interfaces;

namespace Backend.Application.Auth.Commands.Logout;

public record LogoutCommand(string RefreshToken) : IRequest;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IAuthService _authService;

    public LogoutCommandHandler(IAuthService authService) => _authService = authService;

    public Task Handle(LogoutCommand request, CancellationToken cancellationToken) =>
        _authService.RevokeAsync(request.RefreshToken, cancellationToken);
}

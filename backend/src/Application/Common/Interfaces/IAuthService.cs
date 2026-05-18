using Backend.Application.Auth.Models;

namespace Backend.Application.Common.Interfaces;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string password, string? displayName, string? deviceId, CancellationToken ct = default);
    Task<AuthResult> LoginAsync(string email, string password, string? deviceId, CancellationToken ct = default);
    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeAsync(string refreshToken, CancellationToken ct = default);
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Backend.Application.Auth.Models;
using Backend.Application.Common.Exceptions;
using Backend.Application.Common.Interfaces;
using Backend.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Infrastructure.Identity;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _context;
    private readonly JwtSettings _jwtSettings;
    private readonly IAuditService _audit;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext context,
        JwtSettings jwtSettings,
        IAuditService audit)
    {
        _userManager = userManager;
        _context = context;
        _jwtSettings = jwtSettings;
        _audit = audit;
    }

    public async Task<AuthResult> RegisterAsync(string email, string password, string? displayName, string? deviceId, CancellationToken ct = default)
    {
        if (await _userManager.FindByEmailAsync(email) != null)
            throw new ConflictException($"Email '{email}' is already registered.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => new FluentValidation.Results.ValidationFailure(e.Code, e.Description));
            throw new Application.Common.Exceptions.ValidationException(errors);
        }

        // Auto-create default settings for the new user.
        _context.UserSettings.Add(new UserSetting { UserId = user.Id });
        await _context.SaveChangesAsync(ct);

        var (authResult, session) = await CreateSessionAsync(user, deviceId, ct);
        await _audit.LogAsync(user.Id, "user.registered", "user", null,
            new { email, deviceId, sessionId = session.Id }, ct);
        return authResult;
    }

    public async Task<AuthResult> LoginAsync(string email, string password, string? deviceId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, password))
        {
            // Audit failed attempt against the user when known so brute-force is visible per-account.
            if (user != null)
            {
                await _audit.LogAsync(user.Id, "user.login_failed", "user", null,
                    new { email, deviceId }, ct);
            }
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var (authResult, session) = await CreateSessionAsync(user, deviceId, ct);
        await _audit.LogAsync(user.Id, "user.logged_in", "auth_session", session.Id,
            new { deviceId }, ct);
        return authResult;
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenHash = HashToken(refreshToken);
        var session = await _context.AuthSessions
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == tokenHash, ct);

        if (session == null || !session.IsActive)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        var revokedSessionId = session.Id;
        session.RevokedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(ct);

        var user = await _userManager.FindByIdAsync(session.UserId)
            ?? throw new UnauthorizedAccessException("User not found.");

        var (authResult, newSession) = await CreateSessionAsync(user, session.DeviceId, ct);
        await _audit.LogAsync(user.Id, "auth.token_refreshed", "auth_session", newSession.Id,
            new { revokedSessionId, deviceId = session.DeviceId }, ct);
        return authResult;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenHash = HashToken(refreshToken);
        var session = await _context.AuthSessions
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == tokenHash, ct);

        if (session is { IsActive: true })
        {
            session.RevokedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);
            await _audit.LogAsync(session.UserId, "user.logged_out", "auth_session", session.Id,
                new { deviceId = session.DeviceId }, ct);
        }
    }

    private async Task<(AuthResult Result, AuthSession Session)> CreateSessionAsync(
        ApplicationUser user, string? deviceId, CancellationToken ct)
    {
        var accessToken = GenerateAccessToken(user);
        var (refreshToken, refreshTokenHash) = GenerateRefreshToken();

        var session = new AuthSession
        {
            UserId = user.Id,
            RefreshTokenHash = refreshTokenHash,
            DeviceId = deviceId,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
        };

        _context.AuthSessions.Add(session);
        await _context.SaveChangesAsync(ct);

        return (new AuthResult(user.Id, accessToken, refreshToken), session);
    }

    private string GenerateAccessToken(ApplicationUser user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static (string token, string hash) GenerateRefreshToken()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
        return (token, HashToken(token));
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

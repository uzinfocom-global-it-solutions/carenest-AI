namespace Backend.Application.Auth.Models;

public record AuthResult(string UserId, string AccessToken, string RefreshToken);

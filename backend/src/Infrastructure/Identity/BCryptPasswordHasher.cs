using Microsoft.AspNetCore.Identity;

namespace Backend.Infrastructure.Identity;

internal sealed class BCryptPasswordHasher : IPasswordHasher<ApplicationUser>
{
    // BCrypt work factor. 12 ≈ ~250ms on modern hardware — bumps with Moore's law over time.
    private const int WorkFactor = 12;

    public string HashPassword(ApplicationUser user, string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, WorkFactor);
    }

    public PasswordVerificationResult VerifyHashedPassword(
        ApplicationUser user,
        string hashedPassword,
        string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(providedPassword))
            return PasswordVerificationResult.Failed;

        try
        {
            var verified = BCrypt.Net.BCrypt.EnhancedVerify(providedPassword, hashedPassword);
            return verified
                ? PasswordVerificationResult.Success
                : PasswordVerificationResult.Failed;
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Stored hash is not a BCrypt hash (e.g., legacy PBKDF2). Caller can re-hash on next login.
            return PasswordVerificationResult.Failed;
        }
    }
}

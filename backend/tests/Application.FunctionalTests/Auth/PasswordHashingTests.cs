using Backend.Application.Auth.Commands.Register;
using Backend.Infrastructure.Identity;

namespace Backend.Application.FunctionalTests.Auth;

public class PasswordHashingTests : TestBase
{
    [Test]
    public async Task RegisteredPassword_IsBCryptHashed()
    {
        var auth = await TestApp.SendAsync(
            new RegisterCommand("bcrypt@local", "Password123!", null, null));

        var user = await TestApp.FindAsync<ApplicationUser>(auth.UserId);

        user.ShouldNotBeNull();
        user!.PasswordHash.ShouldNotBeNullOrEmpty();

        // Enhanced BCrypt prefix is "$2a$" with embedded SHA-384 pre-hash. Verify shape.
        user.PasswordHash!.ShouldStartWith("$2");
        user.PasswordHash.Length.ShouldBeGreaterThan(50);
    }
}

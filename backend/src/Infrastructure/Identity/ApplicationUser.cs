using Microsoft.AspNetCore.Identity;

namespace Backend.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
}

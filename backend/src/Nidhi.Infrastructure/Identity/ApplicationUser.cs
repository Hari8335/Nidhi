using Microsoft.AspNetCore.Identity;

namespace Nidhi.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser() => Id = Guid.CreateVersion7();
}

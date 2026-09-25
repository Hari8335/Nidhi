using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Persistence;

namespace Nidhi.Infrastructure.Identity;

public sealed class AdminProvisioner(NidhiDbContext db, UserManager<ApplicationUser> users)
{
    public async Task<bool> ProvisionAsync(string email, string password)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Serialize operational retries across processes without adding infrastructure.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(510005)");
        await IdentityRoles.EnsureAsync(db);
        var user = await users.FindByEmailAsync(email);
        if (user is not null)
        {
            var roles = await users.GetRolesAsync(user);
            if (roles.Count != 1 || roles[0] != "ADMIN")
                throw new InvalidOperationException("Provisioning refuses to promote an existing non-admin account.");
            await transaction.CommitAsync();
            return false;
        }
        user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        AuthService.Check(await users.CreateAsync(user, password));
        AuthService.Check(await users.AddToRoleAsync(user, "ADMIN"));
        db.AuditEvents.Add(new AuditEvent(user.Id, "ADMIN", "ADMIN_PROVISIONED", "ApplicationUser",
            user.Id.ToString(), "{\"source\":\"operational-command\"}", DateTime.UtcNow));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }
}

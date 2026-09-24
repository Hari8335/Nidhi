using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nidhi.Infrastructure.Identity;

namespace Nidhi.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.ToTable("asp_net_users");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.UserName).HasColumnName("user_name");
        b.Property(x => x.NormalizedUserName).HasColumnName("normalized_user_name");
        b.Property(x => x.Email).HasColumnName("email");
        b.Property(x => x.NormalizedEmail).HasColumnName("normalized_email");
        b.Property(x => x.EmailConfirmed).HasColumnName("email_confirmed");
        b.Property(x => x.PasswordHash).HasColumnName("password_hash");
        b.Property(x => x.SecurityStamp).HasColumnName("security_stamp");
        b.Property(x => x.ConcurrencyStamp).HasColumnName("concurrency_stamp");
        b.Property(x => x.PhoneNumber).HasColumnName("phone_number");
        b.Property(x => x.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
        b.Property(x => x.TwoFactorEnabled).HasColumnName("two_factor_enabled");
        b.Property(x => x.LockoutEnd).HasColumnName("lockout_end");
        b.Property(x => x.LockoutEnabled).HasColumnName("lockout_enabled");
        b.Property(x => x.AccessFailedCount).HasColumnName("access_failed_count");
        b.HasIndex(x => x.NormalizedEmail).IsUnique().HasDatabaseName("EmailIndex");
    }
}

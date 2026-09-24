using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nidhi.Infrastructure.Identity;

namespace Nidhi.Infrastructure.Persistence.Configurations;

public sealed class IdentityUserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> b)
    {
        b.ToTable("asp_net_user_logins");
        b.Property(x => x.LoginProvider).HasColumnName("login_provider");
        b.Property(x => x.ProviderKey).HasColumnName("provider_key");
        b.Property(x => x.ProviderDisplayName).HasColumnName("provider_display_name");
        b.Property(x => x.UserId).HasColumnName("user_id");
    }
}

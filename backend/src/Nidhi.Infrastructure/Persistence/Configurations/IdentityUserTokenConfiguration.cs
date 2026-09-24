using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nidhi.Infrastructure.Identity;

namespace Nidhi.Infrastructure.Persistence.Configurations;

public sealed class IdentityUserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> b)
    {
        b.ToTable("asp_net_user_tokens");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.LoginProvider).HasColumnName("login_provider");
        b.Property(x => x.Name).HasColumnName("name");
        b.Property(x => x.Value).HasColumnName("value");
    }
}

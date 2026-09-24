using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nidhi.Infrastructure.Identity;

namespace Nidhi.Infrastructure.Persistence.Configurations;

public sealed class IdentityRoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> b)
    {
        b.ToTable("asp_net_role_claims");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.RoleId).HasColumnName("role_id");
        b.Property(x => x.ClaimType).HasColumnName("claim_type");
        b.Property(x => x.ClaimValue).HasColumnName("claim_value");
    }
}

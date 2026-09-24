using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nidhi.Infrastructure.Identity;

namespace Nidhi.Infrastructure.Persistence.Configurations;

public sealed class IdentityRoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> b)
    {
        b.ToTable("asp_net_roles");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Name).HasColumnName("name");
        b.Property(x => x.NormalizedName).HasColumnName("normalized_name");
        b.Property(x => x.ConcurrencyStamp).HasColumnName("concurrency_stamp");
    }
}

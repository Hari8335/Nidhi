using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Identity;

namespace Nidhi.Infrastructure.Persistence.Configurations;

public sealed class CustomerProfileConfiguration : IEntityTypeConfiguration<CustomerProfile>
{
    public void Configure(EntityTypeBuilder<CustomerProfile> b)
    {
        b.ToTable("customer_profiles");
        b.HasKey(x => x.CustomerId).HasName("PK_customer_profiles");
        b.Property(x => x.CustomerId).HasColumnName("id").ValueGeneratedNever().IsRequired();
        b.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(100);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<ApplicationUser>().WithOne().HasForeignKey<CustomerProfile>(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Identity;

namespace Nidhi.Infrastructure.Persistence.Configurations;

public sealed class GoldHoldingConfiguration : IEntityTypeConfiguration<GoldHolding>
{
    public void Configure(EntityTypeBuilder<GoldHolding> b)
    {
        b.ToTable("gold_holdings", t =>
        {
            t.HasCheckConstraint("chk_gold_holdings_quantity_non_negative", "quantity_grams >= 0");
        });
        b.HasKey(x => x.HoldingId).HasName("PK_gold_holdings");
        b.Property(x => x.HoldingId).HasColumnName("id").ValueGeneratedNever().IsRequired();
        b.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        b.Property(x => x.QuantityGrams).HasColumnName("quantity_grams").HasPrecision(20, 8).HasDefaultValue(0m).IsRequired();
        b.Property(x => x.ConcurrencyToken).HasColumnName("xmin").IsRowVersion().IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<CustomerProfile>().WithOne().HasForeignKey<GoldHolding>(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.CustomerId).IsUnique().HasDatabaseName("UK_gold_holdings_customer_id");
    }
}

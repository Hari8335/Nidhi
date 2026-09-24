using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Identity;

namespace Nidhi.Infrastructure.Persistence.Configurations;

public sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> b)
    {
        b.ToTable("wallets", t =>
        {
            t.HasCheckConstraint("chk_wallets_balance_non_negative", "balance_lkr >= 0");
            t.HasCheckConstraint("chk_wallets_balance_max_cap", "balance_lkr <= 5000000");
        });
        b.HasKey(x => x.WalletId).HasName("PK_wallets");
        b.Property(x => x.WalletId).HasColumnName("id").ValueGeneratedNever().IsRequired();
        b.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        b.Property(x => x.BalanceLkr).HasColumnName("balance_lkr").HasPrecision(18, 2).HasDefaultValue(0m).IsRequired();
        b.Property(x => x.ConcurrencyToken).HasColumnName("xmin").IsRowVersion().IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<CustomerProfile>().WithOne().HasForeignKey<Wallet>(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.CustomerId).IsUnique().HasDatabaseName("UK_wallets_customer_id");
    }
}

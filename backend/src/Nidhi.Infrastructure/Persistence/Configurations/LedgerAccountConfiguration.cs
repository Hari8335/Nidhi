using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Identity;

namespace Nidhi.Infrastructure.Persistence.Configurations;

public sealed class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> b)
    {
        b.ToTable("ledger_accounts", t =>
        {
            t.HasCheckConstraint("chk_ledger_accounts_unit_valid", "unit IN ('LKR', 'GOLD_GRAMS')");
            t.HasCheckConstraint("chk_ledger_accounts_class_valid", "classification IN ('ASSET', 'LIABILITY', 'EQUITY', 'CLEARING')");
        });
        b.HasKey(x => x.AccountId).HasName("PK_ledger_accounts");
        b.Property(x => x.AccountId).HasColumnName("id").ValueGeneratedNever().IsRequired();
        b.Property(x => x.AccountNumber).HasColumnName("account_number").HasMaxLength(64).IsRequired();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        b.Property(x => x.Unit).HasColumnName("unit").HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.Classification).HasColumnName("classification").HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(x => x.CustomerId).HasColumnName("customer_id");
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        b.HasOne<CustomerProfile>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.AccountNumber).IsUnique().HasDatabaseName("UK_ledger_accounts_account_number");
        b.HasAlternateKey(x => new { x.AccountId, x.Unit }).HasName("AK_ledger_accounts_id_unit");
        b.HasIndex(x => x.CustomerId).HasDatabaseName("IX_ledger_accounts_customer_id");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Identity;

namespace Nidhi.Infrastructure.Persistence.Configurations;

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> b)
    {
        b.ToTable("ledger_entries", t =>
        {
            t.HasCheckConstraint("chk_ledger_entries_amount_positive", "amount > 0");
            t.HasCheckConstraint("chk_ledger_entries_unit_valid", "unit IN ('LKR', 'GOLD_GRAMS')");
            t.HasCheckConstraint("chk_ledger_entries_direction_valid", "direction IN ('DEBIT', 'CREDIT')");
        });
        b.HasKey(x => x.EntryId).HasName("PK_ledger_entries");
        b.Property(x => x.EntryId).HasColumnName("id").ValueGeneratedNever().IsRequired();
        b.Property(x => x.TransactionId).HasColumnName("transaction_id").IsRequired();
        b.Property(x => x.AccountId).HasColumnName("account_id").IsRequired();
        b.Property(x => x.Unit).HasColumnName("unit").HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.Direction).HasColumnName("direction").HasConversion<string>().HasMaxLength(8).IsRequired();
        b.Property(x => x.Amount).HasColumnName("amount").HasPrecision(20, 8).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        b.HasOne<FinancialTransaction>().WithMany().HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LedgerAccount>().WithMany().HasForeignKey(x => new { x.AccountId, x.Unit }).HasPrincipalKey(x => new { x.AccountId, x.Unit }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TransactionId, x.Unit }).HasDatabaseName("IX_ledger_entries_tx_unit");
        b.HasIndex(x => new { x.AccountId, x.CreatedAtUtc }).IsDescending(false, true).HasDatabaseName("IX_ledger_entries_account_created");
    }
}

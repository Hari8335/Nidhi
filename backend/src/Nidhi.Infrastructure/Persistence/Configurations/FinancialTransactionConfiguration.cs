using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Identity;

namespace Nidhi.Infrastructure.Persistence.Configurations;

public sealed class FinancialTransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    public void Configure(EntityTypeBuilder<FinancialTransaction> b)
    {
        b.ToTable("financial_transactions", t =>
        {
            t.HasCheckConstraint("chk_transactions_type_valid", "type IN ('WALLET_FUNDING', 'GOLD_PURCHASE')");
            t.HasCheckConstraint("chk_transactions_status_valid", "status = 'COMPLETED'");
            t.HasCheckConstraint("chk_transactions_amount_range", "amount_lkr >= 100 AND amount_lkr <= 1000000");
            t.HasCheckConstraint("chk_transactions_gold_purchase_fields", "(type = 'WALLET_FUNDING' AND gold_quantity_grams IS NULL AND price_version_id IS NULL AND applied_price_per_gram_lkr IS NULL AND post_gold_holding_grams IS NULL) OR (type = 'GOLD_PURCHASE' AND gold_quantity_grams IS NOT NULL AND gold_quantity_grams > 0 AND price_version_id IS NOT NULL AND applied_price_per_gram_lkr IS NOT NULL AND applied_price_per_gram_lkr > 0 AND post_gold_holding_grams IS NOT NULL AND post_gold_holding_grams >= gold_quantity_grams)");
        });
        b.HasKey(x => x.TransactionId).HasName("PK_financial_transactions");
        b.Property(x => x.TransactionId).HasColumnName("id").ValueGeneratedNever().IsRequired();
        b.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        b.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(x => x.AmountLkr).HasColumnName("amount_lkr").HasPrecision(18, 2).IsRequired();
        b.Property(x => x.GoldQuantityGrams).HasColumnName("gold_quantity_grams").HasPrecision(20, 8);
        b.Property(x => x.PriceVersionId).HasColumnName("price_version_id");
        b.Property(x => x.AppliedPricePerGramLkr).HasColumnName("applied_price_per_gram_lkr").HasPrecision(18, 4);
        b.Property(x => x.PostOperationWalletBalanceLkr).HasColumnName("post_wallet_balance_lkr").HasPrecision(18, 2).IsRequired();
        b.Property(x => x.PostOperationGoldHoldingGrams).HasColumnName("post_gold_holding_grams").HasPrecision(20, 8);
        b.Property(x => x.IdempotencyRecordId).HasColumnName("idempotency_record_id").IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        b.HasOne<CustomerProfile>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<GoldPrice>().WithMany().HasForeignKey(x => x.PriceVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<IdempotencyRecord>().WithOne().HasForeignKey<FinancialTransaction>(x => x.IdempotencyRecordId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.IdempotencyRecordId).IsUnique().HasDatabaseName("UK_financial_transactions_idempotency");
        b.HasIndex(x => new { x.CustomerId, x.CreatedAtUtc }).IsDescending(false, true).HasDatabaseName("IX_financial_transactions_customer_created");
        b.HasIndex(x => x.CreatedAtUtc).IsDescending().HasDatabaseName("IX_financial_transactions_created_at");
    }
}

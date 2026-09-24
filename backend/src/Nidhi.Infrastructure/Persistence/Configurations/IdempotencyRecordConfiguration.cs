using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Identity;

namespace Nidhi.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> b)
    {
        b.ToTable("idempotency_records", t =>
        {
            t.HasCheckConstraint("chk_idempotency_status_valid", "status IN ('IN_PROGRESS', 'COMPLETED', 'FAILED')");
        });
        b.HasKey(x => x.IdempotencyRecordId).HasName("PK_idempotency_records");
        b.Property(x => x.IdempotencyRecordId).HasColumnName("id").ValueGeneratedNever().IsRequired();
        b.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        b.Property(x => x.Operation).HasColumnName("operation").HasConversion<string>().HasMaxLength(64).IsRequired();
        b.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128).IsRequired();
        b.Property(x => x.RequestHash).HasColumnName("request_hash").HasMaxLength(64).IsRequired();
        b.Property(x => x.ResponseTransactionId).HasColumnName("response_transaction_id");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        b.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<CustomerProfile>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CustomerId, x.Operation, x.IdempotencyKey }).IsUnique().HasDatabaseName("UK_idempotency_scoped_key");
        b.HasOne<FinancialTransaction>().WithMany().HasForeignKey(x => x.ResponseTransactionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.ResponseTransactionId).HasDatabaseName("IX_idempotency_response_tx");
    }
}

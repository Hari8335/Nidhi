using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Identity;

namespace Nidhi.Infrastructure.Persistence.Configurations;

public sealed class GoldPriceConfiguration : IEntityTypeConfiguration<GoldPrice>
{
    public void Configure(EntityTypeBuilder<GoldPrice> b)
    {
        b.ToTable("gold_prices", t =>
        {
            t.HasCheckConstraint("chk_gold_prices_price_positive", "price_per_gram_lkr > 0");
        });
        b.HasKey(x => x.PriceVersionId).HasName("PK_gold_prices");
        b.Property(x => x.PriceVersionId).HasColumnName("id").ValueGeneratedNever().IsRequired();
        b.Property(x => x.PricePerGramLkr).HasColumnName("price_per_gram_lkr").HasPrecision(18, 4).IsRequired();
        b.Property(x => x.PublishedAtUtc).HasColumnName("published_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        b.Property(x => x.PublishedByAdminId).HasColumnName("published_by_admin_id").IsRequired();
        b.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        b.Property(x => x.AuditLogId).HasColumnName("audit_log_id").IsRequired();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PublishedByAdminId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.PublishedAtUtc).IsDescending().HasDatabaseName("IX_gold_prices_published_at_utc");
    }
}

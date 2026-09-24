using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Identity;

namespace Nidhi.Infrastructure.Persistence.Configurations;

public sealed class SavingsGoalConfiguration : IEntityTypeConfiguration<SavingsGoal>
{
    public void Configure(EntityTypeBuilder<SavingsGoal> b)
    {
        b.ToTable("savings_goals", t =>
        {
            t.HasCheckConstraint("chk_savings_goals_target_positive", "target_grams > 0");
            t.HasCheckConstraint("chk_savings_goals_status_valid", "status IN ('ACTIVE', 'COMPLETED', 'REPLACED')");
        });
        b.HasKey(x => x.GoalId).HasName("PK_savings_goals");
        b.Property(x => x.GoalId).HasColumnName("id").ValueGeneratedNever().IsRequired();
        b.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
        b.Property(x => x.TargetGrams).HasColumnName("target_grams").HasPrecision(20, 8).IsRequired();
        b.Property(x => x.TargetDateUtc).HasColumnName("target_date_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<CustomerProfile>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.CustomerId).HasDatabaseName("IX_savings_goals_customer_id");
        b.HasIndex(x => x.CustomerId, "ActiveGoal").IsUnique().HasFilter("status = 'ACTIVE'").HasDatabaseName("uq_savings_goals_one_active_per_customer");
    }
}

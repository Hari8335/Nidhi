using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nidhi.Domain.Entities;
using Nidhi.Infrastructure.Identity;

namespace Nidhi.Infrastructure.Persistence.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        b.ToTable("audit_events");
        b.HasKey(x => x.EventId).HasName("PK_audit_events");
        b.Property(x => x.EventId).HasColumnName("id").ValueGeneratedNever().IsRequired();
        b.Property(x => x.ActorId).HasColumnName("actor_id").IsRequired();
        b.Property(x => x.ActorRole).HasColumnName("actor_role").HasMaxLength(32).IsRequired();
        b.Property(x => x.Action).HasColumnName("action").HasMaxLength(64).IsRequired();
        b.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(64).IsRequired();
        b.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(64).IsRequired();
        b.Property(x => x.DetailsJson).HasColumnName("details").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.TimestampUtc).HasColumnName("timestamp_utc").HasColumnType("timestamp with time zone").IsRequired();
        b.HasIndex(x => x.TimestampUtc).IsDescending().HasDatabaseName("IX_audit_events_timestamp");
        b.HasIndex(x => new { x.ActorId, x.TimestampUtc }).IsDescending(false, true).HasDatabaseName("IX_audit_events_actor");
        b.HasIndex(x => new { x.Action, x.TimestampUtc }).IsDescending(false, true).HasDatabaseName("IX_audit_events_action");
    }
}

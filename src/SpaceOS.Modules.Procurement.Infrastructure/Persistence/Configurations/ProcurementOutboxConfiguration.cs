using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpaceOS.Modules.Procurement.Domain.Aggregates;

namespace SpaceOS.Modules.Procurement.Infrastructure.Persistence.Configurations;

public sealed class ProcurementOutboxConfiguration : IEntityTypeConfiguration<ProcurementOutboxMessage>
{
    public void Configure(EntityTypeBuilder<ProcurementOutboxMessage> builder)
    {
        builder.ToTable("procurement_outbox");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.MessageType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SchemaVersion).IsRequired();
        builder.Property(x => x.IdempotencyKey).IsRequired();
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.AttemptCount).IsRequired();
        builder.Property(x => x.NextAttemptAt).IsRequired();
        builder.Property(x => x.LeaseUntil);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ProcessedAt);

        builder.HasIndex(x => new { x.TenantId, x.MessageType, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
    }
}

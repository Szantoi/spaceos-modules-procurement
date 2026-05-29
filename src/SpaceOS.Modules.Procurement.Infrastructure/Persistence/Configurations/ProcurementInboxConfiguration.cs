using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpaceOS.Modules.Procurement.Domain.Aggregates;

namespace SpaceOS.Modules.Procurement.Infrastructure.Persistence.Configurations;

public sealed class ProcurementInboxConfiguration : IEntityTypeConfiguration<ProcurementInboxMessage>
{
    public void Configure(EntityTypeBuilder<ProcurementInboxMessage> builder)
    {
        builder.ToTable("procurement_inbox");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.MessageType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ResultRef);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ProcessedAt);

        // ON CONFLICT DO NOTHING idempotency guard
        builder.HasIndex(x => new { x.TenantId, x.MessageType, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.ProcessedAt); // DB-P-10: retention sweep
    }
}

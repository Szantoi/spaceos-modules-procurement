using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Infrastructure.Persistence.Configurations;

public class ReceiptQueueConfiguration : IEntityTypeConfiguration<ReceiptQueue>
{
    public void Configure(EntityTypeBuilder<ReceiptQueue> builder)
    {
        builder.ToTable("ReceiptQueues");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.AsnShipmentId).IsRequired();
        builder.Property(x => x.ScannedBy).IsRequired();
        builder.Property(x => x.ActualQuantity).IsRequired();
        builder.Property(x => x.ScannedAt).IsRequired();
        builder.Property(x => x.SyncedAt).IsRequired(false);
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.AsnShipmentId, x.TenantId });
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}

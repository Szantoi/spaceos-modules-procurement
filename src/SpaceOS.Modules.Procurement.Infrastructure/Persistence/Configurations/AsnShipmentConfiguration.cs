using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Infrastructure.Persistence.Configurations;

public class AsnShipmentConfiguration : IEntityTypeConfiguration<AsnShipment>
{
    public void Configure(EntityTypeBuilder<AsnShipment> builder)
    {
        builder.ToTable("AsnShipments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.AsnNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PurchaseOrderId).IsRequired();
        builder.Property(x => x.SupplierId).IsRequired();
        builder.Property(x => x.ExpectedDate).IsRequired();
        builder.Property(x => x.QrPayload).HasColumnType("text").IsRequired();
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.OfflineScannedAt).IsRequired(false);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.AsnNumber).IsUnique();
        builder.HasIndex(x => new { x.PurchaseOrderId, x.TenantId });
        builder.HasIndex(x => new { x.Status, x.TenantId });
    }
}

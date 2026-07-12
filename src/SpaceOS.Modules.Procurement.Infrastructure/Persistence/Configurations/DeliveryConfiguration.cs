using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpaceOS.Modules.Procurement.Domain.Aggregates;

namespace SpaceOS.Modules.Procurement.Infrastructure.Persistence.Configurations;

public class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ToTable("Deliveries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.PurchaseOrderId).IsRequired();
        builder.Property(x => x.ReceivedQuantity).HasPrecision(10, 2);
        builder.Property(x => x.ReceivedAt).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.RecordedBy).HasMaxLength(200).IsRequired();

        // Quality Inspection (added for complaint flow)
        builder.OwnsOne(x => x.QualityInspection, qi =>
        {
            qi.Property(q => q.Status).HasColumnName("QualityStatus");
            qi.Property(q => q.AcceptedQuantity).HasColumnName("AcceptedQuantity").HasPrecision(10, 2);
            qi.Property(q => q.RejectedQuantity).HasColumnName("RejectedQuantity").HasPrecision(10, 2);
            qi.Property(q => q.DefectDescription).HasColumnName("DefectDescription").HasMaxLength(2000);
            qi.Property(q => q.DefectPhotoPaths).HasColumnName("DefectPhotoPaths").HasColumnType("jsonb");
        });

        builder.Property(x => x.InspectedAt);
        builder.Property(x => x.InspectedBy).HasMaxLength(200);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.PurchaseOrderId);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpaceOS.Modules.Procurement.Domain.Aggregates;

namespace SpaceOS.Modules.Procurement.Infrastructure.Persistence.Configurations;

public sealed class PurchaseRequisitionConfiguration : IEntityTypeConfiguration<PurchaseRequisition>
{
    public void Configure(EntityTypeBuilder<PurchaseRequisition> builder)
    {
        builder.ToTable("purchase_requisition");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.RequisitionNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.SourceReference);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.RequestedBy).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ApprovedBy).HasMaxLength(128);
        builder.Property(x => x.ApprovedAt);
        builder.Property(x => x.RejectedReason).HasMaxLength(2000);
        builder.Property(x => x.ConvertedPurchaseOrderId);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).IsRequired();

        // DB-P-01: system xmin rowversion (no user column in DDL)
        builder.Property<uint>("xmin").IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        // DB-P-03: alternate key for composite FK target
        builder.HasAlternateKey("Id", "TenantId").HasName("UQ_Requisition_Id_Tenant");

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.Status });

        // BE-P-04: owned lines with composite FK
        builder.OwnsMany(x => x.Lines, line =>
        {
            line.ToTable("purchase_requisition_line");
            line.HasKey(l => l.Id);
            line.Property(l => l.Id).IsRequired();
            line.Property(l => l.TenantId).IsRequired();
            line.Property(l => l.MaterialCode).HasMaxLength(20).IsRequired();
            line.Property(l => l.Quantity).IsRequired();
            line.Property(l => l.EstimatedUnitPrice).HasPrecision(18, 4);
            line.Property(l => l.PreferredSupplierId);
            line.Property(l => l.Notes).HasMaxLength(500);

            // BE-P-04: composite FK (ParentId, TenantId) → parent alternate key
            line.WithOwner().HasForeignKey("RequisitionId", "TenantId").HasPrincipalKey("Id", "TenantId");
        });

        // private backing field: _lines
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

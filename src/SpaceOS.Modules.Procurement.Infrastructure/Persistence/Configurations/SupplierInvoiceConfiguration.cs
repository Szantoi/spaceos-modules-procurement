using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpaceOS.Modules.Procurement.Domain.Aggregates;

namespace SpaceOS.Modules.Procurement.Infrastructure.Persistence.Configurations;

public sealed class SupplierInvoiceConfiguration : IEntityTypeConfiguration<SupplierInvoice>
{
    public void Configure(EntityTypeBuilder<SupplierInvoice> builder)
    {
        builder.ToTable("supplier_invoice");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.SupplierId).IsRequired();
        builder.Property(x => x.PurchaseOrderId).IsRequired();
        builder.Property(x => x.SupplierInvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.InvoiceDate).IsRequired();
        builder.Property(x => x.DueDate);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.TotalNetAmount).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.TotalVatAmount).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.TotalGrossAmount).HasPrecision(18, 4).IsRequired();
        builder.Property(x => x.LatestMatchId);
        builder.Property(x => x.RecordedBy).HasMaxLength(128).IsRequired();
        builder.Property(x => x.VarianceApprovedBy).HasMaxLength(128);
        builder.Property(x => x.DisputeReason).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).IsRequired();

        // DB-P-01: system xmin rowversion
        builder.Property<uint>("xmin").IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        // DB-P-03: alternate key for composite FK target
        builder.HasAlternateKey("Id", "TenantId").HasName("UQ_Invoice_Id_Tenant");

        builder.HasIndex(x => new { x.TenantId, x.SupplierId });
        builder.HasIndex(x => new { x.TenantId, x.PurchaseOrderId });

        // BE-P-04: owned lines with composite FK
        builder.OwnsMany(x => x.Lines, line =>
        {
            line.ToTable("supplier_invoice_line");
            line.HasKey(l => l.Id);
            line.Property(l => l.Id).IsRequired();
            line.Property(l => l.TenantId).IsRequired();
            line.Property(l => l.MaterialCode).HasMaxLength(20).IsRequired();
            line.Property(l => l.PurchaseOrderLineId);
            line.Property(l => l.Quantity).IsRequired();
            line.Property(l => l.UnitPrice).HasPrecision(18, 4).IsRequired();
            line.Property(l => l.LineNetAmount).HasPrecision(18, 4).IsRequired();
            line.Property(l => l.LineVatAmount).HasPrecision(18, 4).IsRequired();
            line.Property(l => l.LineGrossAmount).HasPrecision(18, 4).IsRequired();

            line.WithOwner().HasForeignKey("InvoiceId", "TenantId").HasPrincipalKey("Id", "TenantId");
        });

        // private backing field: _lines
        builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

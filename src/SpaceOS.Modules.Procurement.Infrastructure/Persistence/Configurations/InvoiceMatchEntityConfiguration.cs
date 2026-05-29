using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpaceOS.Modules.Procurement.Domain.Aggregates;

namespace SpaceOS.Modules.Procurement.Infrastructure.Persistence.Configurations;

public sealed class InvoiceMatchEntityConfiguration : IEntityTypeConfiguration<InvoiceMatchEntity>
{
    public void Configure(EntityTypeBuilder<InvoiceMatchEntity> builder)
    {
        builder.ToTable("invoice_match");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.InvoiceId).IsRequired();
        builder.Property(x => x.PurchaseOrderId).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(20).IsRequired();
        builder.Property(x => x.LineDetailJson).IsRequired();
        builder.Property(x => x.VarianceSummary).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.PriceTolerancePct).HasPrecision(6, 4).IsRequired();
        builder.Property(x => x.QuantityToleranceAbs).IsRequired();
        builder.Property(x => x.EvaluatedAt).IsRequired();

        builder.HasIndex(x => x.InvoiceId);
    }
}

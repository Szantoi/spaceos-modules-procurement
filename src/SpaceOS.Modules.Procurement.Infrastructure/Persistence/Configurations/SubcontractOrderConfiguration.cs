using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpaceOS.Modules.Procurement.Domain.Aggregates;

namespace SpaceOS.Modules.Procurement.Infrastructure.Persistence.Configurations;

public class SubcontractOrderConfiguration : IEntityTypeConfiguration<SubcontractOrder>
{
    public void Configure(EntityTypeBuilder<SubcontractOrder> builder)
    {
        builder.ToTable("SubcontractOrders");
        builder.HasKey(x => x.Id);

        // Basic properties
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.SupplierId).IsRequired();
        builder.Property(x => x.OrderNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).IsRequired();

        // Order details
        builder.Property(x => x.WorkDescription).HasColumnType("text").IsRequired();
        builder.Property(x => x.EstimatedCost).HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Deadline).IsRequired();

        // Status tracking
        builder.Property(x => x.RejectionReason).HasColumnType("text");
        builder.Property(x => x.AcceptedAt);
        builder.Property(x => x.CompletedAt);

        // Audit
        builder.Property(x => x.CreatedBy).HasMaxLength(255).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        // Indexes
        builder.HasIndex(x => new { x.TenantId, x.OrderNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.SupplierId });
        builder.HasIndex(x => new { x.TenantId, x.Status });
    }
}

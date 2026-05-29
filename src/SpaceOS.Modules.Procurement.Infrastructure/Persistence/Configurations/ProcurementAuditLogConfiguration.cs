using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpaceOS.Modules.Procurement.Domain.Aggregates;

namespace SpaceOS.Modules.Procurement.Infrastructure.Persistence.Configurations;

public sealed class ProcurementAuditLogConfiguration : IEntityTypeConfiguration<ProcurementAuditLog>
{
    public void Configure(EntityTypeBuilder<ProcurementAuditLog> builder)
    {
        builder.ToTable("procurement_audit_log");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Actor).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(64).IsRequired();
        builder.Property(x => x.AggregateType).HasMaxLength(48).IsRequired();
        builder.Property(x => x.AggregateId).IsRequired();
        builder.Property(x => x.BeforeJson);
        builder.Property(x => x.AfterJson);
        builder.Property(x => x.SourceIp).HasMaxLength(45);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.AggregateId });
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
    }
}

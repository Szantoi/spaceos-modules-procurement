using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpaceOS.Modules.Procurement.Domain.Aggregates;

namespace SpaceOS.Modules.Procurement.Infrastructure.Persistence.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasColumnName("ContactEmail").HasMaxLength(200).IsRequired(false);
        builder.Property(x => x.Phone).HasMaxLength(50).IsRequired(false);
        builder.Property(x => x.Address).HasMaxLength(500).IsRequired(false);
        builder.Property(x => x.LeadTimeDays).IsRequired();
        builder.Property(x => x.Rating).HasPrecision(3, 1);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.IsActive });
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpaceOS.Modules.Procurement.Domain.Aggregates;

namespace SpaceOS.Modules.Procurement.Infrastructure.Persistence.Configurations;

public sealed class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        builder.ToTable("price_list");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.SupplierId).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.ValidFrom).IsRequired();
        builder.Property(x => x.ValidTo);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        // DB-P-01: system xmin rowversion
        builder.Property<uint>("xmin").IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        // DB-P-03: alternate key for composite FK target
        builder.HasAlternateKey("Id", "TenantId").HasName("UQ_PriceList_Id_Tenant");

        builder.HasIndex(x => new { x.TenantId, x.SupplierId });

        // BE-P-04: owned entries with composite FK
        builder.OwnsMany(x => x.Entries, entry =>
        {
            entry.ToTable("price_list_entry");
            entry.HasKey(e => e.Id);
            entry.Property(e => e.Id).IsRequired();
            entry.Property(e => e.TenantId).IsRequired();
            entry.Property(e => e.MaterialCode).HasMaxLength(20).IsRequired();
            entry.Property(e => e.UnitPrice).HasPrecision(18, 4).IsRequired();
            entry.Property(e => e.MinQuantity).IsRequired();
            entry.Property(e => e.MaxQuantity);

            entry.WithOwner().HasForeignKey("PriceListId", "TenantId").HasPrincipalKey("Id", "TenantId");
        });

        // private backing field: _entries
        builder.Navigation(x => x.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpaceOS.Modules.Procurement.Domain.Aggregates;

namespace SpaceOS.Modules.Procurement.Infrastructure.Persistence.Configurations;

public sealed class MatchPolicyEntityConfiguration : IEntityTypeConfiguration<MatchPolicyEntity>
{
    public void Configure(EntityTypeBuilder<MatchPolicyEntity> builder)
    {
        builder.ToTable("procurement_match_policy");
        builder.HasKey(x => x.TenantId);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.PriceTolerancePct).HasPrecision(6, 4).IsRequired();
        builder.Property(x => x.QuantityToleranceAbs).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
    }
}

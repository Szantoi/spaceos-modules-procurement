namespace SpaceOS.Modules.Procurement.Infrastructure.Adapters;

public interface IProcurementTenantAccessor
{
    Guid TenantId { get; }
}

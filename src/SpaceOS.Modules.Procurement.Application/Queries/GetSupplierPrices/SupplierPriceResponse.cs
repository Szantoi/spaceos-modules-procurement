namespace SpaceOS.Modules.Procurement.Application.Queries.GetSupplierPrices;

public sealed record SupplierPriceResponse(
    Guid SupplierId,
    string SupplierName,
    decimal UnitPrice,
    int LeadTimeDays);

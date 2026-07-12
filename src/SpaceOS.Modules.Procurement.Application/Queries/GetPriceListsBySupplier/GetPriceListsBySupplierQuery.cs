using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Application.Dtos;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetPriceListsBySupplier;

/// <summary>
/// Query to get all price lists for a specific supplier.
/// BE-PROC-001: Supplier self-service price list management.
/// </summary>
public sealed record GetPriceListsBySupplierQuery(
    Guid TenantId,
    Guid SupplierId) : IRequest<Result<IReadOnlyList<PriceListDto>>>;

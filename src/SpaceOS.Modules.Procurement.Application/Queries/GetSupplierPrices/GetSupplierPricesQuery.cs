using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetSupplierPrices;

public sealed record GetSupplierPricesQuery(Guid TenantId, string MaterialType) : IRequest<Result<IReadOnlyList<SupplierPriceResponse>>>;

using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Queries.GetSuppliers;

public sealed record GetSuppliersQuery(Guid TenantId) : IRequest<Result<IReadOnlyList<SupplierResponse>>>;

public sealed record SupplierResponse(Guid Id, string Name, string ContactEmail, int LeadTimeDays, decimal Rating, DateTime CreatedAt);

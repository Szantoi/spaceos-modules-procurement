using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.CreatePurchaseRequisition;

public sealed record RequisitionLineRequest(
    string MaterialCode,
    int Quantity,
    decimal? EstimatedUnitPrice,
    Guid? PreferredSupplierId,
    string? Notes);

public sealed record CreatePurchaseRequisitionCommand(
    Guid TenantId,
    string RequestedBy,
    IReadOnlyList<RequisitionLineRequest> Lines,
    string? Notes = null) : IRequest<Result<Guid>>;

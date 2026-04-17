using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.CreateSupplier;

public sealed record CreateSupplierCommand(
    Guid TenantId,
    string Name,
    string ContactEmail) : IRequest<Result<CreateSupplierResult>>;

public sealed record CreateSupplierResult(Guid Id, string Name, Guid TenantId, DateTime CreatedAt);

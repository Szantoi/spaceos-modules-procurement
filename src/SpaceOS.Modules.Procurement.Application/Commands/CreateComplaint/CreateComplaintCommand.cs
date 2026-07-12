using Ardalis.Result;
using MediatR;
using SpaceOS.Modules.Procurement.Domain.Enums;

namespace SpaceOS.Modules.Procurement.Application.Commands.CreateComplaint;

public sealed record CreateComplaintCommand(
    Guid TenantId,
    Guid SupplierId,
    Guid DeliveryId,
    Guid? PurchaseOrderId,
    ComplaintType Type,
    string Subject,
    string Description,
    decimal AffectedQuantity,
    decimal? ClaimedAmount,
    string? Currency,
    List<string>? EvidencePaths,
    string CreatedBy) : IRequest<Result<Guid>>;

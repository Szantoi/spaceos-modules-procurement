using Ardalis.Result;
using MediatR;

namespace SpaceOS.Modules.Procurement.Application.Commands.ReceiveInvoice;

public sealed record InvoiceLineRequest(
    string MaterialCode,
    Guid? PurchaseOrderLineId,
    int Quantity,
    decimal UnitPrice,
    decimal LineNetAmount,
    decimal LineVatAmount);

public sealed record ReceiveInvoiceCommand(
    Guid TenantId,
    Guid SupplierId,
    Guid PurchaseOrderId,
    string SupplierInvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string Currency,
    string RecordedBy,
    IReadOnlyList<InvoiceLineRequest> Lines) : IRequest<Result<Guid>>;

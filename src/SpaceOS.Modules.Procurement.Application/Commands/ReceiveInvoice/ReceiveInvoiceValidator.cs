using FluentValidation;

namespace SpaceOS.Modules.Procurement.Application.Commands.ReceiveInvoice;

public sealed class ReceiveInvoiceValidator : AbstractValidator<ReceiveInvoiceCommand>
{
    public ReceiveInvoiceValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.SupplierInvoiceNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Currency).Matches(@"^[A-Z]{3}$").WithMessage("Currency must be a valid ISO 4217 code.");
        RuleFor(x => x.RecordedBy).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one invoice line is required.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.MaterialCode).NotEmpty().MaximumLength(20);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThan(0);
            line.RuleFor(l => l.LineNetAmount).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.LineVatAmount).GreaterThanOrEqualTo(0);
        });
    }
}

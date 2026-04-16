using FluentValidation;

namespace SpaceOS.Modules.Procurement.Application.Commands.RecordDelivery;

public sealed class RecordDeliveryValidator : AbstractValidator<RecordDeliveryCommand>
{
    public RecordDeliveryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.ReceivedQuantity).GreaterThan(0);
        RuleFor(x => x.RecordedBy).NotEmpty();
    }
}

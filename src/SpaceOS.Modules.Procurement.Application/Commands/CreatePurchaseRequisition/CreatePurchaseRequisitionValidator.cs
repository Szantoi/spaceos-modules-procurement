using FluentValidation;

namespace SpaceOS.Modules.Procurement.Application.Commands.CreatePurchaseRequisition;

public sealed class CreatePurchaseRequisitionValidator : AbstractValidator<CreatePurchaseRequisitionCommand>
{
    public CreatePurchaseRequisitionValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.RequestedBy).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes is not null);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line is required.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.MaterialCode).NotEmpty().MaximumLength(20);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.EstimatedUnitPrice).GreaterThan(0).When(l => l.EstimatedUnitPrice.HasValue);
            line.RuleFor(l => l.Notes).MaximumLength(500).When(l => l.Notes is not null);
        });
    }
}

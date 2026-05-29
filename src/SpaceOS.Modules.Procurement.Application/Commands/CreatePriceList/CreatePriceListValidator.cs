using FluentValidation;

namespace SpaceOS.Modules.Procurement.Application.Commands.CreatePriceList;

public sealed class CreatePriceListValidator : AbstractValidator<CreatePriceListCommand>
{
    public CreatePriceListValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.Currency).Matches(@"^[A-Z]{3}$").WithMessage("Currency must be a valid ISO 4217 code.");
        RuleFor(x => x.Entries).NotEmpty().WithMessage("At least one entry is required.");
        RuleForEach(x => x.Entries).ChildRules(e =>
        {
            e.RuleFor(en => en.MaterialCode).NotEmpty().MaximumLength(20);
            e.RuleFor(en => en.UnitPrice).GreaterThan(0);
            e.RuleFor(en => en.MinQuantity).GreaterThanOrEqualTo(1);
        });
    }
}

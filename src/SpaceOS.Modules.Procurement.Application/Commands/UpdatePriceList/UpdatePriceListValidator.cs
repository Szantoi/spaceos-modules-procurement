using FluentValidation;

namespace SpaceOS.Modules.Procurement.Application.Commands.UpdatePriceList;

public sealed class UpdatePriceListValidator : AbstractValidator<UpdatePriceListCommand>
{
    public UpdatePriceListValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("TenantId is required.");
        RuleFor(x => x.PriceListId).NotEmpty().WithMessage("PriceListId is required.");
        RuleFor(x => x.ValidFrom).NotEmpty().WithMessage("ValidFrom is required.");
        RuleFor(x => x.ValidTo)
            .Must((cmd, validTo) => !validTo.HasValue || validTo.Value >= cmd.ValidFrom)
            .When(x => x.ValidTo.HasValue)
            .WithMessage("ValidTo must be >= ValidFrom.");
        RuleFor(x => x.Entries).NotEmpty().WithMessage("At least one entry is required.");
        RuleForEach(x => x.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.MaterialCode).NotEmpty().MaximumLength(50);
            entry.RuleFor(e => e.UnitPrice).GreaterThan(0);
            entry.RuleFor(e => e.MinQuantity).GreaterThan(0);
            entry.RuleFor(e => e.MaxQuantity)
                .Must((e, maxQty) => !maxQty.HasValue || maxQty.Value >= e.MinQuantity)
                .When(e => e.MaxQuantity.HasValue)
                .WithMessage("MaxQuantity must be >= MinQuantity.");
        });
    }
}

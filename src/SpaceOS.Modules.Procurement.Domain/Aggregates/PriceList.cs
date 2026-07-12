using Ardalis.Result;
using SpaceOS.Modules.Procurement.Domain.Common;
using SpaceOS.Modules.Procurement.Domain.Enums;
using SpaceOS.Modules.Procurement.Domain.Events;

namespace SpaceOS.Modules.Procurement.Domain.Aggregates;

/// <summary>
/// Price list aggregate root.
/// FSM: Draft → Active → Expired
/// DB-P-09: ActivatePriceList includes domain-level overlap guard.
/// </summary>
public sealed class PriceList : AggregateRoot
{
    private readonly List<PriceListEntry> _entries = new();

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SupplierId { get; private set; }

    /// <summary>ISO 4217 currency code.</summary>
    public string Currency { get; private set; } = string.Empty;

    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }
    public PriceListStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>Read-only view of price list entries.</summary>
    public IReadOnlyList<PriceListEntry> Entries => _entries.AsReadOnly();

    private PriceList() { }

    /// <summary>Creates a new draft price list.</summary>
    public static Result<PriceList> Create(
        Guid tenantId,
        Guid supplierId,
        string currency,
        DateOnly validFrom,
        DateOnly? validTo,
        IReadOnlyList<(string MaterialCode, decimal UnitPrice, int MinQuantity, int? MaxQuantity)> entries)
    {
        if (tenantId == Guid.Empty)
            return Result<PriceList>.Invalid(new ValidationError("TenantId is required."));
        if (supplierId == Guid.Empty)
            return Result<PriceList>.Invalid(new ValidationError("SupplierId is required."));
        if (!System.Text.RegularExpressions.Regex.IsMatch(currency, @"^[A-Z]{3}$"))
            return Result<PriceList>.Invalid(new ValidationError("Currency must be a valid ISO 4217 code."));
        if (validTo.HasValue && validTo.Value < validFrom)
            return Result<PriceList>.Invalid(new ValidationError("ValidTo must be >= ValidFrom."));
        if (entries is null || entries.Count == 0)
            return Result<PriceList>.Invalid(new ValidationError("At least one entry is required."));

        var priceList = new PriceList
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierId = supplierId,
            Currency = currency,
            ValidFrom = validFrom,
            ValidTo = validTo,
            Status = PriceListStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var (mc, up, minQty, maxQty) in entries)
        {
            var entry = PriceListEntry.Create(priceList.Id, tenantId, mc, up, minQty, maxQty);
            priceList._entries.Add(entry);
        }

        priceList.RaiseDomainEvent(new PriceListCreatedEvent(priceList.Id, tenantId, supplierId, currency));
        return Result<PriceList>.Success(priceList);
    }

    /// <summary>
    /// Activates the price list.
    /// Guard: Status must be Draft.
    /// The caller (ActivatePriceListCommandHandler) is responsible for checking overlapping
    /// active price lists (DB-P-09 domain-level check) before calling this method.
    /// </summary>
    public Result Activate()
    {
        if (Status != PriceListStatus.Draft)
            return Result.Invalid(new ValidationError($"Cannot activate a price list in status {Status}."));

        Status = PriceListStatus.Active;
        RaiseDomainEvent(new PriceListActivatedEvent(Id, TenantId, SupplierId, Currency, ValidFrom, ValidTo));
        return Result.Success();
    }

    /// <summary>Expires the price list. Guard: Status must be Active.</summary>
    public Result Expire()
    {
        if (Status != PriceListStatus.Active)
            return Result.Invalid(new ValidationError($"Cannot expire a price list in status {Status}."));

        Status = PriceListStatus.Expired;
        RaiseDomainEvent(new PriceListExpiredEvent(Id, TenantId));
        return Result.Success();
    }

    /// <summary>
    /// Updates the price list entries and validity period.
    /// Guard: Status must be Draft.
    /// </summary>
    public Result Update(
        DateOnly validFrom,
        DateOnly? validTo,
        IReadOnlyList<(string MaterialCode, decimal UnitPrice, int MinQuantity, int? MaxQuantity)> entries)
    {
        if (Status != PriceListStatus.Draft)
            return Result.Invalid(new ValidationError($"Cannot update a price list in status {Status}. Only Draft price lists can be edited."));
        if (validTo.HasValue && validTo.Value < validFrom)
            return Result.Invalid(new ValidationError("ValidTo must be >= ValidFrom."));
        if (entries is null || entries.Count == 0)
            return Result.Invalid(new ValidationError("At least one entry is required."));

        ValidFrom = validFrom;
        ValidTo = validTo;

        _entries.Clear();
        foreach (var (mc, up, minQty, maxQty) in entries)
        {
            var entry = PriceListEntry.Create(Id, TenantId, mc, up, minQty, maxQty);
            _entries.Add(entry);
        }

        return Result.Success();
    }
}

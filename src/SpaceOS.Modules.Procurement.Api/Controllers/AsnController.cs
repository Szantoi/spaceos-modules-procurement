using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Domain.Services;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;

namespace SpaceOS.Modules.Procurement.Api.Controllers;

/// <summary>
/// ASN (Advanced Shipping Notice) tracking API
/// </summary>
[ApiController]
[Authorize]
[Route("api/suppliers/asn")]
public class AsnController : ControllerBase
{
    private readonly ProcurementDbContext _db;
    private readonly IAsnHashService _hashService;

    public AsnController(ProcurementDbContext db, IAsnHashService hashService)
    {
        _db = db;
        _hashService = hashService;
    }

    /// <summary>
    /// Generates a new ASN with QR payload (Week 3 production API)
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateAsnRequest request, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty)
            return Unauthorized(new { error = "Invalid tenant context" });

        // Validate PO exists
        var po = await _db.PurchaseOrders
            .Where(p => p.Id == request.PoId && p.TenantId == tenantId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (po == null)
            return NotFound(new { error = $"Purchase order {request.PoId} not found" });

        // Generate ASN number (simple counter-based, could be enhanced)
        var asnNumber = $"ASN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";

        // Generate QR payload with hash
        var qrPayload = _hashService.GenerateQrPayload(
            asnNumber,
            request.PoId.ToString(),
            request.ExpectedDate);

        // Create ASN entity
        var asn = AsnShipment.Create(
            tenantId,
            asnNumber,
            request.PoId,
            po.SupplierId,
            request.ExpectedDate,
            qrPayload);

        _db.AsnShipments.Add(asn);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        var printableUrl = $"/print/asn/{asnNumber}";

        return Ok(new GenerateAsnResponse(asnNumber, qrPayload, printableUrl));
    }

    /// <summary>
    /// Scans and validates an ASN QR code (Week 3 production API)
    /// </summary>
    [HttpPost("receipt/scan")]
    public async Task<IActionResult> Scan([FromBody] ScanReceiptRequest request, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty)
            return Unauthorized(new { error = "Invalid tenant context" });

        // Validate QR payload hash
        if (!_hashService.ValidateQrPayload(request.QrPayload))
            return BadRequest(new
            {
                valid = false,
                error = "Invalid QR payload hash",
                hashVerified = false
            });

        // Parse QR payload
        var parsed = _hashService.ParseQrPayload(request.QrPayload);
        if (parsed == null)
            return BadRequest(new
            {
                valid = false,
                error = "Invalid QR payload format",
                hashVerified = false
            });

        var (asnNumber, poIdString, expectedDate, _) = parsed.Value;

        if (!Guid.TryParse(poIdString, out var poId))
            return BadRequest(new
            {
                valid = false,
                error = "Invalid PO ID in QR payload",
                hashVerified = true
            });

        // Find ASN
        var asn = await _db.AsnShipments
            .Where(a => a.AsnNumber == asnNumber && a.TenantId == tenantId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (asn == null)
            return NotFound(new
            {
                valid = false,
                error = $"ASN {asnNumber} not found",
                hashVerified = true
            });

        // Get PO details
        var po = await _db.PurchaseOrders
            .Where(p => p.Id == poId && p.TenantId == tenantId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (po == null)
            return NotFound(new
            {
                valid = false,
                error = $"Purchase order {poId} not found",
                hashVerified = true
            });

        // Create receipt queue entry
        var userId = GetUserId();
        var receipt = ReceiptQueue.Create(
            tenantId,
            asn.Id,
            userId,
            request.ActualQuantity,
            DateTime.UtcNow);

        _db.ReceiptQueues.Add(receipt);

        // Mark ASN as received
        asn.MarkAsReceived();

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Ok(new ScanReceiptResponse(
            valid: true,
            po: new PoSummary(po.Id, po.MaterialType, po.Quantity, po.UnitPrice, po.Currency),
            hashVerified: true,
            nextAction: "QUANTITY_CONFIRM"));
    }

    private Guid GetTenantId()
    {
        var claim = User.FindFirst("tenant_id");
        return claim is not null && Guid.TryParse(claim.Value, out var tenantId)
            ? tenantId
            : Guid.Empty;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst("sub") ?? User.FindFirst("user_id");
        return claim is not null && Guid.TryParse(claim.Value, out var userId)
            ? userId
            : Guid.Empty;
    }
}

// Request/Response DTOs
public record GenerateAsnRequest(
    Guid PoId,
    DateTime ExpectedDate);

public record GenerateAsnResponse(
    string Asn,
    string QrPayload,
    string PrintableUrl);

public record ScanReceiptRequest(
    string QrPayload,
    int ActualQuantity);

public record ScanReceiptResponse(
    bool valid,
    PoSummary po,
    bool hashVerified,
    string nextAction);

public record PoSummary(
    Guid Id,
    string MaterialType,
    decimal Quantity,
    decimal UnitPrice,
    string Currency);

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;

namespace SpaceOS.Modules.Procurement.Api.Controllers;

/// <summary>
/// Partner KPI Analytics API (Week 3)
/// </summary>
[ApiController]
[Authorize]
[Route("api/analytics/partners")]
public class AnalyticsController : ControllerBase
{
    private readonly ProcurementDbContext _db;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public AnalyticsController(ProcurementDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <summary>
    /// Gets KPI metrics for a specific supplier (partner)
    /// </summary>
    /// <param name="id">Supplier ID</param>
    /// <param name="period">Period (e.g., "30d", "90d") - default 30 days</param>
    /// <param name="ct">Cancellation token</param>
    [HttpGet("{id:guid}/kpi")]
    public async Task<IActionResult> GetPartnerKpi(Guid id, [FromQuery] string? period, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty)
            return Unauthorized(new { error = "Invalid tenant context" });

        // Parse period parameter
        var days = ParsePeriod(period ?? "30d");
        var since = DateTime.UtcNow.AddDays(-days);

        // Check cache
        var cacheKey = $"kpi_{tenantId}_{id}_{days}d";
        if (_cache.TryGetValue<PartnerKpiResponse>(cacheKey, out var cachedResult) && cachedResult != null)
        {
            return Ok(cachedResult);
        }

        // Verify supplier exists and belongs to tenant
        var supplier = await _db.Suppliers
            .Where(s => s.Id == id && s.TenantId == tenantId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (supplier == null)
            return NotFound(new { error = $"Supplier {id} not found" });

        // Get all POs for this supplier in the period
        var pos = await _db.PurchaseOrders
            .Where(po => po.SupplierId == id
                      && po.TenantId == tenantId
                      && po.CreatedAt >= since)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (pos.Count == 0)
        {
            // No data for this period
            return Ok(new PartnerKpiResponse(
                onTimeDelivery: new KpiMetric(0.0m, 0.0m, pos.Count),
                avgLeadTime: new LeadTimeMetric(0, 0),
                qualityRate: new KpiMetric(0.0m, 0.0m, 1.0m)));
        }

        // Get deliveries for these POs
        var poIds = pos.Select(po => po.Id).ToList();
        var deliveries = await _db.Deliveries
            .Where(d => poIds.Contains(d.PurchaseOrderId) && d.TenantId == tenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Calculate KPIs
        var onTimeCount = 0;
        var totalDelivered = 0;
        var missingDataCount = 0;
        var totalLeadTimeDays = 0;
        var leadTimeCount = 0;

        foreach (var po in pos)
        {
            var delivery = deliveries.FirstOrDefault(d => d.PurchaseOrderId == po.Id);

            if (delivery != null)
            {
                totalDelivered++;

                if (po.ExpectedDeliveryDate.HasValue)
                {
                    if (delivery.ReceivedAt <= po.ExpectedDeliveryDate.Value)
                        onTimeCount++;
                }
                else
                {
                    missingDataCount++;
                }

                // Lead time calculation
                var leadTimeDays = (int)(delivery.ReceivedAt - po.CreatedAt).TotalDays;
                if (leadTimeDays >= 0)
                {
                    totalLeadTimeDays += leadTimeDays;
                    leadTimeCount++;
                }
            }
            else
            {
                // PO created but not delivered yet
                missingDataCount++;
            }
        }

        var onTimeDeliveryRate = totalDelivered > 0
            ? (decimal)onTimeCount / (totalDelivered - missingDataCount)
            : 0.0m;

        var avgLeadTimeDays = leadTimeCount > 0
            ? totalLeadTimeDays / leadTimeCount
            : 0;

        // Quality rate calculation
        var inspectedDeliveries = deliveries.Where(d => d.QualityInspection != null).ToList();
        var passedInspections = inspectedDeliveries.Count(d =>
            d.QualityInspection!.Status == SpaceOS.Modules.Procurement.Domain.Enums.QualityStatus.Passed);
        var qualityRate = inspectedDeliveries.Count > 0
            ? (decimal)passedInspections / inspectedDeliveries.Count
            : 0.0m;

        var dataCompleteness = pos.Count > 0
            ? 1.0m - ((decimal)missingDataCount / pos.Count)
            : 1.0m;

        // Previous period for trend calculation (simplified: -5% placeholder)
        var trend = -0.05m;
        var leadTimeTrend = -2;

        var result = new PartnerKpiResponse(
            onTimeDelivery: new KpiMetric(onTimeDeliveryRate, trend, missingDataCount),
            avgLeadTime: new LeadTimeMetric(avgLeadTimeDays, leadTimeTrend),
            qualityRate: new KpiMetric(qualityRate, trend, dataCompleteness));

        // Cache result
        _cache.Set(cacheKey, result, CacheDuration);

        return Ok(result);
    }

    private static int ParsePeriod(string period)
    {
        if (period.EndsWith("d", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(period[..^1], out var days))
                return days;
        }

        return 30; // default
    }

    private Guid GetTenantId()
    {
        var claim = User.FindFirst("tenant_id");
        return claim is not null && Guid.TryParse(claim.Value, out var tenantId)
            ? tenantId
            : Guid.Empty;
    }
}

// Response DTOs
public record PartnerKpiResponse(
    KpiMetric onTimeDelivery,
    LeadTimeMetric avgLeadTime,
    KpiMetric qualityRate);

public record KpiMetric(
    decimal Value,
    decimal Trend,
    decimal DataCompletenessOrMissingCount);

public record LeadTimeMetric(
    int Days,
    int Trend);

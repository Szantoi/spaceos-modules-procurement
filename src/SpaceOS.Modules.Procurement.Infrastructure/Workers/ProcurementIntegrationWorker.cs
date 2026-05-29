using System.Net;
using System.Security;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;

namespace SpaceOS.Modules.Procurement.Infrastructure.Workers;

/// <summary>
/// Background worker that polls the procurement outbox and dispatches
/// InventoryInboundRequested messages to the Inventory service.
/// Three-phase protocol (ADR-039):
///   Phase 1 — CLAIM: FOR UPDATE SKIP LOCKED, lease 60 s.
///   Phase 2 — PROCESS: HTTP POST to Inventory (manual retry + circuit-breaker).
///   Phase 3 — COMPLETE: mark Completed/Failed; update DeliveryLine.InventorySyncStatus.
/// SEC-P-04: explicit tenant guard per message (fail-closed on mismatch).
/// BE-P-02: manual retry — only transient errors retried, permanents (422/400/403) fail immediately.
/// BE-P-03: reclaim expired InFlight leases.
/// BE-P-08: duplicate 200 from Inventory → Completed.
/// BE-P-10: uses dedicated worker connection string when available.
/// SEC-P-11: only error type logged — no payload, no exception message.
/// </summary>
public sealed class ProcurementIntegrationWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int MaxAttempts = 3;
    private const int LeaseDurationSeconds = 60;
    private const string InventoryBaseUrl = "http://127.0.0.1:5004";
    private const string InventoryInboundPath = "/inventory/internal/inbound";
    private const string InternalSecretEnvKey = "SPACEOS_INTERNAL_SECRET";

    // Circuit-breaker state
    private int _consecutiveTransientFailures;
    private DateTimeOffset _circuitOpenUntil = DateTimeOffset.MinValue;
    private const int CircuitBreakerThreshold = 3;
    private static readonly TimeSpan CircuitBreakerCooldown = TimeSpan.FromSeconds(60);

    private readonly IProcurementWorkerDbContextFactory _dbFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ProcurementIntegrationWorker> _logger;

    public ProcurementIntegrationWorker(
        IProcurementWorkerDbContextFactory dbFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<ProcurementIntegrationWorker> logger)
    {
        _dbFactory = dbFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Outbox poll batch failed — errorType {ErrorType}", ex.GetType().Name);
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        // Circuit-breaker check
        if (DateTimeOffset.UtcNow < _circuitOpenUntil)
        {
            _logger.LogWarning("Circuit breaker open — skipping batch until {OpenUntil}", _circuitOpenUntil);
            return;
        }

        await using var db = await _dbFactory.CreateAsync(ct).ConfigureAwait(false);

        // ── Phase 1: CLAIM ───────────────────────────────────────────────────
        List<Guid> claimedIds;
        await using (var claimTx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false))
        {
            var now = DateTimeOffset.UtcNow;

            List<Domain.Aggregates.ProcurementOutboxMessage> batch;

            if (db.Database.IsRelational())
            {
                batch = await db.OutboxMessages
                    .FromSqlRaw(@"
                        SELECT * FROM spaceos_procurement.procurement_outbox
                        WHERE (""Status"" = 'Pending' AND ""NextAttemptAt"" <= NOW())
                           OR (""Status"" = 'InFlight' AND ""LeaseUntil"" < NOW())
                        ORDER BY ""NextAttemptAt"" ASC
                        FOR UPDATE SKIP LOCKED
                        LIMIT 10")
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
            }
            else
            {
                // In-memory fallback for tests
                batch = await db.OutboxMessages
                    .Where(m => (m.Status == "Pending" && m.NextAttemptAt <= now)
                             || (m.Status == "InFlight" && m.LeaseUntil < now))
                    .OrderBy(m => m.NextAttemptAt)
                    .Take(10)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);
            }

            foreach (var m in batch)
                m.MarkInFlight(LeaseDurationSeconds);

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await claimTx.CommitAsync(ct).ConfigureAwait(false);
            claimedIds = batch.Select(m => m.Id).ToList();
        }

        // ── Phase 2 + 3: PROCESS then COMPLETE (per message) ─────────────────
        foreach (var msgId in claimedIds)
        {
            var msg = await db.OutboxMessages
                .SingleAsync(m => m.Id == msgId, ct)
                .ConfigureAwait(false);

            try
            {
                // SEC-P-04: build payload and validate tenant
                var payload = JsonSerializer.Deserialize<InboundPayload>(msg.PayloadJson)!;

                // SEC-P-04: explicit tenant guard (fail-closed on mismatch)
                if (payload.TenantId != msg.TenantId)
                {
                    _logger.LogCritical(
                        "SEC-P-04 tenant mismatch on outbox message {MessageId}: msg.TenantId={MsgTenant}, payload.TenantId={PayloadTenant}",
                        msg.Id, msg.TenantId, payload.TenantId);

                    // Abort — mark failed immediately, do NOT crash the worker
                    await CompleteMessageAsync(db, msg, success: false,
                        isPermanent: true, errorType: "TenantMismatch", ct).ConfigureAwait(false);
                    continue;
                }

                // Build Inventory inbound receipt DTO
                var inboundDto = new InboundReceiptDto(
                    TenantId: payload.TenantId,
                    DeliveryLineId: payload.DeliveryId,
                    MaterialCode: payload.MaterialType,
                    Quantity: payload.ReceivedQuantity,
                    Unit: "pcs",
                    DeliveredAt: DateTimeOffset.UtcNow,
                    SourceReference: $"Delivery {payload.DeliveryId}");

                // HTTP call with manual retry (outside DB transaction — ADR-039)
                var httpResult = await CallInventoryWithRetryAsync(inboundDto, msg.TenantId, ct)
                    .ConfigureAwait(false);

                await using var completeTx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

                // Set per-message tenant scope (OPEN-04)
                if (db.Database.IsRelational())
                {
                    await db.Database.ExecuteSqlRawAsync(
                        "SELECT set_config('app.current_tenant_id', {0}, true)",
                        [msg.TenantId.ToString()],
                        ct).ConfigureAwait(false);
                }

                if (httpResult.IsSuccess)
                {
                    msg.MarkCompleted();

                    // Update DeliveryLine.InventorySyncStatus
                    if (db.Database.IsRelational())
                    {
                        await db.Database.ExecuteSqlRawAsync(
                            "UPDATE spaceos_procurement.\"Deliveries\" SET \"InventorySyncStatus\" = 'Synced' WHERE \"Id\" = {0}",
                            payload.DeliveryId).ConfigureAwait(false);
                    }

                    _consecutiveTransientFailures = 0;
                }
                else
                {
                    msg.RecordFailure(httpResult.ErrorType, MaxAttempts, httpResult.IsPermanent);

                    if (db.Database.IsRelational() && httpResult.IsPermanent)
                    {
                        await db.Database.ExecuteSqlRawAsync(
                            "UPDATE spaceos_procurement.\"Deliveries\" SET \"InventorySyncStatus\" = 'Failed' WHERE \"Id\" = {0}",
                            payload.DeliveryId).ConfigureAwait(false);
                    }

                    if (!httpResult.IsPermanent)
                    {
                        _consecutiveTransientFailures++;
                        if (_consecutiveTransientFailures >= CircuitBreakerThreshold)
                        {
                            _circuitOpenUntil = DateTimeOffset.UtcNow.Add(CircuitBreakerCooldown);
                            _logger.LogWarning(
                                "Circuit breaker opened after {Failures} consecutive failures — cooling for {Seconds}s",
                                _consecutiveTransientFailures, CircuitBreakerCooldown.TotalSeconds);
                            _consecutiveTransientFailures = 0;
                        }
                    }
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await completeTx.CommitAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // SEC-P-11: log error type only — no payload, no ex.Message (PII risk)
                _logger.LogWarning(
                    "Outbox {MessageId} failed (attempt {Attempt}, errorType {ErrorType})",
                    msg.Id, msg.AttemptCount, ex.GetType().Name);

                var scrubbed = ScrubErrorType(ex.GetType().Name);
                msg.RecordFailure(scrubbed, MaxAttempts, isPermanent: false);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                _consecutiveTransientFailures++;
                if (_consecutiveTransientFailures >= CircuitBreakerThreshold)
                {
                    _circuitOpenUntil = DateTimeOffset.UtcNow.Add(CircuitBreakerCooldown);
                    _logger.LogWarning(
                        "Circuit breaker opened after {Failures} consecutive failures — cooling for {Seconds}s",
                        _consecutiveTransientFailures, CircuitBreakerCooldown.TotalSeconds);
                    _consecutiveTransientFailures = 0;
                }
            }
        }
    }

    private async Task<HttpCallResult> CallInventoryWithRetryAsync(
        InboundReceiptDto dto, Guid tenantId, CancellationToken ct)
    {
        var secret = Environment.GetEnvironmentVariable(InternalSecretEnvKey) ?? string.Empty;
        var json = JsonSerializer.Serialize(dto);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient("inventory-internal");
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {secret}");
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-SpaceOS-TenantId", tenantId.ToString());

                using var response = await httpClient
                    .PostAsync(InventoryBaseUrl + InventoryInboundPath, content, ct)
                    .ConfigureAwait(false);

                // BE-P-08: idempotent 200 (dup) → success
                if (response.IsSuccessStatusCode)
                    return HttpCallResult.Success();

                // Permanent: 400, 403, 422 — no retry
                if (response.StatusCode is HttpStatusCode.BadRequest
                    or HttpStatusCode.Forbidden
                    or HttpStatusCode.UnprocessableEntity)
                {
                    return HttpCallResult.Permanent($"Http{(int)response.StatusCode}");
                }

                // Transient: 5xx, 429, etc.
                if (attempt < MaxAttempts)
                {
                    var delayMs = (int)(Math.Pow(2, attempt - 1) * 5000); // 5s, 10s, 20s
                    await Task.Delay(delayMs, ct).ConfigureAwait(false);
                }

                return HttpCallResult.Transient($"Http{(int)response.StatusCode}");
            }
            catch (HttpRequestException ex)
            {
                // Network/timeout — transient
                if (attempt < MaxAttempts)
                {
                    var delayMs = (int)(Math.Pow(2, attempt - 1) * 5000);
                    await Task.Delay(delayMs, ct).ConfigureAwait(false);
                    continue;
                }
                return HttpCallResult.Transient(ex.GetType().Name);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // Request timeout — transient
                if (attempt < MaxAttempts)
                {
                    var delayMs = (int)(Math.Pow(2, attempt - 1) * 5000);
                    await Task.Delay(delayMs, ct).ConfigureAwait(false);
                    continue;
                }
                return HttpCallResult.Transient("RequestTimeout");
            }
        }

        return HttpCallResult.Transient("MaxRetriesExceeded");
    }

    private static async Task CompleteMessageAsync(
        ProcurementDbContext db,
        Domain.Aggregates.ProcurementOutboxMessage msg,
        bool success,
        bool isPermanent,
        string errorType,
        CancellationToken ct)
    {
        if (success)
            msg.MarkCompleted();
        else
            msg.RecordFailure(errorType, MaxAttempts, isPermanent);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>SEC-P-11: scrub sensitive data from error type string.</summary>
    private static string ScrubErrorType(string errorType)
        => errorType.Length > 64 ? errorType[..64] : errorType;

    // ── Internal DTOs ─────────────────────────────────────────────────────────

    private sealed record InboundPayload(
        Guid TenantId,
        Guid DeliveryId,
        Guid PurchaseOrderId,
        string MaterialType,
        decimal ReceivedQuantity);

    private sealed record InboundReceiptDto(
        Guid TenantId,
        Guid DeliveryLineId,
        string MaterialCode,
        decimal Quantity,
        string Unit,
        DateTimeOffset DeliveredAt,
        string SourceReference);

    private sealed class HttpCallResult
    {
        public bool IsSuccess { get; private init; }
        public bool IsPermanent { get; private init; }
        public string ErrorType { get; private init; } = string.Empty;

        public static HttpCallResult Success() => new() { IsSuccess = true };
        public static HttpCallResult Permanent(string errorType) => new() { IsPermanent = true, ErrorType = errorType };
        public static HttpCallResult Transient(string errorType) => new() { IsPermanent = false, ErrorType = errorType };
    }
}

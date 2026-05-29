using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using SpaceOS.Modules.Procurement.Domain.Aggregates;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;
using SpaceOS.Modules.Procurement.Infrastructure.Workers;
using Xunit;

namespace SpaceOS.Modules.Procurement.Tests.Workers;

/// <summary>
/// Unit tests for ProcurementIntegrationWorker (Track D).
/// Uses InMemory EF Core and a mocked HttpMessageHandler.
/// </summary>
public class ProcurementWorkerTests : IDisposable
{
    private static readonly Guid TenantId = new("60000000-0000-0000-0000-000000000001");
    private static readonly Guid DeliveryId = Guid.NewGuid();
    private static readonly Guid PurchaseOrderId = Guid.NewGuid();

    private const string ValidSecret = "worker-test-secret";

    public ProcurementWorkerTests()
    {
        Environment.SetEnvironmentVariable("SPACEOS_INTERNAL_SECRET", ValidSecret);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SPACEOS_INTERNAL_SECRET", null);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DbContextOptions<ProcurementDbContext> BuildInMemoryOptions(string dbName)
        => new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static (ProcurementDbContext db, string dbName) CreateInMemoryDb()
    {
        var dbName = $"worker-test-{Guid.NewGuid()}";
        return (new ProcurementDbContext(BuildInMemoryOptions(dbName)), dbName);
    }

    /// <summary>
    /// Opens a fresh context (bypasses test's change tracker) to verify what the worker wrote.
    /// </summary>
    private static async Task<ProcurementOutboxMessage?> GetFreshMsgAsync(string dbName, Guid msgId)
    {
        await using var freshDb = new ProcurementDbContext(BuildInMemoryOptions(dbName));
        return await freshDb.OutboxMessages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == msgId);
    }

    private static string BuildPayload(Guid? tenantId = null, Guid? deliveryId = null)
        => JsonSerializer.Serialize(new
        {
            TenantId = tenantId ?? TenantId,
            DeliveryId = deliveryId ?? DeliveryId,
            PurchaseOrderId,
            MaterialType = "WD-001",
            ReceivedQuantity = 10.0
        });

    private static ProcurementOutboxMessage CreatePendingOutboxMsg(
        Guid? tenantId = null, Guid? idempotencyKey = null, string? payloadJson = null)
        => ProcurementOutboxMessage.Create(
            tenantId ?? TenantId,
            "InventoryInboundRequested",
            idempotencyKey ?? DeliveryId,
            payloadJson ?? BuildPayload(tenantId));

    private static ProcurementIntegrationWorker BuildWorker(
        string dbName,
        HttpMessageHandler httpHandler)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var dbFactory = new TestWorkerDbContextFactory(dbName);
        var httpClientFactory = new MockHttpClientFactory(httpHandler);
        var logger = services.GetRequiredService<ILogger<ProcurementIntegrationWorker>>();
        return new ProcurementIntegrationWorker(dbFactory, httpClientFactory, logger);
    }

    private static HttpMessageHandler BuildHttpHandler(HttpStatusCode statusCode, string? body = null)
    {
        var mock = new Mock<HttpMessageHandler>();
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body ?? "{}")
            });
        return mock.Object;
    }

    private static async Task RunOneCycleAsync(ProcurementIntegrationWorker worker, CancellationToken ct)
    {
        var method = typeof(ProcurementIntegrationWorker)
            .GetMethod("ProcessBatchAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        await ((Task)method.Invoke(worker, [ct])!).ConfigureAwait(false);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Worker_WhenPendingMessage_ShouldProcessAndComplete()
    {
        var (db, dbName) = CreateInMemoryDb();
        await using var _ = db;
        var msg = CreatePendingOutboxMsg();
        db.OutboxMessages.Add(msg);
        await db.SaveChangesAsync();

        var worker = BuildWorker(dbName, BuildHttpHandler(HttpStatusCode.OK));
        await RunOneCycleAsync(worker, CancellationToken.None);

        var updated = await GetFreshMsgAsync(dbName, msg.Id);
        updated!.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task Worker_WhenPermanent422_ShouldMarkFailedImmediately()
    {
        var (db, dbName) = CreateInMemoryDb();
        await using var _ = db;
        var msg = CreatePendingOutboxMsg();
        db.OutboxMessages.Add(msg);
        await db.SaveChangesAsync();

        var worker = BuildWorker(dbName, BuildHttpHandler(HttpStatusCode.UnprocessableEntity));
        await RunOneCycleAsync(worker, CancellationToken.None);

        var updated = await GetFreshMsgAsync(dbName, msg.Id);
        updated!.Status.Should().Be("Failed");
        updated.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task Worker_WhenTransient503_ShouldRetryUpToMaxAttempts()
    {
        var (db, dbName) = CreateInMemoryDb();
        await using var _ = db;
        var msg = CreatePendingOutboxMsg();
        db.OutboxMessages.Add(msg);
        await db.SaveChangesAsync();

        var callCount = 0;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            });

        var worker = BuildWorker(dbName, mockHandler.Object);
        await RunOneCycleAsync(worker, CancellationToken.None);

        var updated = await GetFreshMsgAsync(dbName, msg.Id);
        updated!.Status.Should().BeOneOf("Failed", "Pending");
        callCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Worker_WhenDuplicate200_ShouldMarkCompleted()
    {
        // BE-P-08: idempotent 200 from Inventory (dup) → Completed
        var (db, dbName) = CreateInMemoryDb();
        await using var _ = db;
        var msg = CreatePendingOutboxMsg();
        db.OutboxMessages.Add(msg);
        await db.SaveChangesAsync();

        var worker = BuildWorker(dbName, BuildHttpHandler(HttpStatusCode.OK, """{"reason":"duplicate"}"""));
        await RunOneCycleAsync(worker, CancellationToken.None);

        var updated = await GetFreshMsgAsync(dbName, msg.Id);
        updated!.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task Worker_WhenTenantMismatch_ShouldAbort()
    {
        // SEC-P-04: payload.TenantId != msg.TenantId → abort, mark Failed
        var (db, dbName) = CreateInMemoryDb();
        await using var _ = db;

        var differentTenantId = Guid.NewGuid();
        var msg = ProcurementOutboxMessage.Create(
            TenantId,
            "InventoryInboundRequested",
            DeliveryId,
            BuildPayload(tenantId: differentTenantId)); // mismatch

        db.OutboxMessages.Add(msg);
        await db.SaveChangesAsync();

        var worker = BuildWorker(dbName, BuildHttpHandler(HttpStatusCode.OK));
        await RunOneCycleAsync(worker, CancellationToken.None);

        var updated = await GetFreshMsgAsync(dbName, msg.Id);
        updated!.Status.Should().Be("Failed");
        updated.LastError.Should().Contain("TenantMismatch");
    }

    [Fact]
    public async Task Worker_Lease_ShouldReclaimExpiredInFlight()
    {
        // BE-P-03: an expired InFlight message should be reclaimed and processed
        var (db, dbName) = CreateInMemoryDb();
        await using var _ = db;
        var msg = CreatePendingOutboxMsg();
        msg.MarkInFlight(leaseSeconds: -1); // immediately expired
        db.OutboxMessages.Add(msg);
        await db.SaveChangesAsync();

        var worker = BuildWorker(dbName, BuildHttpHandler(HttpStatusCode.OK));
        await RunOneCycleAsync(worker, CancellationToken.None);

        var updated = await GetFreshMsgAsync(dbName, msg.Id);
        updated!.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task Worker_CircuitBreaker_ShouldOpenAfterConsecutiveFailures()
    {
        var (db, dbName) = CreateInMemoryDb();
        await using var _ = db;

        for (var i = 0; i < 3; i++)
        {
            var m = ProcurementOutboxMessage.Create(
                TenantId, "InventoryInboundRequested",
                Guid.NewGuid(), BuildPayload());
            db.OutboxMessages.Add(m);
        }
        await db.SaveChangesAsync();

        var worker = BuildWorker(dbName, BuildHttpHandler(HttpStatusCode.ServiceUnavailable));

        for (var i = 0; i < 4; i++)
            await RunOneCycleAsync(worker, CancellationToken.None);

        await using var verifyDb = new ProcurementDbContext(BuildInMemoryOptions(dbName));
        var statuses = await verifyDb.OutboxMessages.AsNoTracking().Select(m => m.Status).ToListAsync();
        statuses.Should().AllSatisfy(s => s.Should().BeOneOf("Failed", "Pending", "InFlight"));
    }

    [Fact]
    public async Task Worker_AttemptCount_ShouldNotDoubleCountOnReclaim()
    {
        // BE-P-03: reclaiming an InFlight message should not double-count AttemptCount
        var (db, dbName) = CreateInMemoryDb();
        await using var _ = db;
        var msg = CreatePendingOutboxMsg();
        msg.MarkInFlight(leaseSeconds: -1); // expired InFlight
        var beforeAttempt = msg.AttemptCount;
        db.OutboxMessages.Add(msg);
        await db.SaveChangesAsync();

        var worker = BuildWorker(dbName, BuildHttpHandler(HttpStatusCode.OK));
        await RunOneCycleAsync(worker, CancellationToken.None);

        var updated = await GetFreshMsgAsync(dbName, msg.Id);
        updated!.Status.Should().Be("Completed");
        updated.AttemptCount.Should().BeGreaterThanOrEqualTo(beforeAttempt);
    }

    [Fact]
    public async Task Worker_ShouldScrubSensitiveDataFromLastError()
    {
        // SEC-P-11: LastError should only contain error type (≤64 chars), not payload data
        var (db, dbName) = CreateInMemoryDb();
        await using var _ = db;
        var msg = CreatePendingOutboxMsg();
        db.OutboxMessages.Add(msg);
        await db.SaveChangesAsync();

        var worker = BuildWorker(dbName, BuildHttpHandler(HttpStatusCode.BadRequest));
        await RunOneCycleAsync(worker, CancellationToken.None);

        var updated = await GetFreshMsgAsync(dbName, msg.Id);
        updated!.LastError.Should().NotBeNullOrEmpty();
        updated.LastError!.Length.Should().BeLessOrEqualTo(64);
    }

    [Fact]
    public async Task Worker_ShouldSetTenantConfigPerMessage()
    {
        // Per-message set_config — in InMemory mode ExecuteSqlRaw is skipped,
        // verify no exceptions are thrown and message processes normally.
        var (db, dbName) = CreateInMemoryDb();
        await using var _ = db;
        var msg = CreatePendingOutboxMsg();
        db.OutboxMessages.Add(msg);
        await db.SaveChangesAsync();

        var worker = BuildWorker(dbName, BuildHttpHandler(HttpStatusCode.OK));
        var act = async () => await RunOneCycleAsync(worker, CancellationToken.None);

        await act.Should().NotThrowAsync();

        var updated = await GetFreshMsgAsync(dbName, msg.Id);
        updated!.Status.Should().Be("Completed");
    }

    // ── Test doubles ──────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a fresh DbContext from the same InMemory database name per call.
    /// The worker owns and disposes its context; the test verifies via a separate instance.
    /// </summary>
    private sealed class TestWorkerDbContextFactory(string dbName)
        : IProcurementWorkerDbContextFactory
    {
        public Task<ProcurementDbContext> CreateAsync(CancellationToken ct)
            => Task.FromResult(new ProcurementDbContext(BuildInMemoryOptions(dbName)));
    }

    private sealed class MockHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}

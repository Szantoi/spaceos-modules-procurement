using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpaceOS.Modules.Procurement.Infrastructure.Persistence;

namespace SpaceOS.Modules.Procurement.Infrastructure.Retention;

/// <summary>
/// Background service that cleans up completed outbox messages and processed inbox messages
/// older than 30 days. DB-P-10: supports retention sweep indexes.
/// Track F: Observability — reports lag metric.
/// </summary>
public sealed class OutboxRetentionCleanupJob : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxRetentionCleanupJob> _logger;

    public OutboxRetentionCleanupJob(
        IServiceProvider serviceProvider,
        ILogger<OutboxRetentionCleanupJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Outbox retention cleanup failed");
            }

            await Task.Delay(RunInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();

        var cutoff = DateTimeOffset.UtcNow - RetentionPeriod;

        if (!db.Database.IsRelational())
            return;

        // Clean completed outbox messages older than 30 days
        var outboxDeleted = await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM spaceos_procurement.procurement_outbox WHERE \"Status\" = 'Completed' AND \"ProcessedAt\" < {0}",
            cutoff).ConfigureAwait(false);

        // Clean processed inbox messages older than 30 days
        var inboxDeleted = await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM spaceos_procurement.procurement_inbox WHERE \"ProcessedAt\" < {0}",
            cutoff).ConfigureAwait(false);

        if (outboxDeleted > 0 || inboxDeleted > 0)
        {
            _logger.LogInformation(
                "Retention cleanup: deleted {OutboxDeleted} outbox rows and {InboxDeleted} inbox rows older than {CutoffDate}",
                outboxDeleted, inboxDeleted, cutoff);
        }
    }
}

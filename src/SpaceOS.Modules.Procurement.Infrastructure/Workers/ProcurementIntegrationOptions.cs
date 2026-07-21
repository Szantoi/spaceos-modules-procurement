using System.ComponentModel.DataAnnotations;

namespace SpaceOS.Modules.Procurement.Infrastructure.Workers;

/// <summary>
/// Strongly-typed representation of the <c>ProcurementIntegration</c> configuration section,
/// consumed by <see cref="ProcurementIntegrationWorker"/> to reach the Inventory module's
/// internal inbound-receipt endpoint.
/// Validated at startup via <c>ValidateOnStart()</c> (see
/// <see cref="Extensions.ServiceCollectionExtensions.AddProcurementInfrastructure"/>) so a
/// missing base URL causes an immediate application startup failure rather than a runtime
/// HTTP failure on the first outbox batch.
/// </summary>
/// <remarks>
/// <b>Production requirement:</b> <see cref="InventoryBaseUrl"/> has no built-in default —
/// it must be supplied via configuration (appsettings override, environment variable
/// <c>ProcurementIntegration__InventoryBaseUrl</c>, or user-secrets in Development). This
/// guarantees the shipped default never silently points at <c>localhost</c>/<c>127.0.0.1</c>
/// in a real deployment; a missing value fails fast instead.
/// </remarks>
public sealed class ProcurementIntegrationOptions
{
    /// <summary>Configuration section name: <c>ProcurementIntegration</c>.</summary>
    public const string SectionName = "ProcurementIntegration";

    /// <summary>
    /// Base URL of the Inventory module's internal API (no trailing slash), e.g.
    /// <c>http://spaceos-modules-inventory-api:8080</c>. Required — no default.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string InventoryBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Path of the Inventory module's internal inbound-receipt endpoint. Matches the route
    /// actually mapped by the Inventory module (<c>ProcurementReceiverEndpoints</c>):
    /// <c>/internal/inbound</c> — NOT <c>/inventory/internal/inbound</c>.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string InventoryInboundPath { get; set; } = "/internal/inbound";
}

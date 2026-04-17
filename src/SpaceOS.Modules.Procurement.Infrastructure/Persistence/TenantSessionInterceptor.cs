using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SpaceOS.Modules.Procurement.Infrastructure.Persistence;

internal sealed class TenantSessionInterceptor : DbConnectionInterceptor
{
    private const string PgConfigKey = "app.current_tenant_id";
    private readonly IHttpContextAccessor _http;

    public TenantSessionInterceptor(IHttpContextAccessor http)
        => _http = http;

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            await base.ConnectionOpenedAsync(connection, eventData, ct).ConfigureAwait(false);
            return;
        }

        await SetConfigAsync(connection, PgConfigKey, tenantId, ct).ConfigureAwait(false);
        await base.ConnectionOpenedAsync(connection, eventData, ct).ConfigureAwait(false);
    }

    public override async ValueTask<InterceptionResult> ConnectionClosingAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        var tenantId = ResolveTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
            return await base.ConnectionClosingAsync(connection, eventData, result).ConfigureAwait(false);

        await SetConfigAsync(connection, PgConfigKey, string.Empty, CancellationToken.None).ConfigureAwait(false);
        return await base.ConnectionClosingAsync(connection, eventData, result).ConfigureAwait(false);
    }

    private string? ResolveTenantId()
    {
        var ctx = _http.HttpContext;
        if (ctx is null) return null;
        var claim = ctx.User.FindFirst("tid")?.Value;
        return Guid.TryParse(claim, out var g) && g != Guid.Empty ? g.ToString() : null;
    }

    private static async Task SetConfigAsync(DbConnection conn, string key, string value, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT set_config(@key, @value, false)";
        var pk = cmd.CreateParameter(); pk.ParameterName = "@key";   pk.Value = key;   cmd.Parameters.Add(pk);
        var pv = cmd.CreateParameter(); pv.ParameterName = "@value"; pv.Value = value; cmd.Parameters.Add(pv);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}

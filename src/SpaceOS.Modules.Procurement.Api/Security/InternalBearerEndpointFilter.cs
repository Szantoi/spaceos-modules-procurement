using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace SpaceOS.Modules.Procurement.Api.Security;

/// <summary>
/// Endpoint filter that validates the internal shared-secret Bearer token (SEC-P-01).
/// Uses constant-time comparison to prevent timing attacks.
/// Replaces the old X-SpaceOS-Internal header pattern.
/// </summary>
public sealed class InternalBearerEndpointFilter : IEndpointFilter
{
    private const string BearerPrefix = "Bearer ";
    private const string EnvKey = "SPACEOS_INTERNAL_SECRET";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var secret = Environment.GetEnvironmentVariable(EnvKey);
        if (string.IsNullOrEmpty(secret))
        {
            // No secret configured — reject all internal calls in production
            return Results.Problem(
                detail: "Internal secret not configured.",
                statusCode: 503,
                title: "Service Unavailable");
        }

        var authHeader = context.HttpContext.Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            return Results.Unauthorized();

        var providedToken = authHeader[BearerPrefix.Length..].Trim();

        // SEC-P-01: constant-time comparison
        var providedBytes = Encoding.UTF8.GetBytes(providedToken);
        var expectedBytes = Encoding.UTF8.GetBytes(secret);

        if (!CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
            return Results.Unauthorized();

        return await next(context).ConfigureAwait(false);
    }
}

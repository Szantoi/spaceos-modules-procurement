using Microsoft.AspNetCore.Http;

namespace SpaceOS.Modules.Procurement.Infrastructure.Adapters;

public class HttpContextProcurementTenantAccessor : IProcurementTenantAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextProcurementTenantAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenant_id")?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }
}

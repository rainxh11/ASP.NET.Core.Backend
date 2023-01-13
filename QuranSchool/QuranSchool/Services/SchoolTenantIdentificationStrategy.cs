using Autofac.Multitenant;

namespace QuranSchool.Services;

public class SchoolTenantIdentificationStrategy : ITenantIdentificationStrategy
{
    private readonly IHttpContextAccessor _accessor;

    public SchoolTenantIdentificationStrategy(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public bool TryIdentifyTenant(out object tenantId)
    {
        var clientId = _accessor.HttpContext?.User.Claims.First(x => x.Type == "TenantId").Value;
        tenantId = clientId;

        return clientId is not null;
    }
}
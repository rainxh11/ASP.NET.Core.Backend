using MongoDB.Entities;
using System.Collections.Concurrent;

namespace QuranSchool.Services;

public class ClientContextStore
{
    private readonly IHttpContextAccessor _accessor;

    private readonly ConcurrentDictionary<string, ClientDbContext> _store = new();

    public ClientContextStore(IHttpContextAccessor httpContextAccessor)
    {
        _accessor = httpContextAccessor;
    }

    public ClientDbContext? GetContext()
    {
        var clientId = _accessor.HttpContext?.User.Claims.First(x => x.Type == "TenantId").Value;
        return clientId == null
            ? new DBContext() as ClientDbContext
            : _store.GetOrAdd(clientId, x => new ClientDbContext(x));
    }
}
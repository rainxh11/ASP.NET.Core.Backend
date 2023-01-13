using MongoDB.Entities;
using QuranSchool.Models;

namespace QuranSchool.Services;

public class ClientDbContext : DBContext
{
    public string TenantId { get; }

    public ClientDbContext(string tenantId)
    {
        TenantId = tenantId;
        SetGlobalFilterForBaseClass<ClientEntity>(
            b => b.TenantId == tenantId,
            true);

        SetGlobalFilterForBaseClass<ClientFile>(
            b => b.TenantId == tenantId,
            true);
    }

    protected override Action<T> OnBeforeSave<T>()
    {
        Action<ClientEntity> action = f => { f.TenantId = TenantId; };
        return action as Action<T>;
    }

    protected override Action<UpdateBase<T>> OnBeforeUpdate<T>()
    {
        Action<UpdateBase<ClientEntity>> action = update => { update.AddModification(f => f.TenantId, TenantId); };
        return action as Action<UpdateBase<T>>;
    }
}
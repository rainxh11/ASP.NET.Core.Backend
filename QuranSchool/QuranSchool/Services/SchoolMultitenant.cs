using Autofac;
using Autofac.Multitenant;
using MongoDB.Bson;

namespace QuranSchool.Services;

public class SchoolMultitenant
{
    public static MultitenantContainer ConfigureMultitenantContainer(IContainer container)
    {
        var strategy = new SchoolTenantIdentificationStrategy(container.Resolve<IHttpContextAccessor>());
        var mtContainer = new MultitenantContainer(strategy, container);
        mtContainer.ConfigureTenant(ObjectId.Empty.ToString(),
            cb => cb.RegisterType<ClientDbContext>());
        return mtContainer;
    }
}
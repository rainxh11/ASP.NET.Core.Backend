using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Refit;

namespace QuranApi;

public static class Service
{
    public static IServiceCollection AddQuranService(this IServiceCollection services)
    {
        services
            .TryAddSingleton(x =>
                RestService.For<IAlquranApiClient>("http://api.alquran.cloud"));
        return services;
    }
}
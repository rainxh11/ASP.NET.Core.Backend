namespace QuranSchool.Middleware;

public static partial class MiddlewareExtensions
{
    public static IServiceCollection AddErrorHandler(this IServiceCollection services)
    {
        return services
            .AddSingleton<ErrorHandlingMiddleware>();
    }

    public static IApplicationBuilder UseErrorHandlerMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ErrorHandlingMiddleware>();
    }
}
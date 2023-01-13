using System.Net;

namespace QuranSchool.Middleware;

public class DemoRestrictionMiddleware : IMiddleware
{
    private IConfiguration _config;

    public DemoRestrictionMiddleware(IConfiguration config)
    {
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            if (context.User.Identity?.Name == _config["Google:DemoUser"])
            {
                if (new[] { "GET", "OPTIONS" }.Contains(context.Request.Method))
                {
                    await next(context);
                }
                else
                {
                    if (context.Request.PathBase.StartsWithSegments("/api/v1/account/login"))
                    {
                        await next(context);
                    }
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        await context.Response.StartAsync();
                        return;
                    }
                }
            }

            await next(context);
        }
        catch
        {
            await next(context);
        }
    }
}
namespace QuranSchool.Middleware;

public class TokenFromCookiesMiddleware : IMiddleware
{
    private readonly ILogger<TokenFromCookiesMiddleware> _logger;

    public TokenFromCookiesMiddleware(ILogger<TokenFromCookiesMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path.StartsWithSegments("/backgroundjobs") ||
            context.Request.Path.StartsWithSegments("/api")
           )
            if (context.Request.Cookies.ContainsKey("T") &&
                !context.Request.Headers["Authorization"].Any(x => x.Contains("Bearer")))
                try
                {
                    var token = context.Request.Cookies["T"];
                    context.Request.Headers["Authorization"] = $"Bearer {token}";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }

        await next(context);
    }
}
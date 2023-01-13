using QuranSchool.Services;

namespace QuranSchool.Middleware;

public class TokenInjectionMiddleware : IMiddleware
{
    private readonly IIdentityService _identityService;
    private readonly ILogger<TokenInjectionMiddleware> _logger;

    public TokenInjectionMiddleware(ILogger<TokenInjectionMiddleware> logger, IIdentityService identityService)
    {
        _logger = logger;
        _identityService = identityService;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Query.ContainsKey("token"))
            try
            {
                var token = context.Request.Query["token"];
                context.Request.Headers["Authorization"] = $"Bearer {token}";
                /*var account = await _identityService.GetAccountFromToken(token);
                if (account != null)
                {
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, account.ID),
                        new Claim(ClaimTypes.Role, account.Role.ToString()),
                        new Claim(ClaimTypes.Email, account.UserName),
                        new Claim(ClaimTypes.Hash, account.PasswordHash),
                    };
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
                }*/
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

        await next(context);
    }
}
using MongoDB.Entities;

using QuranSchool.Helpers;
using QuranSchool.Models;
using QuranSchool.Services;

using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

namespace QuranSchool.Middleware;
/*
public class AttachmentBasicAuthMiddleware : IMiddleware
{
    private readonly ILogger<AttachmentBasicAuthMiddleware> _logger;
    private readonly LoginInfoSaver _loginSaver;
    private readonly AuthService _tokenService;
    private readonly BasicAuthenticationHelper _basicAuthHelper;

    public AttachmentBasicAuthMiddleware(ILogger<AttachmentBasicAuthMiddleware> logger,
        BasicAuthenticationHelper basicAuthHelper,
        AuthService tokenService,
        LoginInfoSaver loginSaver)
    {
        _basicAuthHelper = basicAuthHelper;
        _logger = logger;
        _tokenService = tokenService;
        _loginSaver = loginSaver;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Headers["Authorization"].Any(x => x.Contains("Bearer")) &&
            (context.Request.Path.StartsWithSegments("/api/v1/report") ||
             context.Request.Path.StartsWithSegments("/backgroundjobs")))
            try
            {
                var authHeader = AuthenticationHeaderValue.Parse(context.Request.Headers["Authorization"]);
                var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
                var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);
                var username = credentials[0];
                var password = credentials[1];

                var account = await _basicAuthHelper.Login(username, password).ConfigureAwait(false);

                try
                {
                    var accountModel = await DB.Find<Account>()
                        .MatchID(account.Claims.Single(x => x.Type == ClaimTypes.NameIdentifier).Value)
                        .ExecuteSingleAsync();

                    await _loginSaver.AddLogin(context, accountModel);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }


                // authenticate credentials with user service and attach user to http context
                context.User = account;
                var token = await _tokenService.GenerateTokenFromIdentity(account.Identity);
                context.Request.Headers["Authorization"] = $"Bearer {token}";
                context.Response.Cookies.Append("T", token);
                _logger.LogInformation("Account {0} Logged in!", username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                context.Response.StatusCode = 401;
                context.Response.Headers.Add("WWW-Authenticate", "Basic realm=\"\"");
                await context.Response.CompleteAsync();
            }

        await next(context);
    }
}*/
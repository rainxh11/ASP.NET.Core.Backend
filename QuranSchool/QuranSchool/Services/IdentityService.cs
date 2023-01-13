using Microsoft.AspNetCore.SignalR;
using MongoDB.Entities;
using QuranSchool.Models;
using System.Net;
using System.Reactive.Linq;
using System.Security.Claims;

namespace QuranSchool.Services;

public class IdentityService : IIdentityService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(IConfiguration configuration, ILogger<IdentityService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Account?> GetAccountFromToken(string token)
    {
        try
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return null;
        }
    }

    public async Task<Account?> GetCurrentAccount(HttpContext httpContext)
    {
        if (httpContext.User.Identity is ClaimsIdentity identity)
            try
            {
                var accountId = identity.Claims.FirstOrDefault(o => o.Type == ClaimTypes.NameIdentifier)?.Value;
                var username = identity.Claims.FirstOrDefault(o => o.Type == ClaimTypes.Email)?.Value;
                var role = identity.Claims.FirstOrDefault(o => o.Type == ClaimTypes.Role)?.Value;

                var account = await DB.Find<Account>().MatchID(accountId).ExecuteSingleAsync().ConfigureAwait(false);
                return account.Role.ToString() != role || account.UserName != username
                    ? throw new Exception("Account Information changed, Token Invalid! Please re-login.")
                    : account ?? throw new Exception("Account not found! Token Invalid.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);

                httpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                await httpContext.Response.WriteAsJsonAsync(new { ex.Message }).ConfigureAwait(false);
                await httpContext.Response.CompleteAsync().ConfigureAwait(false);
            }

        return null;
    }

    public async Task<Account?> GetCurrentHubClient(HubCallerContext hubContext)
    {
        if (hubContext.User.Identity is ClaimsIdentity identity)
            try
            {
                var accountId = identity.Claims.FirstOrDefault(o => o.Type == ClaimTypes.NameIdentifier)?.Value;
                var username = identity.Claims.FirstOrDefault(o => o.Type == ClaimTypes.Email)?.Value;
                var role = identity.Claims.FirstOrDefault(o => o.Type == ClaimTypes.Role)?.Value;

                var account = await DB.Find<Account>().MatchID(accountId).ExecuteSingleAsync().ConfigureAwait(false);
                return account.Role.ToString() != role || account.UserName != username
                    ? throw new Exception("Account Informations changed, Token Invalid! Please relogin.")
                    : account ?? throw new Exception("Account not found! Token Invalid.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

        return null;
    }
}
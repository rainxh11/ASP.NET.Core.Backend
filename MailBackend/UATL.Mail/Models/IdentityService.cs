using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reactive.Linq;
using System.Security.Claims;
using System.Text;
using Akavache;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Entities;

namespace UATL.MailSystem.Common;

public class IdentityService : IIdentityService
{
    private readonly ILogger<IdentityService> _logger;
    private readonly IConfiguration _configuration;

    public IdentityService(IConfiguration configuration, ILogger<IdentityService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Account?> GetAccountFromToken(string token)
    {
        try
        {
            var cached = await BlobCache.InMemory.GetObject<Account>(token);

            return cached;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return null;
        }
    }

    public async Task<Account?> GetCurrentAccount(HttpContext httpContext)
    {
        var token = httpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
        try
        {
            var cached = await BlobCache.LocalMachine.GetObject<Account>(token);

            if (cached != null)
                return cached;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }

        var identity = httpContext.User.Identity as ClaimsIdentity;

        if (identity != null)
            try
            {
                var accountId = identity.Claims.FirstOrDefault(o => o.Type == ClaimTypes.NameIdentifier)?.Value;
                var username = identity.Claims.FirstOrDefault(o => o.Type == ClaimTypes.Email)?.Value;
                var hash = identity.Claims.FirstOrDefault(o => o.Type == ClaimTypes.Hash)?.Value;
                var role = identity.Claims.FirstOrDefault(o => o.Type == ClaimTypes.Role)?.Value;

                var account = await DB.Find<Account>().MatchID(accountId).ExecuteSingleAsync().ConfigureAwait(false);
                if (account.PasswordHash != hash || account.Role.ToString() != role || account.UserName != username)
                    throw new Exception("Account Informations changed, Token Invalid! Please relogin.");

                if (account != null)
                    return account;
                throw new Exception("Account not found! Token Invalid.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);

                httpContext.Response.StatusCode = (int) HttpStatusCode.Unauthorized;
                await httpContext.Response.WriteAsJsonAsync(new {ex.Message}).ConfigureAwait(false);
                await httpContext.Response.CompleteAsync().ConfigureAwait(false);
            }

        return null;
    }

    public async Task<Account?> GetCurrentHubClient(HubCallerContext hubContext)
    {
        var identity = hubContext.User.Identity as ClaimsIdentity;

        if (identity != null)
            try
            {
                var accountId = identity.Claims.FirstOrDefault(o => o.Type == ClaimTypes.NameIdentifier)?.Value;
                var username = identity.Claims.FirstOrDefault(o => o.Type == ClaimTypes.Email)?.Value;
                var hash = identity.Claims.FirstOrDefault(o => o.Type == ClaimTypes.Hash)?.Value;
                var role = identity.Claims.FirstOrDefault(o => o.Type == ClaimTypes.Role)?.Value;

                var account = await DB.Find<Account>().MatchID(accountId).ExecuteSingleAsync().ConfigureAwait(false);
                if (account.PasswordHash != hash || account.Role.ToString() != role || account.UserName != username)
                    throw new Exception("Account Informations changed, Token Invalid! Please relogin.");

                if (account != null)
                    return account;
                throw new Exception("Account not found! Token Invalid.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

        return null;
    }

    private JwtSecurityToken ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var secret = _configuration["Jwt:Key"];
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

            var claimPrincipal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireExpirationTime = true,
                IssuerSigningKey = securityKey,
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);

            var jwtToken = (JwtSecurityToken) validatedToken;
            return jwtToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return null;
        }
    }
}
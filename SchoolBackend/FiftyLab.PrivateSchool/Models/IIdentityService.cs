using Microsoft.AspNetCore.SignalR;

namespace FiftyLab.PrivateSchool;

public interface IIdentityService
{
    Task<Account?> GetAccountFromToken(string token);
    Task<Account?> GetCurrentAccount(HttpContext httpContext);
    Task<Account?> GetCurrentHubClient(HubCallerContext httpContext);
}
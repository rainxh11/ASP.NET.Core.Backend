using Microsoft.AspNetCore.SignalR;
using QuranSchool.Models;

namespace QuranSchool.Services;

public interface IIdentityService
{
    Task<Account?> GetAccountFromToken(string token);
    Task<Account?> GetCurrentAccount(HttpContext httpContext);
    Task<Account?> GetCurrentHubClient(HubCallerContext httpContext);
}
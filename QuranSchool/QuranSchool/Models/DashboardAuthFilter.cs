using Hangfire.Annotations;
using Hangfire.Dashboard;
using System.Reactive.Linq;
using System.Security.Claims;

namespace QuranSchool.Models;

public class DashboardAuthFilter : IDashboardAsyncAuthorizationFilter
{
    public async Task<bool> AuthorizeAsync([NotNull] DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext.User.IsInRole("Admin")) return true;

        if (httpContext.Request.Cookies.Any(x => x.Key == "T"))
        {
            var token = httpContext.Request.Cookies["T"];
            var account = await GetAccountFromToken(token);
            return account != null && account.Role == AccountType.Admin;
        }


        if (httpContext.User.Identity is ClaimsIdentity identity)
        {
            var role = identity.Claims.FirstOrDefault(o => o.Type == ClaimTypes.Role)?.Value;

            return role == AccountRole.Admin;
        }

        return false;
    }

    private async Task<Account?> GetAccountFromToken(string token)
    {
        try
        {
            return null;
        }
        catch (Exception ex)
        {
            return null;
        }
    }
}
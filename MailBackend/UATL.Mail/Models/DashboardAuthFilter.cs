using System.Reactive.Linq;
using System.Security.Claims;
using Akavache;
using Hangfire.Annotations;
using Hangfire.Dashboard;
using UATL.MailSystem.Common;

namespace UATL.Mail.Models;

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
            if (account == null)
                return false;
            return account.Role == AccountType.Admin;
        }

        var identity = httpContext.User.Identity as ClaimsIdentity;

        if (identity != null)
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
            var cached = await BlobCache.InMemory.GetObject<Account>(token);

            return cached;
        }
        catch (Exception ex)
        {
            return null;
        }
    }
}
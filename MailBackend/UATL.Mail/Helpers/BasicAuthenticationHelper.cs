using System.Linq.Expressions;
using System.Security.Claims;
using MongoDB.Entities;
using UATL.MailSystem.Common;

namespace UATL.Mail.Helpers;

public class BasicAuthenticationHelper
{
    public static async Task<ClaimsPrincipal> Login(string username, string password)
    {
        var account = await Authenticate(x => x.UserName == username);
        if (account == null)
            return null;
        if (!VerifyPassword(account, password))
            return null;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.ID),
            new Claim(ClaimTypes.Role, account.Role.ToString()),
            new Claim(ClaimTypes.Email, account.UserName),
            new Claim(ClaimTypes.Hash, account.PasswordHash)
        };
        var identity = new ClaimsIdentity(claims);
        return new ClaimsPrincipal(identity);
    }

    private static bool VerifyPassword(Account account, string password)
    {
        return account.Verify(password);
    }

    //--------------------------------------------------------------------------------------------------------------//
    private static async Task<Account?> Authenticate(Expression<Func<Account, bool>> predicate)
    {
        return await DB.Find<Account>().Match(predicate).ExecuteFirstAsync();
    }
}
using Microsoft.AspNetCore.Identity;
using MongoDB.Entities;
using QuranSchool.Models;
using System.Linq.Expressions;
using System.Security.Claims;

namespace QuranSchool.Helpers;

public class BasicAuthenticationHelper
{
    private readonly UserManager<Account> _userManager;

    public BasicAuthenticationHelper(IServiceScope scope)
    {
        _userManager = scope.ServiceProvider.GetService<UserManager<Account>>();
    }

    public async Task<ClaimsPrincipal> Login(string username, string password)
    {
        var account = await Authenticate(x => x.UserName == username);
        if (account == null)
            return null;

        var verify = await _userManager.CheckPasswordAsync(account, password);
        if (!verify)
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


    //--------------------------------------------------------------------------------------------------------------//
    private async Task<Account?> Authenticate(Expression<Func<Account, bool>> predicate)
    {
        return await DB.Find<Account>().Match(predicate).ExecuteFirstAsync();
    }
}
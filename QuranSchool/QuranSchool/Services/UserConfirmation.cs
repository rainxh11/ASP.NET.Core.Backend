using Microsoft.AspNetCore.Identity;
using QuranSchool.Models;

namespace QuranSchool.Services;

public class UserConfirmation : IUserConfirmation<Account>
{
    public Task<bool> IsConfirmedAsync(UserManager<Account> manager, Account user)
    {
        return Task.FromResult(user.Enabled && user.EmailConfirmed);
    }
}
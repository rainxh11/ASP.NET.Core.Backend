using Microsoft.AspNetCore.Identity;
using MongoDB.Entities;
using QuranSchool.Models;
using SmtpServer;
using SmtpServer.Authentication;

namespace QuranSchool.Email;

public class EmailUserAuthenticator : IUserAuthenticator
{
    private readonly UserManager<Account> _userManager;
    private readonly ILogger<EmailUserAuthenticator> _logger;
    private readonly IConfiguration _config;

    public EmailUserAuthenticator(IServiceScopeFactory scopeFactory,
        ILogger<EmailUserAuthenticator> logger,
        IConfiguration config)
    {
        _config = config;
        _logger = logger;
        _userManager = scopeFactory.CreateScope().ServiceProvider.GetService<UserManager<Account>>()!;
    }

    public async Task<bool> AuthenticateAsync(ISessionContext context,
        string user, string password,
        CancellationToken token)
    {
        try
        {
            if (user == "admin" || user == _config["MailerSend:Email"])
            {
                if (password == _config["DefaultAdminPassword"]) return true;

                var admin = await DB.Find<Account>()
                    .Match(x => x.UserName == "admin")
                    .ExecuteSingleAsync(token);
                return await _userManager.CheckPasswordAsync(admin, password);
            }

            var account = await DB.Find<Account>()
                .Match(x => x.UserName == user && x.Enabled)
                .ExecuteFirstAsync(token);
            return await _userManager.CheckPasswordAsync(account, password);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex.Message);
            return false;
        }
    }
}
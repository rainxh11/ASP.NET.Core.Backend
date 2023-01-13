using FluentEmail.Core;
using Microsoft.AspNetCore.Identity;
using QuranSchool.Models;
using System.Reflection;

namespace QuranSchool.Email;

public class EmailSender
{
    private readonly UserManager<Account> _userManager;
    private readonly ILogger<EmailSender> _logger;
    private readonly IFluentEmail _mailSender;

    public EmailSender(IFluentEmail mailSender,
        ILogger<EmailSender> logger,
        UserManager<Account> userManager)
    {
        _mailSender = mailSender;
        _logger = logger;
        _userManager = userManager;
    }

    public async Task SendEmailConfirmation(string email)
    {
        var account = await _userManager.FindByEmailAsync(email);
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(account);
        var model = new EmailConfirmModel()
        {
            Email = email,
            Token = token
        };

        var mail = _mailSender
            .To(email)
            .Subject("Quran School Email Confirmation")
            .UsingTemplateFromEmbedded("QuranSchool.EmailConfirm.cshtml",
                model,
                model.GetType().GetTypeInfo().Assembly);
        _logger?.LogInformation("Sending Email Confirmation to: {0}, {1}", account.Name, email);
        await mail.SendAsync();
    }

    public async Task SendPasswordReset(string email)
    {
        var account = await _userManager.FindByEmailAsync(email);
        var token = await _userManager.GeneratePasswordResetTokenAsync(account);
        var model = new PasswordResetModel()
        {
            Email = email,
            Token = token,
            Name = account.Name
        };

        var mail = _mailSender
            .To(email)
            .Subject("Quran School Password Reset")
            .UsingTemplateFromEmbedded("QuranSchool.PasswordReset.cshtml",
                model,
                model.GetType().GetTypeInfo().Assembly);

        _logger?.LogInformation("Sending Password Reset to: {0}, {1}", account.Name, email);
        await mail.SendAsync();
    }
}
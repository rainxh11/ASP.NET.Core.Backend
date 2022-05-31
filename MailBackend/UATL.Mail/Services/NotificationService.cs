using FluentEmail.Core;
using Microsoft.AspNetCore.SignalR;
using UATL.Mail.Hubs;

namespace UATL.Mail.Services;

public class NotificationService
{
    private readonly ILogger<MailHub> _logger;
    private readonly IFluentEmail _fluentMail;
    private readonly IHubContext<MailHub> _mailHub;

    public NotificationService(IHubContext<MailHub> mailHub, ILogger<MailHub> logger, IFluentEmail fluentMail)
    {
        _mailHub = mailHub;
        _logger = logger;
        _fluentMail = fluentMail;
    }

    public async Task SendAll(string message)
    {
        try
        {
            await _mailHub.Clients.All.SendAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }
    }

    public async Task Send(string userId, string message)
    {
        try
        {
            await _mailHub.Clients.User(userId).SendAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }
    }

    public async Task SendEmail(string to, string subject, string message)
    {
        await _fluentMail
            .To(to)
            .Subject(subject)
            .SendAsync();
    }
}
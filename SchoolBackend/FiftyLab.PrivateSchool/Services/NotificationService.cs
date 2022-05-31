using FiftyLab.PrivateSchool.Hubs;
using FluentEmail.Core;
using Microsoft.AspNetCore.SignalR;

namespace FiftyLab.PrivateSchool.Services;

public class NotificationService
{
    private readonly ILogger<PrivateSchoolHub> _logger;
    private readonly IFluentEmail _fluentMail;
    private readonly IHubContext<PrivateSchoolHub> _mailHub;

    public NotificationService(IHubContext<PrivateSchoolHub> mailHub, ILogger<PrivateSchoolHub> logger, IFluentEmail fluentMail)
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
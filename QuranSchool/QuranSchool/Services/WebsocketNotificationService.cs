using Microsoft.AspNetCore.SignalR;
using QuranSchool.Hubs;

namespace QuranSchool.Services;

public class WebsocketNotificationService
{
    private readonly ILogger<PrivateSchoolHub> _logger;
    private readonly IHubContext<PrivateSchoolHub> _mailHub;

    public WebsocketNotificationService(IHubContext<PrivateSchoolHub> mailHub, ILogger<PrivateSchoolHub> logger)
    {
        _mailHub = mailHub;
        _logger = logger;
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
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace QuranSchool.Hubs;

[Authorize]
public class PrivateSchoolHub : Hub
{
    private readonly ILogger<PrivateSchoolHub> _logger;

    public PrivateSchoolHub(ILogger<PrivateSchoolHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        var user = Context.User;

        _logger.LogInformation("Hub ClientEntity: {@userId} Connected.", userId);
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
        await base.OnDisconnectedAsync(exception);
    }
}
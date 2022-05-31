using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UATL.MailSystem.Common;

namespace UATL.Mail.Hubs;

[Authorize]
public class MailHub : Hub
{
    private readonly ILogger<MailHub> _logger;

    public MailHub(ILogger<MailHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        var user = Context.User;

        _logger.LogInformation("Hub Client: {@userId} Connected.", userId);
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

[Authorize(Roles = $"{AccountRole.Admin},{AccountRole.User}")]
public class ChatHub : Hub
{
}
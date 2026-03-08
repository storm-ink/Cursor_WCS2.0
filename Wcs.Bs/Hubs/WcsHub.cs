using Microsoft.AspNetCore.SignalR;

namespace Wcs.Bs.Hubs;

public class WcsHub : Hub
{
    private readonly ILogger<WcsHub> _logger;

    public WcsHub(ILogger<WcsHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogDebug("[SignalR] {ConnId} joined group {Group}", Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogDebug("[SignalR] {ConnId} left group {Group}", Context.ConnectionId, groupName);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("[SignalR] Client connected: {ConnId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("[SignalR] Client disconnected: {ConnId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

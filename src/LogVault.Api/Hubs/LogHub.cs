using LogVault.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LogVault.Api.Hubs;

[Authorize(Policy = "RequireUser")]
public class LogHub : Hub
{
    private readonly ILogHubNotifier _notifier;

    public LogHub(ILogHubNotifier notifier) => _notifier = notifier;

    public Task SetFilter(LogHubFilter filter)
    {
        _notifier.RegisterFilter(Context.ConnectionId, filter);
        return Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _notifier.UnregisterFilter(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

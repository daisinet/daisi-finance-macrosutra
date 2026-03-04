using Microsoft.AspNetCore.SignalR;

namespace MacroSutra.Web.Hubs;

/// <summary>
/// SignalR hub for real-time strategy alerts and portfolio updates.
/// Clients join an account group to receive events scoped to their account.
/// </summary>
public class MacroSutraHub : Hub
{
    /// <summary>
    /// Adds the caller to the group for the specified account.
    /// </summary>
    public async Task JoinAccount(string accountId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, accountId);
    }
}

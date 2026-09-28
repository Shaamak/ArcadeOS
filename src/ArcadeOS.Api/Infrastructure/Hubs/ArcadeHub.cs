using Microsoft.AspNetCore.SignalR;

namespace ArcadeOS.Api.Infrastructure.Hubs;

/// <summary>
/// Strongly-typed interface for clients listening to ArcadeOS telemetry events.
/// </summary>
public interface IArcadeClient
{
    Task MachineStatusChanged(Guid machineId, string machineName, string status);
    Task GameplayOccurred(Guid machineId, string machineName, Guid cardId, decimal creditsSpent, int ticketsEarned);
    Task HighValueRedemption(string customerName, string rewardTitle, int ticketsSpent);
}

/// <summary>
/// SignalR Hub for real-time Arcade telemetry and operator alerts.
/// </summary>
public class ArcadeHub : Hub<IArcadeClient>
{
    public async Task JoinOperatorGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Operators");
    }

    public async Task LeaveOperatorGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Operators");
    }
}

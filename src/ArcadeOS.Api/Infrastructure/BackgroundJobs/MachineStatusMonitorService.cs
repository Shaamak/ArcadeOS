using ArcadeOS.Api.Application.Interfaces;

namespace ArcadeOS.Api.Infrastructure.BackgroundJobs;

/// <summary>
/// A hosted service that runs continuously in the background of the ASP.NET Core process.
/// 
/// WHY DO WE NEED THIS?
/// Machines send heartbeats saying "I'm Online!". But if a machine's power cord is pulled,
/// it can't send an "I'm Offline!" message. We need a background watcher to notice the silence
/// and flip the status so the staff dashboard reflects reality.
/// </summary>
public class MachineStatusMonitorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MachineStatusMonitorService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _offlineTimeout = TimeSpan.FromMinutes(5);

    // We inject IServiceProvider instead of IMachineService directly because BackgroundServices
    // are singletons, and IMachineService is scoped. We must create a scope manually per run.
    public MachineStatusMonitorService(IServiceProvider serviceProvider, ILogger<MachineStatusMonitorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Machine Status Monitor background service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Create a new DI scope for the scoped services
                using var scope = _serviceProvider.CreateScope();
                var machineService = scope.ServiceProvider.GetRequiredService<IMachineService>();

                // Any machine that hasn't pinged us in the last 5 minutes gets marked offline
                await machineService.MarkStaleMachinesOfflineAsync(_offlineTimeout, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while monitoring machine statuses.");
            }

            // Sleep until the next interval
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Machine Status Monitor background service is stopping.");
    }
}

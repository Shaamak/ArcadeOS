using ArcadeOS.Api.Application.Interfaces;

namespace ArcadeOS.Api.Infrastructure.BackgroundJobs;

/// <summary>
/// Background service that periodically checks for expired customer memberships.
/// Scoped resolution pattern is used since BackgroundService is a Singleton.
/// </summary>
public class MembershipExpirationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MembershipExpirationService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public MembershipExpirationService(IServiceProvider serviceProvider, ILogger<MembershipExpirationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Membership Expiration background service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var membershipService = scope.ServiceProvider.GetRequiredService<IMembershipService>();

                await membershipService.ProcessExpiredMembershipsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing expired memberships.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Membership Expiration background service is stopping.");
    }
}

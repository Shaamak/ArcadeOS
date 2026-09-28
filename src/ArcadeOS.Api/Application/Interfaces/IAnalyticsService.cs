using ArcadeOS.Api.Application.DTOs;

namespace ArcadeOS.Api.Application.Interfaces;

public interface IAnalyticsService
{
    Task<DashboardSummaryDto> GetDashboardSummaryAsync();
    Task<IEnumerable<MachinePerformanceDto>> GetMachinePerformanceAsync();
    Task<IEnumerable<HourlyActivityDto>> GetHourlyActivityAsync(DateTime date);
    Task<IEnumerable<MembershipDistributionDto>> GetMembershipDistributionAsync();
}

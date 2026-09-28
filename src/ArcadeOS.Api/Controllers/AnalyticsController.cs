using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArcadeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Operator")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary()
    {
        var result = await _analyticsService.GetDashboardSummaryAsync();
        return Ok(result);
    }

    [HttpGet("machines")]
    public async Task<ActionResult<IEnumerable<MachinePerformanceDto>>> GetMachinePerformance()
    {
        var result = await _analyticsService.GetMachinePerformanceAsync();
        return Ok(result);
    }

    [HttpGet("hourly")]
    public async Task<ActionResult<IEnumerable<HourlyActivityDto>>> GetHourlyActivity([FromQuery] DateTime? date)
    {
        var targetDate = date ?? DateTime.UtcNow;
        var result = await _analyticsService.GetHourlyActivityAsync(targetDate);
        return Ok(result);
    }

    [HttpGet("memberships")]
    public async Task<ActionResult<IEnumerable<MembershipDistributionDto>>> GetMembershipDistribution()
    {
        var result = await _analyticsService.GetMembershipDistributionAsync();
        return Ok(result);
    }
}

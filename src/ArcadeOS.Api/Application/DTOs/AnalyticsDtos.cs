namespace ArcadeOS.Api.Application.DTOs;

public record DashboardSummaryDto(
    decimal TotalRevenue,
    decimal TotalCreditsSpent,
    int TotalActiveCards,
    int TotalMachines,
    int OnlineMachines,
    int TotalTicketsIssued,
    int TotalRewardsRedeemed
);

public record MachinePerformanceDto(
    Guid MachineId,
    string Name,
    string Category,
    decimal TotalRevenue,
    int TotalPlays,
    int TotalTicketsIssued,
    decimal PayoutPercentage
);

public record HourlyActivityDto(
    int HourOfDay,
    int TotalSessions,
    decimal TotalCreditsSpent
);

public record MembershipDistributionDto(
    string PlanName,
    int MemberCount,
    decimal Percentage
);

using ArcadeOS.Api.Domain.Enums;

namespace ArcadeOS.Api.Application.DTOs;

public record MembershipPlanDto(
    Guid Id,
    string Name,
    string Description,
    decimal MonthlyFee,
    int DailyBonusTickets,
    decimal GameplayDiscountPercent,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateMembershipPlanDto(
    string Name,
    string Description,
    decimal MonthlyFee,
    int DailyBonusTickets,
    decimal GameplayDiscountPercent
);

public record MembershipDto(
    Guid Id,
    Guid CustomerId,
    Guid PlanId,
    string PlanName,
    MembershipStatus Status,
    DateTime StartDate,
    DateTime EndDate,
    bool AutoRenew,
    DateTime? LastBonusClaimedAt,
    DateTime CreatedAt
);

public record EnrollMembershipDto(
    Guid CustomerId,
    Guid PlanId,
    bool AutoRenew = true
);

public record ClaimDailyBonusResponseDto(
    Guid CustomerId,
    int TicketsClaimed,
    int NewTicketBalance,
    DateTime ClaimedAtUtc
);

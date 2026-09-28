using ArcadeOS.Api.Domain.Enums;

namespace ArcadeOS.Api.Application.DTOs;

public record RewardItemDto(
    Guid Id,
    string Name,
    string Description,
    int TicketCost,
    int StockQuantity,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateRewardItemDto(
    string Name,
    string Description,
    int TicketCost,
    int StockQuantity
);

public record UpdateRewardItemDto(
    string Name,
    string Description,
    int TicketCost,
    int StockQuantity,
    bool IsActive
);

public record RedeemRewardDto(
    Guid CustomerId,
    Guid RewardItemId,
    string? ReferenceId = null
);

public record RedemptionDto(
    Guid Id,
    Guid CustomerId,
    Guid RewardItemId,
    string RewardItemName,
    int TicketsSpent,
    RedemptionStatus Status,
    string? ReferenceId,
    DateTime RedeemedAtUtc,
    DateTime? ClaimedAtUtc
);

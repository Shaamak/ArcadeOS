using ArcadeOS.Api.Application.DTOs;

namespace ArcadeOS.Api.Application.Interfaces;

public interface IRewardService
{
    /// <summary>Creates a new reward item in inventory (Admin only).</summary>
    Task<RewardItemDto> CreateRewardAsync(CreateRewardItemDto dto, CancellationToken ct = default);

    /// <summary>Updates an existing reward item (Admin only).</summary>
    Task<RewardItemDto> UpdateRewardAsync(Guid id, UpdateRewardItemDto dto, CancellationToken ct = default);

    /// <summary>Gets all active reward items available for ticket redemption.</summary>
    Task<IEnumerable<RewardItemDto>> GetAvailableRewardsAsync(CancellationToken ct = default);

    /// <summary>
    /// Atomically redeems a reward: checks stock, verifies customer ticket balance,
    /// decrements tickets, decrements inventory stock with Optimistic Concurrency, and records redemption.
    /// </summary>
    Task<RedemptionDto> RedeemRewardAsync(RedeemRewardDto dto, CancellationToken ct = default);

    /// <summary>Marks a pending redemption as physically claimed at counter (Staff/Admin).</summary>
    Task<RedemptionDto> ClaimRedemptionAsync(Guid redemptionId, CancellationToken ct = default);

    /// <summary>Gets redemption history for a customer.</summary>
    Task<IEnumerable<RedemptionDto>> GetCustomerRedemptionsAsync(Guid customerId, CancellationToken ct = default);
}

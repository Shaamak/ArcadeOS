using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArcadeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RewardsController : ControllerBase
{
    private readonly IRewardService _rewardService;

    public RewardsController(IRewardService rewardService)
    {
        _rewardService = rewardService;
    }

    // ─────────────────────────── INVENTORY ───────────────────────────

    [HttpGet]
    [AllowAnonymous] // Any customer can browse the rewards catalog
    public async Task<ActionResult<IEnumerable<RewardItemDto>>> GetAvailableRewards(CancellationToken ct = default)
    {
        var rewards = await _rewardService.GetAvailableRewardsAsync(ct);
        return Ok(rewards);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RewardItemDto>> CreateReward(
        [FromBody] CreateRewardItemDto dto, CancellationToken ct = default)
    {
        var item = await _rewardService.CreateRewardAsync(dto, ct);
        return Created($"api/rewards/{item.Id}", item);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RewardItemDto>> UpdateReward(
        Guid id, [FromBody] UpdateRewardItemDto dto, CancellationToken ct = default)
    {
        var updated = await _rewardService.UpdateRewardAsync(id, dto, ct);
        return Ok(updated);
    }

    // ─────────────────────────── REDEMPTIONS ───────────────────────────

    /// <summary>
    /// Atomically redeems a reward for a customer:
    /// 1. Check stock
    /// 2. Deduct tickets from wallet (with concurrency)
    /// 3. Decrement stock (with concurrency)
    /// 4. Create redemption record (Pending status)
    /// </summary>
    [HttpPost("redeem")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<RedemptionDto>> RedeemReward(
        [FromBody] RedeemRewardDto dto, CancellationToken ct = default)
    {
        var redemption = await _rewardService.RedeemRewardAsync(dto, ct);
        return Created($"api/rewards/redemptions/{redemption.Id}", redemption);
    }

    /// <summary>
    /// Staff marks a Pending redemption as Claimed when the customer picks up the physical prize.
    /// </summary>
    [HttpPost("redemptions/{redemptionId:guid}/claim")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<RedemptionDto>> ClaimRedemption(
        Guid redemptionId, CancellationToken ct = default)
    {
        var redemption = await _rewardService.ClaimRedemptionAsync(redemptionId, ct);
        return Ok(redemption);
    }

    /// <summary>
    /// Gets the full redemption history for a customer.
    /// </summary>
    [HttpGet("customers/{customerId:guid}/redemptions")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<IEnumerable<RedemptionDto>>> GetCustomerRedemptions(
        Guid customerId, CancellationToken ct = default)
    {
        var redemptions = await _rewardService.GetCustomerRedemptionsAsync(customerId, ct);
        return Ok(redemptions);
    }
}

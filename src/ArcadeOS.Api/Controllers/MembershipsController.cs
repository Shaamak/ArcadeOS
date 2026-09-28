using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArcadeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MembershipsController : ControllerBase
{
    private readonly IMembershipService _membershipService;

    public MembershipsController(IMembershipService membershipService)
    {
        _membershipService = membershipService;
    }

    // ─────────────────────────── PLANS ───────────────────────────

    [HttpGet("plans")]
    [AllowAnonymous] // Customers browsing plans don't need to be logged in
    public async Task<ActionResult<IEnumerable<MembershipPlanDto>>> GetActivePlans(CancellationToken ct = default)
    {
        var plans = await _membershipService.GetActivePlansAsync(ct);
        return Ok(plans);
    }

    [HttpPost("plans")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<MembershipPlanDto>> CreatePlan(
        [FromBody] CreateMembershipPlanDto dto, CancellationToken ct = default)
    {
        var plan = await _membershipService.CreatePlanAsync(dto, ct);
        return Created($"api/memberships/plans/{plan.Id}", plan);
    }

    // ─────────────────────────── SUBSCRIPTIONS ───────────────────────────

    /// <summary>
    /// Admin/Staff enrolls a customer into a plan.
    /// </summary>
    [HttpPost("enroll")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<MembershipDto>> Enroll(
        [FromBody] EnrollMembershipDto dto, CancellationToken ct = default)
    {
        var membership = await _membershipService.EnrollAsync(dto, ct);
        return Created($"api/memberships/customers/{dto.CustomerId}", membership);
    }

    /// <summary>
    /// Gets the active membership for a specific customer.
    /// </summary>
    [HttpGet("customers/{customerId:guid}")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<MembershipDto>> GetCustomerMembership(
        Guid customerId, CancellationToken ct = default)
    {
        var membership = await _membershipService.GetCustomerMembershipAsync(customerId, ct);
        if (membership is null)
            return NotFound(new { error = new { code = "NOT_FOUND", message = "No active membership found for this customer." } });

        return Ok(membership);
    }

    /// <summary>
    /// Customer claims their daily bonus tickets from their active membership.
    /// Can only be called once every 24 hours — rate limited in the service layer.
    /// </summary>
    [HttpPost("customers/{customerId:guid}/claim-daily-bonus")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<ClaimDailyBonusResponseDto>> ClaimDailyBonus(
        Guid customerId, CancellationToken ct = default)
    {
        var result = await _membershipService.ClaimDailyBonusAsync(customerId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Cancels a customer's active membership immediately.
    /// </summary>
    [HttpDelete("customers/{customerId:guid}")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> CancelMembership(
        Guid customerId, CancellationToken ct = default)
    {
        await _membershipService.CancelMembershipAsync(customerId, ct);
        return NoContent();
    }
}

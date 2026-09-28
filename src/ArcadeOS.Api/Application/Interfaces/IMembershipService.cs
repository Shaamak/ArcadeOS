using ArcadeOS.Api.Application.DTOs;

namespace ArcadeOS.Api.Application.Interfaces;

public interface IMembershipService
{
    /// <summary>Creates a new membership plan (Admin only).</summary>
    Task<MembershipPlanDto> CreatePlanAsync(CreateMembershipPlanDto dto, CancellationToken ct = default);

    /// <summary>Lists all active membership plans available for subscription.</summary>
    Task<IEnumerable<MembershipPlanDto>> GetActivePlansAsync(CancellationToken ct = default);

    /// <summary>Enrolls a customer into a membership plan.</summary>
    Task<MembershipDto> EnrollAsync(EnrollMembershipDto dto, CancellationToken ct = default);

    /// <summary>Gets active membership for a customer, or null if none.</summary>
    Task<MembershipDto?> GetCustomerMembershipAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>
    /// Claims daily bonus tickets for an active membership once every 24 hours.
    /// </summary>
    Task<ClaimDailyBonusResponseDto> ClaimDailyBonusAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>Cancels a customer's active membership.</summary>
    Task CancelMembershipAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>Background job process to set expired memberships to Expired status.</summary>
    Task ProcessExpiredMembershipsAsync(CancellationToken ct = default);
}

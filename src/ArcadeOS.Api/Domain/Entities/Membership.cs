using ArcadeOS.Api.Domain.Enums;

namespace ArcadeOS.Api.Domain.Entities;

/// <summary>
/// Active or past subscription belonging to a customer.
/// Controls daily ticket claims and discount eligibility.
/// </summary>
public class Membership
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public Guid PlanId { get; set; }

    public MembershipStatus Status { get; set; } = MembershipStatus.Active;

    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public DateTime EndDate { get; set; }

    public bool AutoRenew { get; set; } = true;

    // Prevents double-claiming daily bonus tickets within 24 hours
    public DateTime? LastBonusClaimedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Customer Customer { get; set; } = null!;

    public MembershipPlan Plan { get; set; } = null!;
}

using ArcadeOS.Api.Domain.Enums;

namespace ArcadeOS.Api.Domain.Entities;

/// <summary>
/// Record of a customer exchanging earned tickets for a physical RewardItem.
/// </summary>
public class Redemption
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public Guid RewardItemId { get; set; }

    public int TicketsSpent { get; set; }

    public RedemptionStatus Status { get; set; } = RedemptionStatus.Pending;

    // Unique reference ID provided by client for idempotency
    public string? ReferenceId { get; set; }

    public DateTime RedeemedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ClaimedAtUtc { get; set; }

    // Navigation properties
    public Customer Customer { get; set; } = null!;

    public RewardItem RewardItem { get; set; } = null!;
}

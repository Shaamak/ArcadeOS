namespace ArcadeOS.Api.Domain.Enums;

/// <summary>
/// Status of a reward item redemption transaction.
/// </summary>
public enum RedemptionStatus
{
    Pending,   // Redeemed digitally, waiting for physical pick up at counter
    Claimed,   // Picked up by customer at redemption counter
    Cancelled  // Cancelled/refunded by staff
}

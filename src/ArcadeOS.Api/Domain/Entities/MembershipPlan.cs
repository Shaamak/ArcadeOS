namespace ArcadeOS.Api.Domain.Entities;

/// <summary>
/// Defines a subscription plan (e.g. VIP Pass, Gold Tier, Silver Tier).
/// Describes benefits like daily bonus tickets, gameplay discounts, and fee.
/// </summary>
public class MembershipPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    // Monthly subscription fee
    public decimal MonthlyFee { get; set; }

    // Tickets given automatically every 24 hours when claimed
    public int DailyBonusTickets { get; set; }

    // Percentage discount on machine play costs (0% to 100%)
    public decimal GameplayDiscountPercent { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

namespace ArcadeOS.Api.Domain.Entities;

/// <summary>
/// Physical prize/reward item available at the arcade redemption counter.
/// E.g. Plushies, PS5, Gaming Mouse, Keychains.
/// 
/// CONCURRENCY (RowVersion):
/// Uses EF Core's optimistic concurrency token (xmin system column in PostgreSQL)
/// to ensure two simultaneous redemption requests never decrement limited stock below zero.
/// </summary>
public class RewardItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int TicketCost { get; set; }

    public int StockQuantity { get; set; }

    public bool IsActive { get; set; } = true;

    // Optimistic concurrency token mapped to Postgres xmin
    public uint RowVersion { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

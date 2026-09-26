using ArcadeOS.Api.Domain.Enums;

namespace ArcadeOS.Api.Domain.Entities;

/// <summary>
/// A time-series log of machine statuses.
/// 
/// WHY TIME-SERIES?
/// Instead of just updating a "LastPing" column on the Machine table, we log every ping.
/// This allows us to answer questions like: "What percentage of the day was this machine broken?"
/// or "How many plays happened between 2 PM and 3 PM?"
/// 
/// We could store a JSONB payload here to capture specific hardware stats (e.g., temperature, 
/// coin jam sensors) without needing to change our DB schema every time a new machine type is added.
/// </summary>
public class MachineHeartbeat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid MachineId { get; set; }
    
    // Status reported by the machine itself
    public MachineStatus Status { get; set; }
    
    // How many credits does the physical coin hopper / card reader hold?
    public decimal? CreditBalance { get; set; }
    
    // Lifetime number of times played according to the machine's internal memory
    public int? PlayCount { get; set; }
    
    // Optional error code if the machine is jammed/broken
    public string? ErrorCode { get; set; }
    
    // In PostgreSQL, this would map to a JSONB column to store flexible diagnostic data
    public string? PayloadJson { get; set; }
    
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public Machine Machine { get; set; } = null!;
}

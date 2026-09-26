using ArcadeOS.Api.Domain.Enums;

namespace ArcadeOS.Api.Domain.Entities;

/// <summary>
/// Represents a physical game cabinet in the amusement center.
/// 
/// IOT PATTERN:
/// Machines are treated like IoT edge devices. They have their own credentials (AppUser with Role=Machine)
/// and authenticate via JWT to post data to the API. 
/// 
/// We keep the Machine entity relatively static. The highly volatile data (is it online right now? 
/// how many times was it played today?) is captured via Heartbeats.
/// </summary>
public class Machine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public string Name { get; set; } = string.Empty; // e.g., "Pac-Man Battle Royale"
    
    public string? Model { get; set; } // e.g., "Namco 2011"
    
    public string? SerialNumber { get; set; }
    
    public decimal CreditCost { get; set; } = 1.0m; // How many credits to play?
    
    public int TicketPayout { get; set; } = 0; // Average ticket payout or fixed payout

    // Current state. This is updated by background jobs (Offline) or heartbeats (Online/Error)
    public MachineStatus Status { get; set; } = MachineStatus.Offline;

    public string? QrCode { get; set; } // For players to scan with the mobile app
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties for future use (e.g., LocationId)
    // public Guid LocationId { get; set; }
    
    public ICollection<MachineHeartbeat> Heartbeats { get; set; } = new List<MachineHeartbeat>();
}

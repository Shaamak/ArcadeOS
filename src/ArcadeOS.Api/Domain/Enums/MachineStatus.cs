namespace ArcadeOS.Api.Domain.Enums;

/// <summary>
/// Represents the current operational state of a game machine.
/// 
/// WHY STORE AS STRING IN DB?
/// Same as TransactionType — if we add a new status later, integer enums might shift
/// and corrupt old data. Strings ("Online", "Offline") are safe and readable directly in pgAdmin.
/// </summary>
public enum MachineStatus
{
    Online,       // Machine is connected and ready to play
    Offline,      // Machine hasn't sent a heartbeat recently
    Error,        // Machine reported a hardware/software fault
    Maintenance   // Staff put the machine in maintenance mode (ignores heartbeats)
}

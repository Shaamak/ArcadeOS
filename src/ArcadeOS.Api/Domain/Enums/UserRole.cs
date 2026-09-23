namespace ArcadeOS.Api.Domain.Enums;

/// <summary>
/// Defines the role of a user in the system.
/// Each role has different permissions enforced via [Authorize(Roles = "...")] on controllers.
/// </summary>
public enum UserRole
{
    Admin,   // Full access — managers, superusers
    Staff,   // POS terminal access — scoped to a location
    Kiosk,   // Self-service kiosk machine account
    Machine  // Arcade machine agent — heartbeat-only access
}

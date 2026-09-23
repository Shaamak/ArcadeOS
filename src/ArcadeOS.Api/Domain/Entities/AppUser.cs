using ArcadeOS.Api.Domain.Enums;

namespace ArcadeOS.Api.Domain.Entities;

/// <summary>
/// Represents a system user (staff, admin, kiosk account, or machine agent).
/// This is NOT the same as a Customer — customers are members of the arcade.
/// AppUser is the authentication identity.
/// </summary>
public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Login name — must be unique across all users.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>BCrypt hashed password. Never store plaintext.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Determines what this user is allowed to do in the API.</summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Optional: Staff users are scoped to a specific location.
    /// Null means the user has global access (Admin).
    /// </summary>
    public Guid? LocationId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Opaque token stored server-side.
    /// When a client uses an expired JWT access token, they present this
    /// to get a new access token WITHOUT logging in again.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>When this refresh token expires. After this, user must log in again.</summary>
    public DateTime? RefreshTokenExpiresAt { get; set; }
}

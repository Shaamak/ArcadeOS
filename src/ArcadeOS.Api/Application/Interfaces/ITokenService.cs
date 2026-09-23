using ArcadeOS.Api.Domain.Entities;

namespace ArcadeOS.Api.Application.Interfaces;

/// <summary>
/// Contract for generating and validating JWT tokens.
///
/// WHY AN INTERFACE?
/// The controller and service code depends on this interface, not the concrete class.
/// This means in unit tests, we can swap in a fake/mock TokenService without needing
/// a real JWT signing key or a running server. This is the Dependency Inversion Principle.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Creates a signed JWT access token containing the user's ID, username, and role as claims.
    /// The token is valid for a short duration (e.g. 15 minutes).
    /// </summary>
    string GenerateAccessToken(AppUser user);

    /// <summary>
    /// Creates a cryptographically random opaque refresh token string.
    /// This is stored in the DB alongside its expiry date.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>Returns when an access token expires (UTC).</summary>
    DateTime GetAccessTokenExpiry();

    /// <summary>Returns when a refresh token expires (UTC).</summary>
    DateTime GetRefreshTokenExpiry();
}

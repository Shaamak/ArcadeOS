namespace ArcadeOS.Api.Application.DTOs;

/// <summary>
/// Data Transfer Objects for Authentication.
///
/// DTOs are "shapes" of data the API sends/receives — they are NOT domain entities.
/// Why separate? Entities are internal (coupled to the DB), DTOs are public contracts.
/// If we add an internal field to AppUser, it doesn't automatically leak through the API.
/// </summary>

// --- Request DTOs (what the client sends) ---

/// <summary>Login request body — POST /api/auth/login</summary>
public record LoginRequest(string Username, string Password);

/// <summary>Refresh request body — POST /api/auth/refresh</summary>
public record RefreshRequest(string RefreshToken);

// --- Response DTOs (what the API returns) ---

/// <summary>
/// Returned after a successful login or refresh.
/// AccessToken: short-lived JWT (15 min) — sent with every API request in the Authorization header.
/// RefreshToken: long-lived opaque token (7 days) — stored by the client, used only to get new AccessTokens.
/// </summary>
public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt
);

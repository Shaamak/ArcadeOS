using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Interfaces;
using ArcadeOS.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArcadeOS.Api.Controllers;

/// <summary>
/// Handles authentication endpoints: login and token refresh.
///
/// FLOW — Login:
///   Client POST /api/auth/login { username, password }
///     → We look up the user in the DB
///     → We verify the submitted password against the stored BCrypt hash
///     → We generate a JWT access token + refresh token
///     → We save the refresh token hash to the DB
///     → We return both tokens to the client
///
/// FLOW — Refresh:
///   Client POST /api/auth/refresh { refreshToken }
///     → We look up the refresh token in the DB
///     → We check it hasn't expired
///     → We issue a new access token (and optionally rotate the refresh token)
///     → We return the new tokens
///
/// These endpoints are intentionally NOT decorated with [Authorize] —
/// they ARE the endpoints that grant authorization.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ArcadeDbContext _db;
    private readonly ITokenService _tokenService;

    // Both dependencies are injected by ASP.NET Core's DI container at request time.
    // The controller never calls `new ArcadeDbContext()` or `new TokenService()` directly.
    public AuthController(ArcadeDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    /// <summary>
    /// POST /api/auth/login
    /// Validates credentials and returns JWT access + refresh tokens.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // 1. Find user by username (case-insensitive comparison happens in DB)
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

        if (user is null)
            return Unauthorized(new { error = new { code = "INVALID_CREDENTIALS", message = "Invalid username or password." } });

        // 2. Verify the submitted password against the BCrypt hash stored in the DB.
        //    BCrypt.Verify() hashes the submitted password with the SAME salt that was
        //    used when the password was originally hashed, then compares. The salt is
        //    embedded in the stored hash string itself ($2a$12$...).
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { error = new { code = "INVALID_CREDENTIALS", message = "Invalid username or password." } });

        // 3. Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // 4. Persist the refresh token to the DB (so we can validate it later)
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiresAt = _tokenService.GetRefreshTokenExpiry();
        await _db.SaveChangesAsync();

        return Ok(new AuthResponse(
            AccessToken: accessToken,
            AccessTokenExpiresAt: _tokenService.GetAccessTokenExpiry(),
            RefreshToken: refreshToken,
            RefreshTokenExpiresAt: user.RefreshTokenExpiresAt.Value
        ));
    }

    /// <summary>
    /// POST /api/auth/refresh
    /// Issues a new access token using a valid refresh token.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        // Look up user by their stored refresh token
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken && u.IsActive);

        if (user is null || user.RefreshTokenExpiresAt < DateTime.UtcNow)
            return Unauthorized(new { error = new { code = "INVALID_REFRESH_TOKEN", message = "Refresh token is invalid or expired. Please log in again." } });

        // Issue a new access token
        var newAccessToken = _tokenService.GenerateAccessToken(user);

        // Rotate the refresh token — best practice: issue a new one each time
        // This limits the window during which a stolen refresh token can be used.
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiresAt = _tokenService.GetRefreshTokenExpiry();
        await _db.SaveChangesAsync();

        return Ok(new AuthResponse(
            AccessToken: newAccessToken,
            AccessTokenExpiresAt: _tokenService.GetAccessTokenExpiry(),
            RefreshToken: newRefreshToken,
            RefreshTokenExpiresAt: user.RefreshTokenExpiresAt.Value
        ));
    }
}

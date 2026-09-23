using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ArcadeOS.Api.Application.Interfaces;
using ArcadeOS.Api.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ArcadeOS.Api.Infrastructure.Auth;

/// <summary>
/// Concrete implementation of ITokenService.
/// Handles all JWT creation logic.
///
/// HOW JWT WORKS (simplified):
/// A JWT is three base64-encoded parts separated by dots: Header.Payload.Signature
///
///   Header:  { "alg": "HS256", "typ": "JWT" }
///   Payload: { "sub": "user-id", "role": "Admin", "exp": 1234567890 }
///   Signature: HMACSHA256(base64(header) + "." + base64(payload), secretKey)
///
/// The server signs the token with a secret key. When the client sends the token back,
/// the server re-signs the header+payload and checks if the signature matches.
/// If someone tampers with the payload (e.g., changes "Staff" to "Admin"), the signature
/// won't match and the token is rejected.
///
/// The client CANNOT fake tokens without knowing the secret key.
/// </summary>
public class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    // Access token lifetime: short (15 min) — limits window of exposure if stolen
    private const int AccessTokenLifetimeMinutes = 15;

    // Refresh token lifetime: longer (7 days) — client can silently renew access tokens
    private const int RefreshTokenLifetimeDays = 7;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateAccessToken(AppUser user)
    {
        // 1. Define the signing key from appsettings.json
        //    This MUST be at least 32 characters (256 bits) for HS256
        var jwtKey = _config["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT Key not configured in appsettings.");

        var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        // 2. Define claims — these are "statements" embedded inside the token payload
        //    The [Authorize] middleware reads these claims to enforce permissions.
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),    // "subject" = user ID
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),  // username
            new Claim(ClaimTypes.Role, user.Role.ToString()),              // role for [Authorize(Roles)]
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // unique token ID
        };

        // 3. Build the token descriptor
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],       // who created this token (our API)
            audience: _config["Jwt:Audience"],   // who this token is intended for (our Angular app)
            claims: claims,
            expires: GetAccessTokenExpiry(),
            signingCredentials: credentials
        );

        // 4. Serialize to string: "eyJ..."
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        // A refresh token is simply a cryptographically random byte array, base64-encoded.
        // It has NO embedded information — it's just an opaque random string.
        // The server looks it up in the database to find the associated user.
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

    public DateTime GetAccessTokenExpiry()
        => DateTime.UtcNow.AddMinutes(AccessTokenLifetimeMinutes);

    public DateTime GetRefreshTokenExpiry()
        => DateTime.UtcNow.AddDays(RefreshTokenLifetimeDays);
}

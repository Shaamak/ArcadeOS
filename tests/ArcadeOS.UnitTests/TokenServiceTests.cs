using System.IdentityModel.Tokens.Jwt;
using ArcadeOS.Api.Domain.Entities;
using ArcadeOS.Api.Domain.Enums;
using ArcadeOS.Api.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;

namespace ArcadeOS.UnitTests;

/// <summary>
/// Unit tests for TokenService.
/// These tests do NOT hit a real database or web server —
/// they instantiate the class directly, which is fast and isolated.
/// </summary>
public class TokenServiceTests
{
    // ---------------------------------------------------------------
    // Test Setup — build a fake IConfiguration (like an in-memory appsettings.json)
    // ---------------------------------------------------------------

    private static TokenService BuildTokenService()
    {
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "TestSuperSecretKey-MustBe32CharsOrMore!!",
            ["Jwt:Issuer"] = "ArcadeOS.Api.Test",
            ["Jwt:Audience"] = "ArcadeOS.Web.Test"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        return new TokenService(config);
    }

    private static AppUser BuildTestUser(UserRole role = UserRole.Admin) => new AppUser
    {
        Id = Guid.NewGuid(),
        Username = "testuser",
        PasswordHash = "hashed",
        Role = role,
        IsActive = true
    };

    // ---------------------------------------------------------------
    // Test: GenerateAccessToken returns a non-empty JWT string
    // ---------------------------------------------------------------
    [Fact]
    public void GenerateAccessToken_ReturnsNonEmptyString()
    {
        var service = BuildTokenService();
        var user = BuildTestUser();

        var token = service.GenerateAccessToken(user);

        Assert.NotNull(token);
        Assert.NotEmpty(token);
        // A JWT always has exactly 2 dots, producing 3 segments: header.payload.signature
        Assert.Equal(2, token.Count(c => c == '.'));
    }

    // ---------------------------------------------------------------
    // Test: The JWT contains the correct claims (role, username, subject)
    // ---------------------------------------------------------------
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Staff)]
    public void GenerateAccessToken_ContainsCorrectClaims(UserRole role)
    {
        var service = BuildTokenService();
        var user = BuildTestUser(role);

        var tokenString = service.GenerateAccessToken(user);

        // Parse the token without validating the signature (we just want to inspect claims)
        var handler = new JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(tokenString);

        // Check the subject claim matches the user ID
        var subClaim = parsed.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        Assert.NotNull(subClaim);
        Assert.Equal(user.Id.ToString(), subClaim.Value);

        // Check the role claim is set correctly
        // Note: JwtBearerMiddleware maps ClaimTypes.Role to "role" in the token
        var roleClaim = parsed.Claims.FirstOrDefault(c =>
            c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role);
        Assert.NotNull(roleClaim);
        Assert.Equal(role.ToString(), roleClaim.Value);
    }

    // ---------------------------------------------------------------
    // Test: GenerateRefreshToken returns a different value each call
    // ---------------------------------------------------------------
    [Fact]
    public void GenerateRefreshToken_ReturnsUniqueTokenEachCall()
    {
        var service = BuildTokenService();

        var token1 = service.GenerateRefreshToken();
        var token2 = service.GenerateRefreshToken();

        Assert.NotEqual(token1, token2);
        Assert.NotEmpty(token1);
    }

    // ---------------------------------------------------------------
    // Test: Access token expiry is in the future
    // ---------------------------------------------------------------
    [Fact]
    public void GetAccessTokenExpiry_IsInTheFuture()
    {
        var service = BuildTokenService();
        var expiry = service.GetAccessTokenExpiry();

        Assert.True(expiry > DateTime.UtcNow);
    }

    // ---------------------------------------------------------------
    // Test: Refresh token expiry is longer than access token expiry
    // ---------------------------------------------------------------
    [Fact]
    public void GetRefreshTokenExpiry_IsLongerThanAccessTokenExpiry()
    {
        var service = BuildTokenService();

        var accessExpiry = service.GetAccessTokenExpiry();
        var refreshExpiry = service.GetRefreshTokenExpiry();

        Assert.True(refreshExpiry > accessExpiry);
    }
}

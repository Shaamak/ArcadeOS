using System.Text;
using ArcadeOS.Api.Application.Interfaces;
using ArcadeOS.Api.Infrastructure.Auth;
using ArcadeOS.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// ============================================================
// PROGRAM.CS — The entry point and composition root.
// This is where we "wire up" all services (the DI container)
// and configure the HTTP middleware pipeline.
//
// Think of it in two phases:
//   Phase 1 (builder.*): Register services — "what can the app do?"
//   Phase 2 (app.*):     Configure pipeline — "in what order does the app process requests?"
// ============================================================

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// PHASE 1 — Register Services (Dependency Injection Container)
// ============================================================

// --- Controllers ---
// Tells the framework to discover all classes inheriting from ControllerBase
// and automatically route HTTP requests to them.
builder.Services.AddControllers();

// --- Database (Entity Framework Core + PostgreSQL) ---
// We register ArcadeDbContext as a Scoped service.
// Scoped = one instance per HTTP request. This is correct for DbContext because:
//   - It tracks changes within a single request/transaction.
//   - Two concurrent requests get SEPARATE DbContext instances (no data leaks).
builder.Services.AddDbContext<ArcadeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- JWT Authentication ---
// This registers the authentication middleware and tells it to validate
// incoming Bearer tokens (JWT) on every request that has [Authorize].
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(options =>
{
    // Set JWT Bearer as the default scheme — every [Authorize] attribute will use it.
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // These parameters MUST match what TokenService used when CREATING the token.
    // If any of these don't match, the token is rejected with 401 Unauthorized.
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],

        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],

        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

        ValidateLifetime = true,  // Reject expired tokens
        ClockSkew = TimeSpan.Zero // No tolerance for expiry (default is 5 min — we disable it)
    };
});

// Role-based authorization requires this. It reads the Role claim from the JWT
// and enforces [Authorize(Roles = "Admin")] etc.
builder.Services.AddAuthorization();

// --- Application Services ---
// Register our TokenService as the implementation for ITokenService.
// Any class that asks for ITokenService in its constructor will receive a TokenService instance.
// Scoped lifetime: one instance per request (fine for stateless JWT operations).
builder.Services.AddScoped<ITokenService, TokenService>();

// --- CORS (Cross-Origin Resource Sharing) ---
// The browser blocks JavaScript from calling an API on a different domain/port
// by default. We need to explicitly allow our Angular app (running on :4200)
// to call our API (running on :5000/7000).
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200")  // Angular dev server
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// ============================================================
// PHASE 2 — Configure HTTP Middleware Pipeline
// Requests flow TOP → BOTTOM through this pipeline.
// Responses flow BOTTOM → TOP back up.
// ORDER MATTERS: Auth must come before Authorization.
// ============================================================

app.UseHttpsRedirection();

// Allow Angular app to call our API during development
app.UseCors("AllowAngularDev");

// Reads the JWT from the "Authorization: Bearer <token>" header and
// populates HttpContext.User with claims extracted from the token.
app.UseAuthentication();

// Checks if the authenticated user has the required roles/policies
// for the requested endpoint (e.g. [Authorize(Roles = "Admin")]).
app.UseAuthorization();

// Routes incoming HTTP requests to the correct Controller/Action method.
app.MapControllers();

app.Run();

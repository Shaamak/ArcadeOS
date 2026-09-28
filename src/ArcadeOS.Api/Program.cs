using System.Text;
using ArcadeOS.Api.Application.Interfaces;
using ArcadeOS.Api.Infrastructure.Auth;
using ArcadeOS.Api.Infrastructure.Hubs;
using ArcadeOS.Api.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// ============================================================
// PROGRAM.CS — The entry point and composition root.
// This is where we "wire up" all services (the DI container)
// and configure the HTTP middleware pipeline.
// ============================================================

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// PHASE 1 — Register Services (Dependency Injection Container)
// ============================================================

builder.Services.AddControllers();
builder.Services.AddSignalR();

// OpenAPI / Swagger Documentation Setup
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database (Entity Framework Core + PostgreSQL)
builder.Services.AddDbContext<ArcadeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],

        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],

        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Application Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ArcadeOS.Api.Application.Interfaces.ICustomerService, ArcadeOS.Api.Application.Services.CustomerService>();
builder.Services.AddScoped<ArcadeOS.Api.Application.Interfaces.IWalletService, ArcadeOS.Api.Application.Services.WalletService>();
builder.Services.AddScoped<ArcadeOS.Api.Application.Interfaces.IMachineService, ArcadeOS.Api.Application.Services.MachineService>();
builder.Services.AddScoped<ArcadeOS.Api.Application.Interfaces.IGameplayService, ArcadeOS.Api.Application.Services.GameplayService>();
builder.Services.AddScoped<ArcadeOS.Api.Application.Interfaces.IMembershipService, ArcadeOS.Api.Application.Services.MembershipService>();
builder.Services.AddScoped<ArcadeOS.Api.Application.Interfaces.IRewardService, ArcadeOS.Api.Application.Services.RewardService>();
builder.Services.AddScoped<ArcadeOS.Api.Application.Interfaces.IAnalyticsService, ArcadeOS.Api.Application.Services.AnalyticsService>();

// Register background workers
builder.Services.AddHostedService<ArcadeOS.Api.Infrastructure.BackgroundJobs.MachineStatusMonitorService>();
builder.Services.AddHostedService<ArcadeOS.Api.Infrastructure.BackgroundJobs.MembershipExpirationService>();

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<ArcadeOS.Api.Application.Validators.CreateCustomerDtoValidator>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // SignalR needs credentials for WebSockets
    });
});

var app = builder.Build();

// ============================================================
// PHASE 2 — Configure HTTP Middleware Pipeline
// ============================================================

app.UseMiddleware<ArcadeOS.Api.Middleware.ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ArcadeOS API v1");
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAngularDev");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ArcadeHub>("/hubs/arcade");

app.Run();

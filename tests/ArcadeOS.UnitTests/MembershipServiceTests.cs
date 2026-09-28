using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Services;
using ArcadeOS.Api.Domain.Entities;
using ArcadeOS.Api.Domain.Enums;
using ArcadeOS.Api.Domain.Exceptions;
using ArcadeOS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArcadeOS.UnitTests;

public class MembershipServiceTests
{
    private ArcadeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ArcadeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ArcadeDbContext(options);
    }

    // Seeds a customer + wallet, then returns them
    private static async Task<(Customer customer, Wallet wallet)> SeedCustomerWithWallet(
        ArcadeDbContext db, int ticketBalance = 0)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = "Jane",
            LastName = "Arcade",
            Email = $"jane-{Guid.NewGuid()}@arcade.com",
            IsActive = true
        };

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Balance = 100m,
            LifetimeCredits = 100m,
            TicketBalance = ticketBalance
        };

        db.Customers.Add(customer);
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        return (customer, wallet);
    }

    // Seeds a membership plan, then returns it
    private static async Task<MembershipPlan> SeedPlan(ArcadeDbContext db,
        int dailyBonusTickets = 50, decimal discountPercent = 10m)
    {
        var plan = new MembershipPlan
        {
            Id = Guid.NewGuid(),
            Name = "Gold Plan",
            Description = "VIP Gold Tier",
            MonthlyFee = 9.99m,
            DailyBonusTickets = dailyBonusTickets,
            GameplayDiscountPercent = discountPercent,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.MembershipPlans.Add(plan);
        await db.SaveChangesAsync();

        return plan;
    }

    // ─────────────────────────── PLAN TESTS ───────────────────────────

    [Fact]
    public async Task CreatePlanAsync_ValidInput_CreatesPlan()
    {
        // Arrange
        using var db = CreateDbContext();
        var walletService = new WalletService(db);
        var service = new MembershipService(db, walletService);

        var dto = new CreateMembershipPlanDto("Silver Plan", "Basic Tier", 4.99m, 20, 5m);

        // Act
        var result = await service.CreatePlanAsync(dto);

        // Assert
        Assert.Equal("Silver Plan", result.Name);
        Assert.Equal(20, result.DailyBonusTickets);
        Assert.Equal(5m, result.GameplayDiscountPercent);
        Assert.True(result.IsActive);

        var dbPlan = await db.MembershipPlans.FindAsync(result.Id);
        Assert.NotNull(dbPlan);
        Assert.Equal("Silver Plan", dbPlan.Name);
    }

    [Fact]
    public async Task CreatePlanAsync_NegativeFee_ThrowsValidationException()
    {
        using var db = CreateDbContext();
        var walletService = new WalletService(db);
        var service = new MembershipService(db, walletService);

        var dto = new CreateMembershipPlanDto("Bad Plan", "", -1m, 0, 0m);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreatePlanAsync(dto));
    }

    [Fact]
    public async Task CreatePlanAsync_DiscountOver100_ThrowsValidationException()
    {
        using var db = CreateDbContext();
        var walletService = new WalletService(db);
        var service = new MembershipService(db, walletService);

        var dto = new CreateMembershipPlanDto("Illegal Plan", "", 10m, 10, 110m);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreatePlanAsync(dto));
    }

    // ─────────────────────────── ENROLL TESTS ───────────────────────────

    [Fact]
    public async Task EnrollAsync_ValidRequest_CreatesActiveMembership()
    {
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db);
        var plan = await SeedPlan(db);
        var walletService = new WalletService(db);
        var service = new MembershipService(db, walletService);

        var dto = new EnrollMembershipDto(customer.Id, plan.Id, AutoRenew: true);

        var result = await service.EnrollAsync(dto);

        Assert.Equal(customer.Id, result.CustomerId);
        Assert.Equal(plan.Id, result.PlanId);
        Assert.Equal(MembershipStatus.Active, result.Status);
        Assert.True(result.AutoRenew);
        // End date should be approximately 1 month from now
        Assert.True(result.EndDate > result.StartDate);
    }

    [Fact]
    public async Task EnrollAsync_CustomerAlreadyHasActiveMembership_ThrowsValidationException()
    {
        // This test captures a critical domain rule: one active membership per customer at a time.
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db);
        var plan = await SeedPlan(db);
        var walletService = new WalletService(db);
        var service = new MembershipService(db, walletService);

        var dto = new EnrollMembershipDto(customer.Id, plan.Id);

        // First enrollment should succeed
        await service.EnrollAsync(dto);

        // Second enrollment should fail — cannot have two active memberships
        await Assert.ThrowsAsync<ValidationException>(() => service.EnrollAsync(dto));
    }

    // ─────────────────────────── DAILY BONUS TESTS ───────────────────────────

    [Fact]
    public async Task ClaimDailyBonusAsync_FirstClaim_AddsTicketsToWallet()
    {
        using var db = CreateDbContext();
        var (customer, wallet) = await SeedCustomerWithWallet(db, ticketBalance: 0);
        var plan = await SeedPlan(db, dailyBonusTickets: 50);
        var walletService = new WalletService(db);
        var service = new MembershipService(db, walletService);

        // Enroll first
        await service.EnrollAsync(new EnrollMembershipDto(customer.Id, plan.Id));

        // Act: claim daily bonus
        var result = await service.ClaimDailyBonusAsync(customer.Id);

        Assert.Equal(50, result.TicketsClaimed);
        Assert.Equal(50, result.NewTicketBalance);

        // Verify wallet was actually updated
        var updatedWallet = await db.Wallets.FirstAsync(w => w.CustomerId == customer.Id);
        Assert.Equal(50, updatedWallet.TicketBalance);

        // Verify LastBonusClaimedAt was stamped
        var membership = await db.Memberships.FirstAsync(m => m.CustomerId == customer.Id);
        Assert.NotNull(membership.LastBonusClaimedAt);
    }

    [Fact]
    public async Task ClaimDailyBonusAsync_ClaimedWithin24Hours_ThrowsValidationException()
    {
        // This validates the rate-limiting logic — cannot claim twice in 24 hours
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db);
        var plan = await SeedPlan(db, dailyBonusTickets: 50);
        var walletService = new WalletService(db);
        var service = new MembershipService(db, walletService);

        await service.EnrollAsync(new EnrollMembershipDto(customer.Id, plan.Id));

        // First claim succeeds
        await service.ClaimDailyBonusAsync(customer.Id);

        // Immediate second claim should fail (within 24 hours)
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ClaimDailyBonusAsync(customer.Id));
    }

    [Fact]
    public async Task ClaimDailyBonusAsync_NoActiveMembership_ThrowsNotFoundException()
    {
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db);
        var walletService = new WalletService(db);
        var service = new MembershipService(db, walletService);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.ClaimDailyBonusAsync(customer.Id));
    }

    // ─────────────────────────── CANCEL TESTS ───────────────────────────

    [Fact]
    public async Task CancelMembershipAsync_ActiveMembership_SetsCancelledStatus()
    {
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db);
        var plan = await SeedPlan(db);
        var walletService = new WalletService(db);
        var service = new MembershipService(db, walletService);

        await service.EnrollAsync(new EnrollMembershipDto(customer.Id, plan.Id));

        await service.CancelMembershipAsync(customer.Id);

        var membership = await db.Memberships.FirstAsync(m => m.CustomerId == customer.Id);
        Assert.Equal(MembershipStatus.Cancelled, membership.Status);
        Assert.False(membership.AutoRenew);
    }

    [Fact]
    public async Task CancelMembershipAsync_NoActiveMembership_ThrowsNotFoundException()
    {
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db);
        var walletService = new WalletService(db);
        var service = new MembershipService(db, walletService);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CancelMembershipAsync(customer.Id));
    }

    // ─────────────────────────── BACKGROUND EXPIRY TESTS ───────────────────────────

    [Fact]
    public async Task ProcessExpiredMembershipsAsync_AutoRenewTrue_ExtendsEndDate()
    {
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db);
        var plan = await SeedPlan(db);

        // Seed an already-expired but auto-renew membership directly
        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            PlanId = plan.Id,
            Status = MembershipStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-1).AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(-1), // Already expired
            AutoRenew = true,
            CreatedAt = DateTime.UtcNow.AddMonths(-1),
            UpdatedAt = DateTime.UtcNow.AddMonths(-1)
        };

        db.Memberships.Add(membership);
        await db.SaveChangesAsync();

        var walletService = new WalletService(db);
        var service = new MembershipService(db, walletService);

        await service.ProcessExpiredMembershipsAsync();

        var updated = await db.Memberships.FindAsync(membership.Id);
        Assert.Equal(MembershipStatus.Active, updated!.Status); // Still active (renewed)
        Assert.True(updated.EndDate > DateTime.UtcNow); // Extended by 1 month
    }

    [Fact]
    public async Task ProcessExpiredMembershipsAsync_AutoRenewFalse_SetsExpiredStatus()
    {
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db);
        var plan = await SeedPlan(db);

        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            PlanId = plan.Id,
            Status = MembershipStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-1).AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(-1), // Already expired
            AutoRenew = false,
            CreatedAt = DateTime.UtcNow.AddMonths(-1),
            UpdatedAt = DateTime.UtcNow.AddMonths(-1)
        };

        db.Memberships.Add(membership);
        await db.SaveChangesAsync();

        var walletService = new WalletService(db);
        var service = new MembershipService(db, walletService);

        await service.ProcessExpiredMembershipsAsync();

        var updated = await db.Memberships.FindAsync(membership.Id);
        Assert.Equal(MembershipStatus.Expired, updated!.Status); // Expired — no renewal
    }
}

using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Services;
using ArcadeOS.Api.Domain.Entities;
using ArcadeOS.Api.Domain.Enums;
using ArcadeOS.Api.Domain.Exceptions;
using ArcadeOS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArcadeOS.UnitTests;

public class RewardServiceTests
{
    private ArcadeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ArcadeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ArcadeDbContext(options);
    }

    private static async Task<(Customer customer, Wallet wallet)> SeedCustomerWithWallet(
        ArcadeDbContext db, int ticketBalance = 500)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = "Player",
            LastName = "One",
            Email = $"player-{Guid.NewGuid()}@arcade.com",
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

    private static async Task<RewardItem> SeedRewardItem(
        ArcadeDbContext db, int ticketCost = 100, int stock = 10)
    {
        var item = new RewardItem
        {
            Id = Guid.NewGuid(),
            Name = "Plushie Bear",
            Description = "Soft and cuddly",
            TicketCost = ticketCost,
            StockQuantity = stock,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.RewardItems.Add(item);
        await db.SaveChangesAsync();

        return item;
    }

    // ─────────────────────────── CREATE REWARD TESTS ───────────────────────────

    [Fact]
    public async Task CreateRewardAsync_ValidInput_CreatesItem()
    {
        using var db = CreateDbContext();
        var walletService = new WalletService(db);
        var service = new RewardService(db, walletService);

        var dto = new CreateRewardItemDto("Gaming Mouse", "High precision mouse", 500, 20);

        var result = await service.CreateRewardAsync(dto);

        Assert.Equal("Gaming Mouse", result.Name);
        Assert.Equal(500, result.TicketCost);
        Assert.Equal(20, result.StockQuantity);
        Assert.True(result.IsActive);

        var dbItem = await db.RewardItems.FindAsync(result.Id);
        Assert.NotNull(dbItem);
    }

    [Fact]
    public async Task CreateRewardAsync_ZeroTicketCost_ThrowsValidationException()
    {
        using var db = CreateDbContext();
        var walletService = new WalletService(db);
        var service = new RewardService(db, walletService);

        var dto = new CreateRewardItemDto("Free Item", "", 0, 10);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateRewardAsync(dto));
    }

    [Fact]
    public async Task CreateRewardAsync_NegativeStock_ThrowsValidationException()
    {
        using var db = CreateDbContext();
        var walletService = new WalletService(db);
        var service = new RewardService(db, walletService);

        var dto = new CreateRewardItemDto("Ghost Item", "", 100, -5);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateRewardAsync(dto));
    }

    // ─────────────────────────── REDEMPTION TESTS ───────────────────────────

    [Fact]
    public async Task RedeemRewardAsync_SufficientTickets_SuccessfulRedemption()
    {
        // This is the core happy path: customer spends tickets to get a prize.
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db, ticketBalance: 500);
        var item = await SeedRewardItem(db, ticketCost: 100, stock: 5);
        var walletService = new WalletService(db);
        var service = new RewardService(db, walletService);

        var dto = new RedeemRewardDto(customer.Id, item.Id, Guid.NewGuid().ToString());

        var result = await service.RedeemRewardAsync(dto);

        Assert.Equal(customer.Id, result.CustomerId);
        Assert.Equal(item.Id, result.RewardItemId);
        Assert.Equal(100, result.TicketsSpent);
        Assert.Equal(RedemptionStatus.Pending, result.Status);

        // Verify ticket balance was deducted
        var updatedWallet = await db.Wallets.FirstAsync(w => w.CustomerId == customer.Id);
        Assert.Equal(400, updatedWallet.TicketBalance);  // 500 - 100

        // Verify stock was decremented
        var updatedItem = await db.RewardItems.FindAsync(item.Id);
        Assert.Equal(4, updatedItem!.StockQuantity);  // 5 - 1
    }

    [Fact]
    public async Task RedeemRewardAsync_InsufficientTickets_ThrowsValidationException()
    {
        // Customer cannot spend tickets they don't have
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db, ticketBalance: 50); // Only 50 tickets
        var item = await SeedRewardItem(db, ticketCost: 100, stock: 5);  // Costs 100
        var walletService = new WalletService(db);
        var service = new RewardService(db, walletService);

        var dto = new RedeemRewardDto(customer.Id, item.Id);

        await Assert.ThrowsAsync<ValidationException>(() => service.RedeemRewardAsync(dto));

        // Verify wallet and stock are unchanged after the failed attempt
        var wallet = await db.Wallets.FirstAsync(w => w.CustomerId == customer.Id);
        Assert.Equal(50, wallet.TicketBalance);

        var rewardItem = await db.RewardItems.FindAsync(item.Id);
        Assert.Equal(5, rewardItem!.StockQuantity);
    }

    [Fact]
    public async Task RedeemRewardAsync_OutOfStock_ThrowsValidationException()
    {
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db, ticketBalance: 500);
        var item = await SeedRewardItem(db, ticketCost: 100, stock: 0); // Zero stock
        var walletService = new WalletService(db);
        var service = new RewardService(db, walletService);

        var dto = new RedeemRewardDto(customer.Id, item.Id);

        await Assert.ThrowsAsync<ValidationException>(() => service.RedeemRewardAsync(dto));
    }

    [Fact]
    public async Task RedeemRewardAsync_DuplicateReferenceId_IsIdempotent()
    {
        // Same as wallet idempotency: a retry on the same ReferenceId returns the original
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db, ticketBalance: 500);
        var item = await SeedRewardItem(db, ticketCost: 100, stock: 10);
        var walletService = new WalletService(db);
        var service = new RewardService(db, walletService);

        var referenceId = Guid.NewGuid().ToString();
        var dto = new RedeemRewardDto(customer.Id, item.Id, referenceId);

        var first = await service.RedeemRewardAsync(dto);
        var second = await service.RedeemRewardAsync(dto);  // Retry with same referenceId

        Assert.Equal(first.Id, second.Id);  // Same redemption returned

        var redemptionCount = await db.Redemptions.CountAsync();
        Assert.Equal(1, redemptionCount);   // Only one record created

        var wallet = await db.Wallets.FirstAsync(w => w.CustomerId == customer.Id);
        Assert.Equal(400, wallet.TicketBalance);  // Deducted only once
    }

    // ─────────────────────────── CLAIM REDEMPTION TESTS ───────────────────────────

    [Fact]
    public async Task ClaimRedemptionAsync_PendingRedemption_SetsClaimedStatus()
    {
        // Simulates the staff counter scanning the redemption and marking it as picked up
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db, ticketBalance: 500);
        var item = await SeedRewardItem(db, ticketCost: 100, stock: 5);
        var walletService = new WalletService(db);
        var service = new RewardService(db, walletService);

        var dto = new RedeemRewardDto(customer.Id, item.Id);
        var redemption = await service.RedeemRewardAsync(dto);

        var result = await service.ClaimRedemptionAsync(redemption.Id);

        Assert.Equal(RedemptionStatus.Claimed, result.Status);
        Assert.NotNull(result.ClaimedAtUtc);
    }

    [Fact]
    public async Task ClaimRedemptionAsync_AlreadyClaimed_ThrowsValidationException()
    {
        // Prevents double-claiming the same prize
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db, ticketBalance: 500);
        var item = await SeedRewardItem(db, ticketCost: 100, stock: 5);
        var walletService = new WalletService(db);
        var service = new RewardService(db, walletService);

        var dto = new RedeemRewardDto(customer.Id, item.Id);
        var redemption = await service.RedeemRewardAsync(dto);

        await service.ClaimRedemptionAsync(redemption.Id);  // First claim

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ClaimRedemptionAsync(redemption.Id));   // Second claim
    }

    // ─────────────────────────── WALLET DEDUCT TICKETS TESTS ───────────────────────────

    [Fact]
    public async Task DeductTicketsAsync_SufficientBalance_DeductsCorrectly()
    {
        using var db = CreateDbContext();
        var (customer, wallet) = await SeedCustomerWithWallet(db, ticketBalance: 200);
        var service = new WalletService(db);

        await service.DeductTicketsAsync(customer.Id, 75);

        var updated = await db.Wallets.FindAsync(wallet.Id);
        Assert.Equal(125, updated!.TicketBalance);  // 200 - 75
    }

    [Fact]
    public async Task DeductTicketsAsync_InsufficientTickets_ThrowsValidationException()
    {
        using var db = CreateDbContext();
        var (customer, wallet) = await SeedCustomerWithWallet(db, ticketBalance: 30);
        var service = new WalletService(db);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.DeductTicketsAsync(customer.Id, 100));

        // Verify balance unchanged
        var unchanged = await db.Wallets.FindAsync(wallet.Id);
        Assert.Equal(30, unchanged!.TicketBalance);
    }
}

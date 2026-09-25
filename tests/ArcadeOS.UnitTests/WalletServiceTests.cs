using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Services;
using ArcadeOS.Api.Domain.Entities;
using ArcadeOS.Api.Domain.Exceptions;
using ArcadeOS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArcadeOS.UnitTests;

public class WalletServiceTests
{
    private ArcadeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ArcadeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ArcadeDbContext(options);
    }

    private static async Task<(Customer customer, Wallet wallet)> SeedCustomerWithWallet(
        ArcadeDbContext db, decimal initialBalance = 100m)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            Email = $"test-{Guid.NewGuid()}@example.com",
            IsActive = true
        };

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Balance = initialBalance,
            LifetimeCredits = initialBalance,
            TicketBalance = 0
        };

        db.Customers.Add(customer);
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        return (customer, wallet);
    }

    // ─────────── TopUp Tests ───────────

    [Fact]
    public async Task TopUpAsync_ValidRequest_IncreaseBalanceAndCreatesTransaction()
    {
        // Arrange
        using var db = CreateDbContext();
        var (customer, wallet) = await SeedCustomerWithWallet(db, initialBalance: 50m);
        var service = new WalletService(db);

        var dto = new TopUpRequestDto(customer.Id, 25m, Guid.NewGuid().ToString(), "Test top-up");

        // Act
        var result = await service.TopUpAsync(dto);

        // Assert
        Assert.Equal(25m, result.Amount);
        Assert.Equal(50m, result.BalanceBefore);
        Assert.Equal(75m, result.BalanceAfter);                          // 50 + 25

        var updatedWallet = await db.Wallets.FindAsync(wallet.Id);
        Assert.Equal(75m, updatedWallet!.Balance);
        Assert.Equal(75m, updatedWallet.LifetimeCredits);                // LifetimeCredits increases
    }

    [Fact]
    public async Task TopUpAsync_DuplicateReferenceId_IsIdempotent()
    {
        // Arrange
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db, initialBalance: 50m);
        var service = new WalletService(db);

        var referenceId = Guid.NewGuid().ToString();
        var dto = new TopUpRequestDto(customer.Id, 25m, referenceId, "First attempt");

        // Act: Call twice with same ReferenceId
        var firstResult = await service.TopUpAsync(dto);
        var secondResult = await service.TopUpAsync(dto);  // retry

        // Assert: Only one transaction exists, balance incremented only once
        Assert.Equal(firstResult.Id, secondResult.Id);     // Same transaction returned
        var walletTxCount = await db.Transactions.CountAsync();
        Assert.Equal(1, walletTxCount);                    // Only one record in DB

        var wallet = await db.Wallets.FirstAsync(w => w.CustomerId == customer.Id);
        Assert.Equal(75m, wallet.Balance);                 // Only incremented once
    }

    [Fact]
    public async Task TopUpAsync_ZeroAmount_ThrowsValidationException()
    {
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db);
        var service = new WalletService(db);

        var dto = new TopUpRequestDto(customer.Id, 0m, Guid.NewGuid().ToString(), null);

        await Assert.ThrowsAsync<ValidationException>(() => service.TopUpAsync(dto));
    }

    // ─────────── Debit Tests ───────────

    [Fact]
    public async Task DebitAsync_SufficientBalance_DeductsCreditsAndCreatesTransaction()
    {
        using var db = CreateDbContext();
        var (customer, wallet) = await SeedCustomerWithWallet(db, initialBalance: 100m);
        var service = new WalletService(db);

        var dto = new DebitRequestDto(customer.Id, 30m, Guid.NewGuid().ToString(), "Played machine #5");

        var result = await service.DebitAsync(dto);

        Assert.Equal(30m, result.Amount);
        Assert.Equal(100m, result.BalanceBefore);
        Assert.Equal(70m, result.BalanceAfter);    // 100 - 30

        var updatedWallet = await db.Wallets.FindAsync(wallet.Id);
        Assert.Equal(70m, updatedWallet!.Balance);
        Assert.Equal(100m, updatedWallet.LifetimeCredits);  // LifetimeCredits unchanged on debit
    }

    [Fact]
    public async Task DebitAsync_InsufficientBalance_ThrowsInsufficientBalanceException()
    {
        // CRITICAL TEST: This ensures you can never go into negative balance
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db, initialBalance: 20m);
        var service = new WalletService(db);

        var dto = new DebitRequestDto(customer.Id, 50m, Guid.NewGuid().ToString(), null);

        await Assert.ThrowsAsync<InsufficientBalanceException>(() => service.DebitAsync(dto));

        // Verify balance unchanged
        var wallet = await db.Wallets.FirstAsync(w => w.CustomerId == customer.Id);
        Assert.Equal(20m, wallet.Balance);
    }

    [Fact]
    public async Task DebitAsync_DuplicateReferenceId_IsIdempotent()
    {
        using var db = CreateDbContext();
        var (customer, _) = await SeedCustomerWithWallet(db, initialBalance: 100m);
        var service = new WalletService(db);

        var referenceId = Guid.NewGuid().ToString();
        var dto = new DebitRequestDto(customer.Id, 30m, referenceId, "Machine play");

        var firstResult = await service.DebitAsync(dto);
        var secondResult = await service.DebitAsync(dto);  // duplicate retry

        Assert.Equal(firstResult.Id, secondResult.Id);
        var txCount = await db.Transactions.CountAsync();
        Assert.Equal(1, txCount);

        var wallet = await db.Wallets.FirstAsync(w => w.CustomerId == customer.Id);
        Assert.Equal(70m, wallet.Balance);  // Deducted only once
    }

    // ─────────── Transaction History Tests ───────────

    [Fact]
    public async Task GetTransactionsAsync_ReturnsPaginatedResults()
    {
        using var db = CreateDbContext();
        var (customer, wallet) = await SeedCustomerWithWallet(db, initialBalance: 500m);
        var service = new WalletService(db);

        // Create 5 transactions
        for (int i = 1; i <= 5; i++)
        {
            await service.TopUpAsync(new TopUpRequestDto(customer.Id, i * 10m, Guid.NewGuid().ToString(), null));
        }

        var query = new TransactionListQueryDto(customer.Id, null, Page: 1, PageSize: 3);
        var result = await service.GetTransactionsAsync(query);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.Items.Count);   // Page size respected
        Assert.Equal(2, result.TotalPages);    // ceil(5/3) = 2
        Assert.True(result.HasNextPage);
    }
}

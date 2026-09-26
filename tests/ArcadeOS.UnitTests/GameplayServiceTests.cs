using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Services;
using ArcadeOS.Api.Domain.Entities;
using ArcadeOS.Api.Domain.Enums;
using ArcadeOS.Api.Domain.Exceptions;
using ArcadeOS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ArcadeOS.UnitTests;

public class GameplayServiceTests
{
    private ArcadeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ArcadeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ArcadeDbContext(options);
    }

    private readonly Mock<ILogger<GameplayService>> _mockLogger = new();

    private static async Task<(Customer customer, Wallet wallet, Machine machine)> SeedDataAsync(
        ArcadeDbContext db, decimal initialBalance = 10m, decimal creditCost = 2.5m, int ticketPayout = 50)
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

        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = "Pac-Man",
            CreditCost = creditCost,
            TicketPayout = ticketPayout,
            Status = MachineStatus.Online,
            IsActive = true
        };

        db.Customers.Add(customer);
        db.Wallets.Add(wallet);
        db.Machines.Add(machine);
        await db.SaveChangesAsync();

        return (customer, wallet, machine);
    }

    [Fact]
    public async Task PlayGameAsync_ValidRequest_DeductsCreditsAndAwardsTickets()
    {
        // Arrange
        using var db = CreateDbContext();
        var (customer, wallet, machine) = await SeedDataAsync(db, initialBalance: 10m, creditCost: 2m, ticketPayout: 50);
        var service = new GameplayService(db, _mockLogger.Object);

        var request = new PlayGameRequestDto(customer.Id, machine.Id, Guid.NewGuid().ToString());

        // Act
        var result = await service.PlayGameAsync(request);

        // Assert
        Assert.Equal(2m, result.CreditsDeducted);
        Assert.Equal(50, result.TicketsAwarded);
        Assert.Equal(8m, result.RemainingBalance); // 10 - 2
        Assert.Equal(50, result.TotalTickets);

        // Verify DB State
        var updatedWallet = await db.Wallets.FindAsync(wallet.Id);
        Assert.Equal(8m, updatedWallet!.Balance);
        Assert.Equal(50, updatedWallet.TicketBalance);

        var transactions = await db.Transactions.Where(t => t.WalletId == wallet.Id).ToListAsync();
        Assert.Single(transactions);
        Assert.Equal(TransactionType.Debit, transactions[0].Type);
        Assert.Equal(2m, transactions[0].Amount);
    }

    [Fact]
    public async Task PlayGameAsync_MachineOffline_ThrowsValidationException()
    {
        using var db = CreateDbContext();
        var (customer, _, machine) = await SeedDataAsync(db);
        
        machine.Status = MachineStatus.Offline;
        await db.SaveChangesAsync();

        var service = new GameplayService(db, _mockLogger.Object);
        var request = new PlayGameRequestDto(customer.Id, machine.Id, Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<ValidationException>(() => service.PlayGameAsync(request));
    }

    [Fact]
    public async Task PlayGameAsync_InsufficientBalance_ThrowsInsufficientBalanceException()
    {
        using var db = CreateDbContext();
        var (customer, _, machine) = await SeedDataAsync(db, initialBalance: 1m, creditCost: 2m); // Only 1 credit, costs 2

        var service = new GameplayService(db, _mockLogger.Object);
        var request = new PlayGameRequestDto(customer.Id, machine.Id, Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<InsufficientBalanceException>(() => service.PlayGameAsync(request));
    }

    [Fact]
    public async Task PlayGameAsync_DuplicateReferenceId_IsIdempotentAndReturnsCachedResult()
    {
        using var db = CreateDbContext();
        var (customer, _, machine) = await SeedDataAsync(db, initialBalance: 10m, creditCost: 2m, ticketPayout: 50);
        var service = new GameplayService(db, _mockLogger.Object);

        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new PlayGameRequestDto(customer.Id, machine.Id, idempotencyKey);

        // Act
        var firstResult = await service.PlayGameAsync(request);
        var secondResult = await service.PlayGameAsync(request); // Exact same request retried

        // Assert
        Assert.Equal(firstResult.TransactionId, secondResult.TransactionId);
        Assert.Equal(firstResult.RemainingBalance, secondResult.RemainingBalance);
        
        var wallet = await db.Wallets.FirstAsync(w => w.CustomerId == customer.Id);
        Assert.Equal(8m, wallet.Balance); // Deducted only once
        Assert.Equal(50, wallet.TicketBalance); // Awarded tickets only once
    }
}

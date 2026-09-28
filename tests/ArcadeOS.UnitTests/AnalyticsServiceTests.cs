using ArcadeOS.Api.Application.Services;
using ArcadeOS.Api.Domain.Entities;
using ArcadeOS.Api.Domain.Enums;
using ArcadeOS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArcadeOS.UnitTests;

public class AnalyticsServiceTests
{
    private ArcadeDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ArcadeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ArcadeDbContext(options);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_ShouldReturnCorrectAggregations()
    {
        // Arrange
        using var context = GetInMemoryDbContext();

        var customer = new Customer
        {
            FirstName = "John",
            LastName = "Analytics",
            Email = "john.analytics@example.com",
            Phone = "+15550001111"
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var wallet = new Wallet
        {
            CustomerId = customer.Id,
            Balance = 100m,
            TicketBalance = 50
        };
        context.Wallets.Add(wallet);
        await context.SaveChangesAsync();

        var machine = new Machine
        {
            Name = "Street Fighter",
            Model = "Arcade Cabinet",
            CreditCost = 5m,
            Status = MachineStatus.Online
        };
        context.Machines.Add(machine);

        var topup = new Transaction
        {
            WalletId = wallet.Id,
            Type = TransactionType.TopUp,
            Amount = 50m,
            BalanceBefore = 0m,
            BalanceAfter = 50m,
            CreatedAt = DateTime.UtcNow
        };
        context.Transactions.Add(topup);

        var debit = new Transaction
        {
            WalletId = wallet.Id,
            Type = TransactionType.Debit,
            Amount = 5m,
            BalanceBefore = 50m,
            BalanceAfter = 45m,
            Description = "Played Street Fighter",
            CreatedAt = DateTime.UtcNow
        };
        context.Transactions.Add(debit);

        await context.SaveChangesAsync();

        var service = new AnalyticsService(context);

        // Act
        var result = await service.GetDashboardSummaryAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(50m, result.TotalRevenue);
        Assert.Equal(5m, result.TotalCreditsSpent);
        Assert.Equal(1, result.TotalActiveCards);
        Assert.Equal(1, result.TotalMachines);
        Assert.Equal(1, result.OnlineMachines);
        Assert.Equal(50, result.TotalTicketsIssued);
    }

    [Fact]
    public async Task GetMachinePerformanceAsync_ShouldCalculateRevenueAndPayoutRatio()
    {
        // Arrange
        using var context = GetInMemoryDbContext();

        var machine1 = new Machine { Name = "Tekken", Model = "Cabinet", CreditCost = 4m, TicketPayout = 20, Status = MachineStatus.Online };
        var machine2 = new Machine { Name = "Pac-Man", Model = "Retro", CreditCost = 2m, TicketPayout = 5, Status = MachineStatus.Online };
        context.Machines.AddRange(machine1, machine2);
        await context.SaveChangesAsync();

        var wallet = new Wallet { CustomerId = Guid.NewGuid(), Balance = 100m };
        context.Wallets.Add(wallet);
        await context.SaveChangesAsync();

        var txn1 = new Transaction { WalletId = wallet.Id, Type = TransactionType.Debit, Amount = 4m, BalanceBefore = 100, BalanceAfter = 96, Description = "Played Tekken", CreatedAt = DateTime.UtcNow };
        var txn2 = new Transaction { WalletId = wallet.Id, Type = TransactionType.Debit, Amount = 2m, BalanceBefore = 96, BalanceAfter = 94, Description = "Played Pac-Man", CreatedAt = DateTime.UtcNow };
        context.Transactions.AddRange(txn1, txn2);
        await context.SaveChangesAsync();

        var service = new AnalyticsService(context);

        // Act
        var result = (await service.GetMachinePerformanceAsync()).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(machine1.Id, result.First().MachineId);
        Assert.Equal(4m, result.First().TotalRevenue);
        Assert.Equal(20, result.First().TotalTicketsIssued);
    }

    [Fact]
    public async Task GetMembershipDistributionAsync_ShouldReturnCorrectPercentages()
    {
        // Arrange
        using var context = GetInMemoryDbContext();

        var planGold = new MembershipPlan { Name = "Gold Plan", MonthlyFee = 50m, DailyBonusTickets = 20, GameplayDiscountPercent = 10 };
        context.MembershipPlans.Add(planGold);
        await context.SaveChangesAsync();

        var cust1 = new Customer { FirstName = "Member 1", LastName = "User", Email = "m1@test.com" };
        var cust2 = new Customer { FirstName = "Member 2", LastName = "User", Email = "m2@test.com" };
        context.Customers.AddRange(cust1, cust2);
        await context.SaveChangesAsync();

        var mem1 = new Membership { CustomerId = cust1.Id, PlanId = planGold.Id, Status = MembershipStatus.Active, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30) };
        var mem2 = new Membership { CustomerId = cust2.Id, PlanId = planGold.Id, Status = MembershipStatus.Active, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30) };
        context.Memberships.AddRange(mem1, mem2);
        await context.SaveChangesAsync();

        var service = new AnalyticsService(context);

        // Act
        var result = (await service.GetMembershipDistributionAsync()).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("Gold Plan", result.First().PlanName);
        Assert.Equal(2, result.First().MemberCount);
        Assert.Equal(100.0m, result.First().Percentage);
    }
}

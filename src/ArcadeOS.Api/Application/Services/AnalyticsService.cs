using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Interfaces;
using ArcadeOS.Api.Domain.Enums;
using ArcadeOS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArcadeOS.Api.Application.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly ArcadeDbContext _context;

    public AnalyticsService(ArcadeDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync()
    {
        var totalRevenue = await _context.Transactions
            .Where(t => t.Type == TransactionType.TopUp)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        var totalCreditsSpent = await _context.Transactions
            .Where(t => t.Type == TransactionType.Debit)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        var activeCards = await _context.Wallets.CountAsync();

        var totalMachines = await _context.Machines.CountAsync();
        var onlineMachines = await _context.Machines
            .CountAsync(m => m.Status == MachineStatus.Online);

        var totalTicketsIssued = await _context.Wallets
            .SumAsync(w => (int?)w.TicketBalance) ?? 0;

        var totalRewardsRedeemed = await _context.Redemptions.CountAsync();

        return new DashboardSummaryDto(
            totalRevenue,
            totalCreditsSpent,
            activeCards,
            totalMachines,
            onlineMachines,
            totalTicketsIssued,
            totalRewardsRedeemed
        );
    }

    public async Task<IEnumerable<MachinePerformanceDto>> GetMachinePerformanceAsync()
    {
        var machines = await _context.Machines.ToListAsync();
        var debitTransactions = await _context.Transactions
            .Where(t => t.Type == TransactionType.Debit)
            .ToListAsync();

        var performanceList = new List<MachinePerformanceDto>();

        foreach (var m in machines)
        {
            // Transactions containing machine name in description
            var mTxns = debitTransactions
                .Where(t => t.Description != null && t.Description.Contains(m.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var totalRevenue = mTxns.Sum(t => t.Amount);
            var totalPlays = mTxns.Count;
            var totalTickets = totalPlays * m.TicketPayout;

            var payoutPct = totalPlays > 0 ? (decimal)totalTickets / totalPlays : 0m;

            performanceList.Add(new MachinePerformanceDto(
                m.Id,
                m.Name,
                m.Model ?? "Standard",
                totalRevenue,
                totalPlays,
                totalTickets,
                Math.Round(payoutPct, 2)
            ));
        }

        return performanceList.OrderByDescending(p => p.TotalRevenue);
    }

    public async Task<IEnumerable<HourlyActivityDto>> GetHourlyActivityAsync(DateTime date)
    {
        var startOfDay = date.Date.ToUniversalTime();
        var endOfDay = startOfDay.AddDays(1);

        var txns = await _context.Transactions
            .Where(t => t.CreatedAt >= startOfDay && t.CreatedAt < endOfDay && t.Type == TransactionType.Debit)
            .ToListAsync();

        var result = new List<HourlyActivityDto>();

        for (int hour = 0; hour < 24; hour++)
        {
            var hourlyTxns = txns.Where(t => t.CreatedAt.Hour == hour).ToList();
            result.Add(new HourlyActivityDto(
                hour,
                hourlyTxns.Count,
                hourlyTxns.Sum(t => t.Amount)
            ));
        }

        return result;
    }

    public async Task<IEnumerable<MembershipDistributionDto>> GetMembershipDistributionAsync()
    {
        var totalActiveMemberships = await _context.Memberships
            .CountAsync(m => m.Status == MembershipStatus.Active);

        if (totalActiveMemberships == 0)
        {
            return Enumerable.Empty<MembershipDistributionDto>();
        }

        var grouped = await _context.Memberships
            .Include(m => m.Plan)
            .Where(m => m.Status == MembershipStatus.Active)
            .GroupBy(m => m.Plan.Name)
            .Select(g => new
            {
                PlanName = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        return grouped.Select(g => new MembershipDistributionDto(
            g.PlanName,
            g.Count,
            Math.Round(((decimal)g.Count / totalActiveMemberships) * 100, 2)
        ));
    }
}

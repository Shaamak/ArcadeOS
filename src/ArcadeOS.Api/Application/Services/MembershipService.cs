using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Interfaces;
using ArcadeOS.Api.Domain.Entities;
using ArcadeOS.Api.Domain.Enums;
using ArcadeOS.Api.Domain.Exceptions;
using ArcadeOS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArcadeOS.Api.Application.Services;

public class MembershipService : IMembershipService
{
    private readonly ArcadeDbContext _db;
    private readonly IWalletService _walletService;

    public MembershipService(ArcadeDbContext db, IWalletService walletService)
    {
        _db = db;
        _walletService = walletService;
    }

    public async Task<MembershipPlanDto> CreatePlanAsync(CreateMembershipPlanDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ValidationException("Plan name is required.");

        if (dto.MonthlyFee < 0)
            throw new ValidationException("Monthly fee cannot be negative.");

        if (dto.GameplayDiscountPercent < 0 || dto.GameplayDiscountPercent > 100)
            throw new ValidationException("Discount percentage must be between 0 and 100.");

        if (dto.DailyBonusTickets < 0)
            throw new ValidationException("Daily bonus tickets cannot be negative.");

        var plan = new MembershipPlan
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            MonthlyFee = dto.MonthlyFee,
            DailyBonusTickets = dto.DailyBonusTickets,
            GameplayDiscountPercent = dto.GameplayDiscountPercent,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.MembershipPlans.Add(plan);
        await _db.SaveChangesAsync(ct);

        return MapPlanToDto(plan);
    }

    public async Task<IEnumerable<MembershipPlanDto>> GetActivePlansAsync(CancellationToken ct = default)
    {
        var plans = await _db.MembershipPlans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.MonthlyFee)
            .ToListAsync(ct);

        return plans.Select(MapPlanToDto);
    }

    public async Task<MembershipDto> EnrollAsync(EnrollMembershipDto dto, CancellationToken ct = default)
    {
        var customerExists = await _db.Customers.AnyAsync(c => c.Id == dto.CustomerId && c.IsActive, ct);
        if (!customerExists)
            throw new NotFoundException($"Active customer with ID '{dto.CustomerId}' was not found.");

        var plan = await _db.MembershipPlans.FirstOrDefaultAsync(p => p.Id == dto.PlanId && p.IsActive, ct);
        if (plan is null)
            throw new NotFoundException($"Active membership plan with ID '{dto.PlanId}' was not found.");

        // Check if customer already has an active subscription
        var existingActive = await _db.Memberships
            .FirstOrDefaultAsync(m => m.CustomerId == dto.CustomerId && m.Status == MembershipStatus.Active, ct);

        if (existingActive is not null)
        {
            throw new ValidationException("Customer already has an active membership subscription.");
        }

        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            CustomerId = dto.CustomerId,
            PlanId = dto.PlanId,
            Status = MembershipStatus.Active,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(1),
            AutoRenew = dto.AutoRenew,
            LastBonusClaimedAt = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Memberships.Add(membership);
        await _db.SaveChangesAsync(ct);

        // Reload plan details for DTO mapping
        membership.Plan = plan;

        return MapMembershipToDto(membership);
    }

    public async Task<MembershipDto?> GetCustomerMembershipAsync(Guid customerId, CancellationToken ct = default)
    {
        var membership = await _db.Memberships
            .Include(m => m.Plan)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CustomerId == customerId && m.Status == MembershipStatus.Active, ct);

        return membership is null ? null : MapMembershipToDto(membership);
    }

    public async Task<ClaimDailyBonusResponseDto> ClaimDailyBonusAsync(Guid customerId, CancellationToken ct = default)
    {
        var membership = await _db.Memberships
            .Include(m => m.Plan)
            .FirstOrDefaultAsync(m => m.CustomerId == customerId && m.Status == MembershipStatus.Active, ct);

        if (membership is null)
            throw new NotFoundException("No active membership subscription found for this customer.");

        if (membership.Plan.DailyBonusTickets <= 0)
            throw new ValidationException("The active membership plan does not offer daily bonus tickets.");

        // Enforce 24-hour rate limit on bonus claims
        if (membership.LastBonusClaimedAt.HasValue)
        {
            var hoursSinceLastClaim = (DateTime.UtcNow - membership.LastBonusClaimedAt.Value).TotalHours;
            if (hoursSinceLastClaim < 24)
            {
                var remainingHours = Math.Ceiling(24 - hoursSinceLastClaim);
                throw new ValidationException($"Daily bonus already claimed. Please try again in {remainingHours} hours.");
            }
        }

        var now = DateTime.UtcNow;
        membership.LastBonusClaimedAt = now;
        membership.UpdatedAt = now;

        // Credit tickets to wallet via WalletService (handles optimistic concurrency internally)
        await _walletService.AddTicketsAsync(customerId, membership.Plan.DailyBonusTickets, ct);

        await _db.SaveChangesAsync(ct);

        var wallet = await _walletService.GetWalletAsync(customerId, ct);
        var newBalance = wallet?.TicketBalance ?? 0;

        return new ClaimDailyBonusResponseDto(
            customerId,
            membership.Plan.DailyBonusTickets,
            newBalance,
            now
        );
    }

    public async Task CancelMembershipAsync(Guid customerId, CancellationToken ct = default)
    {
        var membership = await _db.Memberships
            .FirstOrDefaultAsync(m => m.CustomerId == customerId && m.Status == MembershipStatus.Active, ct);

        if (membership is null)
            throw new NotFoundException("No active membership found for this customer.");

        membership.Status = MembershipStatus.Cancelled;
        membership.AutoRenew = false;
        membership.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task ProcessExpiredMembershipsAsync(CancellationToken ct = default)
    {
        var expiredMemberships = await _db.Memberships
            .Where(m => m.Status == MembershipStatus.Active && m.EndDate < DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var membership in expiredMemberships)
        {
            if (membership.AutoRenew)
            {
                membership.EndDate = membership.EndDate.AddMonths(1);
            }
            else
            {
                membership.Status = MembershipStatus.Expired;
            }
            membership.UpdatedAt = DateTime.UtcNow;
        }

        if (expiredMemberships.Any())
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    private static MembershipPlanDto MapPlanToDto(MembershipPlan p) =>
        new(p.Id, p.Name, p.Description, p.MonthlyFee, p.DailyBonusTickets, p.GameplayDiscountPercent, p.IsActive, p.CreatedAt);

    private static MembershipDto MapMembershipToDto(Membership m) =>
        new(m.Id, m.CustomerId, m.PlanId, m.Plan?.Name ?? string.Empty, m.Status, m.StartDate, m.EndDate, m.AutoRenew, m.LastBonusClaimedAt, m.CreatedAt);
}

using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Interfaces;
using ArcadeOS.Api.Domain.Entities;
using ArcadeOS.Api.Domain.Enums;
using ArcadeOS.Api.Domain.Exceptions;
using ArcadeOS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArcadeOS.Api.Application.Services;

public class RewardService : IRewardService
{
    private readonly ArcadeDbContext _db;
    private readonly IWalletService _walletService;

    public RewardService(ArcadeDbContext db, IWalletService walletService)
    {
        _db = db;
        _walletService = walletService;
    }

    public async Task<RewardItemDto> CreateRewardAsync(CreateRewardItemDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ValidationException("Reward item name is required.");

        if (dto.TicketCost <= 0)
            throw new ValidationException("Ticket cost must be greater than zero.");

        if (dto.StockQuantity < 0)
            throw new ValidationException("Stock quantity cannot be negative.");

        var item = new RewardItem
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            TicketCost = dto.TicketCost,
            StockQuantity = dto.StockQuantity,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.RewardItems.Add(item);
        await _db.SaveChangesAsync(ct);

        return MapItemToDto(item);
    }

    public async Task<RewardItemDto> UpdateRewardAsync(Guid id, UpdateRewardItemDto dto, CancellationToken ct = default)
    {
        var item = await _db.RewardItems.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (item is null)
            throw new NotFoundException($"Reward item with ID '{id}' was not found.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ValidationException("Reward item name is required.");

        if (dto.TicketCost <= 0)
            throw new ValidationException("Ticket cost must be greater than zero.");

        if (dto.StockQuantity < 0)
            throw new ValidationException("Stock quantity cannot be negative.");

        item.Name = dto.Name.Trim();
        item.Description = dto.Description?.Trim() ?? string.Empty;
        item.TicketCost = dto.TicketCost;
        item.StockQuantity = dto.StockQuantity;
        item.IsActive = dto.IsActive;
        item.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return MapItemToDto(item);
    }

    public async Task<IEnumerable<RewardItemDto>> GetAvailableRewardsAsync(CancellationToken ct = default)
    {
        var items = await _db.RewardItems
            .AsNoTracking()
            .Where(r => r.IsActive && r.StockQuantity > 0)
            .OrderBy(r => r.TicketCost)
            .ToListAsync(ct);

        return items.Select(MapItemToDto);
    }

    public async Task<RedemptionDto> RedeemRewardAsync(RedeemRewardDto dto, CancellationToken ct = default)
    {
        // STEP 1: Idempotency Check
        if (!string.IsNullOrWhiteSpace(dto.ReferenceId))
        {
            var existingRedemption = await _db.Redemptions
                .Include(r => r.RewardItem)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ReferenceId == dto.ReferenceId, ct);

            if (existingRedemption is not null)
            {
                return MapRedemptionToDto(existingRedemption);
            }
        }

        // STEP 2: Load Reward Item & Check Stock
        var item = await _db.RewardItems.FirstOrDefaultAsync(r => r.Id == dto.RewardItemId && r.IsActive, ct);
        if (item is null)
            throw new NotFoundException($"Active reward item with ID '{dto.RewardItemId}' was not found.");

        if (item.StockQuantity <= 0)
            throw new ValidationException($"Reward item '{item.Name}' is out of stock.");

        // STEP 3: Deduct Tickets from Customer Wallet (optimistically concurrency managed inside WalletService)
        await _walletService.DeductTicketsAsync(dto.CustomerId, item.TicketCost, ct);

        // STEP 4: Decrement Inventory Stock & Record Redemption Record
        item.StockQuantity -= 1;
        item.UpdatedAt = DateTime.UtcNow;

        var redemption = new Redemption
        {
            Id = Guid.NewGuid(),
            CustomerId = dto.CustomerId,
            RewardItemId = item.Id,
            TicketsSpent = item.TicketCost,
            Status = RedemptionStatus.Pending,
            ReferenceId = dto.ReferenceId,
            RedeemedAtUtc = DateTime.UtcNow
        };

        _db.Redemptions.Add(redemption);

        // Save with Optimistic Concurrency Retry (in case another user bought the last item concurrently)
        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
                redemption.RewardItem = item;
                return MapRedemptionToDto(redemption);
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries - 1)
            {
                await ex.Entries.First().ReloadAsync(ct);
                var refreshedItem = (RewardItem)ex.Entries.First().Entity;

                if (refreshedItem.StockQuantity <= 0)
                    throw new ValidationException($"Reward item '{refreshedItem.Name}' went out of stock during processing.");

                refreshedItem.StockQuantity -= 1;
                refreshedItem.UpdatedAt = DateTime.UtcNow;
            }
        }

        throw new InvalidOperationException("Failed to complete reward redemption due to concurrency conflicts.");
    }

    public async Task<RedemptionDto> ClaimRedemptionAsync(Guid redemptionId, CancellationToken ct = default)
    {
        var redemption = await _db.Redemptions
            .Include(r => r.RewardItem)
            .FirstOrDefaultAsync(r => r.Id == redemptionId, ct);

        if (redemption is null)
            throw new NotFoundException($"Redemption record with ID '{redemptionId}' was not found.");

        if (redemption.Status == RedemptionStatus.Claimed)
            throw new ValidationException("Redemption has already been claimed.");

        if (redemption.Status == RedemptionStatus.Cancelled)
            throw new ValidationException("Cannot claim a cancelled redemption.");

        redemption.Status = RedemptionStatus.Claimed;
        redemption.ClaimedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return MapRedemptionToDto(redemption);
    }

    public async Task<IEnumerable<RedemptionDto>> GetCustomerRedemptionsAsync(Guid customerId, CancellationToken ct = default)
    {
        var redemptions = await _db.Redemptions
            .Include(r => r.RewardItem)
            .AsNoTracking()
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.RedeemedAtUtc)
            .ToListAsync(ct);

        return redemptions.Select(MapRedemptionToDto);
    }

    private static RewardItemDto MapItemToDto(RewardItem r) =>
        new(r.Id, r.Name, r.Description, r.TicketCost, r.StockQuantity, r.IsActive, r.CreatedAt, r.UpdatedAt);

    private static RedemptionDto MapRedemptionToDto(Redemption r) =>
        new(r.Id, r.CustomerId, r.RewardItemId, r.RewardItem?.Name ?? string.Empty, r.TicketsSpent, r.Status, r.ReferenceId, r.RedeemedAtUtc, r.ClaimedAtUtc);
}

using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Interfaces;
using ArcadeOS.Api.Domain.Entities;
using ArcadeOS.Api.Domain.Enums;
using ArcadeOS.Api.Domain.Exceptions;
using ArcadeOS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArcadeOS.Api.Application.Services;

public class GameplayService : IGameplayService
{
    private readonly ArcadeDbContext _db;
    private readonly ILogger<GameplayService> _logger;

    public GameplayService(ArcadeDbContext db, ILogger<GameplayService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PlayGameResponseDto> PlayGameAsync(PlayGameRequestDto dto, CancellationToken ct = default)
    {
        // ============================================================
        // STEP 1: IDEMPOTENCY CHECK
        // ============================================================
        var existingTransaction = await _db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ReferenceId == dto.ReferenceId, ct);

        if (existingTransaction is not null)
        {
            var cachedWallet = await _db.Wallets
                .AsNoTracking()
                .FirstAsync(w => w.Id == existingTransaction.WalletId, ct);

            // Fetch the machine to get the tickets awarded for the response
            var cachedMachine = await _db.Machines
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == dto.MachineId, ct);
                
            return new PlayGameResponseDto(
                existingTransaction.Id,
                existingTransaction.Amount,
                cachedMachine?.TicketPayout ?? 0,
                cachedWallet.Balance,
                cachedWallet.TicketBalance
            );
        }

        // ============================================================
        // STEP 2: LOAD ENTITIES
        // We load the Machine (read-only) and Wallet (tracked for update)
        // ============================================================
        var machine = await _db.Machines
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == dto.MachineId && m.IsActive, ct);

        if (machine is null)
            throw new NotFoundException($"Machine '{dto.MachineId}' not found or is inactive.");

        if (machine.Status != MachineStatus.Online)
            throw new ValidationException($"Machine '{machine.Name}' is currently {machine.Status}.");

        var wallet = await _db.Wallets
            .FirstOrDefaultAsync(w => w.CustomerId == dto.CustomerId, ct);

        if (wallet is null)
            throw new NotFoundException($"Wallet for customer '{dto.CustomerId}' not found.");

        if (wallet.Balance < machine.CreditCost)
            throw new InsufficientBalanceException(
                $"Insufficient balance. Needed: {machine.CreditCost:F2}, Available: {wallet.Balance:F2}");

        // ============================================================
        // STEP 3: PREPARE LEDGER & BALANCES
        // ============================================================
        var balanceBefore = wallet.Balance;
        var balanceAfter = balanceBefore - machine.CreditCost;

        wallet.Balance = balanceAfter;
        wallet.TicketBalance += machine.TicketPayout; // Award tickets immediately for this MVP
        wallet.UpdatedAt = DateTime.UtcNow;

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            Type = TransactionType.Debit,
            Amount = machine.CreditCost,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            ReferenceId = dto.ReferenceId,
            Description = $"Played {machine.Name}",
            CreatedAt = DateTime.UtcNow
        };

        _db.Transactions.Add(transaction);

        // ============================================================
        // STEP 4: SAVE WITH OPTIMISTIC CONCURRENCY RETRY
        // ============================================================
        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
                
                _logger.LogInformation("Customer {CustomerId} played {MachineId}. Debited {Cost}. Awarded {Tickets} tickets.",
                    dto.CustomerId, dto.MachineId, machine.CreditCost, machine.TicketPayout);

                return new PlayGameResponseDto(
                    transaction.Id,
                    machine.CreditCost,
                    machine.TicketPayout,
                    wallet.Balance,
                    wallet.TicketBalance
                );
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries - 1)
            {
                await ex.Entries.First().ReloadAsync(ct);
                var refreshedWallet = (Wallet)ex.Entries.First().Entity;

                if (refreshedWallet.Balance < machine.CreditCost)
                    throw new InsufficientBalanceException("Insufficient balance after concurrent update.");

                balanceBefore = refreshedWallet.Balance;
                balanceAfter = balanceBefore - machine.CreditCost;

                refreshedWallet.Balance = balanceAfter;
                refreshedWallet.TicketBalance += machine.TicketPayout;
                refreshedWallet.UpdatedAt = DateTime.UtcNow;

                transaction.BalanceBefore = balanceBefore;
                transaction.BalanceAfter = balanceAfter;
            }
        }

        throw new InvalidOperationException("Failed to complete Gameplay transaction due to concurrency conflicts.");
    }
}

using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Interfaces;
using ArcadeOS.Api.Domain.Entities;
using ArcadeOS.Api.Domain.Enums;
using ArcadeOS.Api.Domain.Exceptions;
using ArcadeOS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArcadeOS.Api.Application.Services;

public class WalletService : IWalletService
{
    private readonly ArcadeDbContext _db;

    public WalletService(ArcadeDbContext db)
    {
        _db = db;
    }

    public async Task<WalletDto?> GetWalletAsync(Guid customerId, CancellationToken ct = default)
    {
        var wallet = await _db.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.CustomerId == customerId, ct);

        return wallet is null ? null : MapToDto(wallet);
    }

    public async Task<TransactionDto> TopUpAsync(TopUpRequestDto dto, CancellationToken ct = default)
    {
        if (dto.Amount <= 0)
            throw new ValidationException("Amount must be greater than zero.");

        // ============================================================
        // STEP 1: IDEMPOTENCY CHECK
        // Before doing anything, check if we already processed this exact
        // request (identified by ReferenceId).
        //
        // Scenario: POS terminal posts TopUp → DB writes succeed → network
        // drops before response reaches POS → POS retries the same request.
        //
        // Without idempotency: the wallet gets double-charged.
        // With idempotency: we detect the duplicate ReferenceId and return
        // the ORIGINAL transaction — safe retry, no double charge.
        // ============================================================
        var existingTransaction = await _db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ReferenceId == dto.ReferenceId, ct);

        if (existingTransaction is not null)
        {
            // Already processed — return the cached result without touching the wallet
            return MapTransactionToDto(existingTransaction);
        }

        // ============================================================
        // STEP 2: LOAD THE WALLET
        // We do NOT use AsNoTracking() here because we NEED EF Core to
        // track the wallet entity so it can:
        //   a) Detect the RowVersion (xmin) for optimistic concurrency
        //   b) Send the UPDATE SQL when we call SaveChangesAsync()
        // ============================================================
        var wallet = await _db.Wallets
            .FirstOrDefaultAsync(w => w.CustomerId == dto.CustomerId, ct);

        if (wallet is null)
            throw new NotFoundException($"Wallet for customer '{dto.CustomerId}' was not found.");

        // ============================================================
        // STEP 3: CREATE LEDGER ENTRY
        // We snapshot the balance BEFORE and AFTER so we have an
        // immutable record of the wallet state at this exact moment.
        // This is the accounting principle of double-entry bookkeeping.
        // ============================================================
        var balanceBefore = wallet.Balance;
        var balanceAfter = balanceBefore + dto.Amount;

        // Update the wallet's cached balance
        wallet.Balance = balanceAfter;
        wallet.LifetimeCredits += dto.Amount; // LifetimeCredits only ever increases
        wallet.UpdatedAt = DateTime.UtcNow;

        // Write the immutable ledger record
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            Type = TransactionType.TopUp,
            Amount = dto.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            ReferenceId = dto.ReferenceId,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow
        };

        _db.Transactions.Add(transaction);

        // ============================================================
        // STEP 4: SAVE WITH OPTIMISTIC CONCURRENCY HANDLING
        //
        // WHY RETRY LOOP?
        // EF Core / PostgreSQL's xmin concurrency token means:
        // If two TopUp requests hit the same wallet simultaneously,
        // one will succeed and one will throw DbUpdateConcurrencyException.
        // The loop catches the exception, reloads the wallet with fresh data,
        // and retries. This prevents lost updates without database row locking.
        //
        // This is OPTIMISTIC CONCURRENCY — assume no conflict, handle it if it happens.
        // PESSIMISTIC CONCURRENCY would lock the row upfront — slower but simpler.
        // ============================================================
        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
                return MapTransactionToDto(transaction);
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries - 1)
            {
                // Reload the wallet entry with fresh data from DB
                await ex.Entries.First().ReloadAsync(ct);

                // Recalculate balances with the refreshed wallet state
                var refreshedWallet = (Wallet)ex.Entries.First().Entity;
                balanceBefore = refreshedWallet.Balance;
                balanceAfter = balanceBefore + dto.Amount;

                refreshedWallet.Balance = balanceAfter;
                refreshedWallet.LifetimeCredits += dto.Amount;
                refreshedWallet.UpdatedAt = DateTime.UtcNow;

                // Update the ledger entry with recalculated snapshots
                transaction.BalanceBefore = balanceBefore;
                transaction.BalanceAfter = balanceAfter;
            }
        }

        throw new InvalidOperationException("Failed to complete TopUp after multiple retries due to concurrency conflicts.");
    }

    public async Task<TransactionDto> DebitAsync(DebitRequestDto dto, CancellationToken ct = default)
    {
        if (dto.Amount <= 0)
            throw new ValidationException("Amount must be greater than zero.");

        // STEP 1: Idempotency check (same as TopUp)
        var existingTransaction = await _db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ReferenceId == dto.ReferenceId, ct);

        if (existingTransaction is not null)
            return MapTransactionToDto(existingTransaction);

        // STEP 2: Load the tracked wallet entity
        var wallet = await _db.Wallets
            .FirstOrDefaultAsync(w => w.CustomerId == dto.CustomerId, ct);

        if (wallet is null)
            throw new NotFoundException($"Wallet for customer '{dto.CustomerId}' was not found.");

        // ============================================================
        // STEP 3: BALANCE GUARD — PREVENT NEGATIVE BALANCE
        // This is a domain rule: you cannot spend what you don't have.
        // We check this BEFORE writing any ledger entry.
        // The DB also has a CHECK constraint (balance >= 0) as a safety net.
        // Never rely on application code alone for financial constraints.
        // ============================================================
        if (wallet.Balance < dto.Amount)
            throw new InsufficientBalanceException(
                $"Insufficient balance. Available: {wallet.Balance:F2}, Requested: {dto.Amount:F2}");

        var balanceBefore = wallet.Balance;
        var balanceAfter = balanceBefore - dto.Amount;

        wallet.Balance = balanceAfter;
        wallet.UpdatedAt = DateTime.UtcNow;
        // Note: LifetimeCredits does NOT change on debit — it only tracks credits added

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            Type = TransactionType.Debit,
            Amount = dto.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            ReferenceId = dto.ReferenceId,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow
        };

        _db.Transactions.Add(transaction);

        // STEP 4: Save with optimistic concurrency retry
        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
                return MapTransactionToDto(transaction);
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries - 1)
            {
                await ex.Entries.First().ReloadAsync(ct);
                var refreshedWallet = (Wallet)ex.Entries.First().Entity;

                // RE-CHECK BALANCE after reload — another debit may have already run!
                if (refreshedWallet.Balance < dto.Amount)
                    throw new InsufficientBalanceException(
                        $"Insufficient balance after concurrent update. Available: {refreshedWallet.Balance:F2}");

                balanceBefore = refreshedWallet.Balance;
                balanceAfter = balanceBefore - dto.Amount;

                refreshedWallet.Balance = balanceAfter;
                refreshedWallet.UpdatedAt = DateTime.UtcNow;

                transaction.BalanceBefore = balanceBefore;
                transaction.BalanceAfter = balanceAfter;
            }
        }

        throw new InvalidOperationException("Failed to complete Debit after multiple retries due to concurrency conflicts.");
    }

    public async Task<TransactionDto> RefundAsync(RefundRequestDto dto, CancellationToken ct = default)
    {
        if (dto.Amount <= 0)
            throw new ValidationException("Amount must be greater than zero.");

        var existingTransaction = await _db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ReferenceId == dto.ReferenceId, ct);

        if (existingTransaction is not null)
            return MapTransactionToDto(existingTransaction);

        var wallet = await _db.Wallets
            .FirstOrDefaultAsync(w => w.CustomerId == dto.CustomerId, ct);

        if (wallet is null)
            throw new NotFoundException($"Wallet for customer '{dto.CustomerId}' was not found.");

        var balanceBefore = wallet.Balance;
        var balanceAfter = balanceBefore + dto.Amount;

        wallet.Balance = balanceAfter;
        wallet.UpdatedAt = DateTime.UtcNow;

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            Type = TransactionType.Refund,
            Amount = dto.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            ReferenceId = dto.ReferenceId,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow
        };

        _db.Transactions.Add(transaction);

        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
                return MapTransactionToDto(transaction);
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries - 1)
            {
                await ex.Entries.First().ReloadAsync(ct);
                var refreshedWallet = (Wallet)ex.Entries.First().Entity;

                balanceBefore = refreshedWallet.Balance;
                balanceAfter = balanceBefore + dto.Amount;

                refreshedWallet.Balance = balanceAfter;
                refreshedWallet.UpdatedAt = DateTime.UtcNow;

                transaction.BalanceBefore = balanceBefore;
                transaction.BalanceAfter = balanceAfter;
            }
        }

        throw new InvalidOperationException("Failed to complete Refund after multiple retries due to concurrency conflicts.");
    }

    public async Task AddTicketsAsync(Guid customerId, int tickets, CancellationToken ct = default)
    {
        if (tickets <= 0) return;

        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.CustomerId == customerId, ct);
        if (wallet is null)
            throw new NotFoundException($"Wallet for customer '{customerId}' was not found.");

        wallet.TicketBalance += tickets;
        wallet.UpdatedAt = DateTime.UtcNow;

        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries - 1)
            {
                await ex.Entries.First().ReloadAsync(ct);
                var refreshedWallet = (Wallet)ex.Entries.First().Entity;
                refreshedWallet.TicketBalance += tickets;
                refreshedWallet.UpdatedAt = DateTime.UtcNow;
            }
        }

        throw new InvalidOperationException("Failed to add tickets due to concurrency conflicts.");
    }

    public async Task DeductTicketsAsync(Guid customerId, int tickets, CancellationToken ct = default)
    {
        if (tickets <= 0)
            throw new ValidationException("Tickets to deduct must be greater than zero.");

        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.CustomerId == customerId, ct);
        if (wallet is null)
            throw new NotFoundException($"Wallet for customer '{customerId}' was not found.");

        if (wallet.TicketBalance < tickets)
            throw new ValidationException($"Insufficient ticket balance. Required: {tickets}, Available: {wallet.TicketBalance}");

        wallet.TicketBalance -= tickets;
        wallet.UpdatedAt = DateTime.UtcNow;

        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries - 1)
            {
                await ex.Entries.First().ReloadAsync(ct);
                var refreshedWallet = (Wallet)ex.Entries.First().Entity;
                if (refreshedWallet.TicketBalance < tickets)
                    throw new ValidationException($"Insufficient ticket balance after concurrent update. Available: {refreshedWallet.TicketBalance}");

                refreshedWallet.TicketBalance -= tickets;
                refreshedWallet.UpdatedAt = DateTime.UtcNow;
            }
        }

        throw new InvalidOperationException("Failed to deduct tickets due to concurrency conflicts.");
    }

    public async Task<PagedResultDto<TransactionDto>> GetTransactionsAsync(
        TransactionListQueryDto query, CancellationToken ct = default)
    {
        // First, get the wallet for this customer
        var wallet = await _db.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.CustomerId == query.CustomerId, ct);

        if (wallet is null)
            throw new NotFoundException($"Wallet for customer '{query.CustomerId}' was not found.");

        var dbQuery = _db.Transactions
            .AsNoTracking()
            .Where(t => t.WalletId == wallet.Id);

        if (query.TypeFilter.HasValue)
            dbQuery = dbQuery.Where(t => t.Type == query.TypeFilter.Value);

        var total = await dbQuery.CountAsync(ct);

        var items = await dbQuery
            .OrderByDescending(t => t.CreatedAt) // Most recent transactions first
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => MapTransactionToDto(t))
            .ToListAsync(ct);

        return new PagedResultDto<TransactionDto>(items, query.Page, query.PageSize, total);
    }

    private static WalletDto MapToDto(Wallet w) =>
        new(w.Id, w.CustomerId, w.Balance, w.LifetimeCredits, w.TicketBalance, w.UpdatedAt);

    private static TransactionDto MapTransactionToDto(Transaction t) =>
        new(t.Id, t.WalletId, t.Type, t.Amount, t.BalanceBefore, t.BalanceAfter, t.ReferenceId, t.Description, t.CreatedAt);
}

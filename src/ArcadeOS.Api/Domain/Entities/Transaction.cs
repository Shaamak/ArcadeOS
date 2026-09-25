using ArcadeOS.Api.Domain.Enums;

namespace ArcadeOS.Api.Domain.Entities;

/// <summary>
/// A Transaction is a permanent, APPEND-ONLY record of every credit movement.
///
/// THE KEY INSIGHT — WHY NOT JUST UPDATE THE BALANCE?
/// Imagine a balance column on Wallet. UPDATE wallets SET balance = balance - 10.
/// Now: can you tell WHY the balance dropped? When? Who did it? Was it a refund?
/// No — you've destroyed that information forever.
///
/// The Ledger Pattern stores EVERY event. The Wallet.Balance is just a cached
/// total you can always recompute by summing Transactions. This is exactly
/// how bank account statements work.
///
/// IMMUTABILITY:
/// Once written, a transaction MUST NEVER be updated or deleted.
/// If a charge was wrong, you add a REFUND transaction. The audit trail is sacred.
///
/// IDEMPOTENCY via ReferenceId:
/// A client generating a TopUp sends a unique UUID as ReferenceId (e.g., a receipt ID).
/// If their network drops and they retry, we detect the same ReferenceId already exists
/// and return the original result — NO DOUBLE CHARGE.
/// This is critical for any payment system.
///
/// BalanceBefore / BalanceAfter:
/// Snapshots at the time of writing. Even if you later delete/restore other records,
/// you can always audit what the balance was at the exact moment of this transaction.
/// </summary>
public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // FK to Wallet — every transaction belongs to exactly one wallet
    public Guid WalletId { get; set; }

    // What kind of movement is this?
    public TransactionType Type { get; set; }

    // The credit amount (always POSITIVE — the Type field tells direction)
    // Using positive amounts makes reporting/aggregation simpler.
    public decimal Amount { get; set; }

    // Snapshots of wallet state — immutable audit points
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }

    // IDEMPOTENCY KEY: Client-supplied UUID. Unique constraint on DB.
    // If a TopUp is retried with the same ReferenceId, we return the cached result.
    // Null for system-generated transactions (Adjustments, Rewards).
    public string? ReferenceId { get; set; }

    // Human-readable description (e.g., "Credit top-up via POS terminal")
    public string? Description { get; set; }

    // Which staff member or system initiated this? Nullable = system/machine initiated.
    public Guid? CreatedByUserId { get; set; }

    // Immutable timestamp — NEVER update this after writing
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties for EF Core joins
    public Wallet Wallet { get; set; } = null!;
}

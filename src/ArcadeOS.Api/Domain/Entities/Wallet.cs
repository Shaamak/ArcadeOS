namespace ArcadeOS.Api.Domain.Entities;

/// <summary>
/// A Wallet is a 1:1 companion to a Customer.
/// It holds the CURRENT balance (a fast-read snapshot)
/// and a running ticket total from game play.
///
/// WHY SEPARATE FROM CUSTOMER?
/// Single Responsibility. Wallet logic (locking, concurrency, balance checks)
/// is complex enough to deserve its own entity. Mixing it into Customer
/// creates a "God Object" — one class doing too many things.
///
/// WHY `decimal` FOR BALANCE?
/// Never use `float` or `double` for money! They use binary floating-point
/// and cannot represent 0.10 exactly. 1.10 + 2.20 = 3.3000000000000003 in float.
/// `decimal` uses base-10 and is exact for financial calculations.
///
/// CONCURRENCY (RowVersion):
/// The `RowVersion` byte array is EF Core's optimistic concurrency token.
/// Every time a row is updated, Postgres increments this version.
/// If two requests try to update the same wallet simultaneously, the second
/// one will fail with a DbUpdateConcurrencyException because the version
/// it read is no longer current — giving us safe conflict detection without locking.
/// </summary>
public class Wallet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // FK to Customer — one wallet per customer, strictly enforced in DB
    public Guid CustomerId { get; set; }

    // The live credit balance. MUST be >= 0 (enforced in DB via CHECK constraint)
    public decimal Balance { get; set; } = 0;

    // Cumulative credits ever added (for analytics / loyalty tier calculation)
    public decimal LifetimeCredits { get; set; } = 0;

    // Ticket balance earned from playing games
    public int TicketBalance { get; set; } = 0;

    // Concurrency token — EF uses this to detect simultaneous updates (optimistic concurrency)
    public uint RowVersion { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property — lets EF Core join Customer data when needed
    public Customer Customer { get; set; } = null!;

    // Collection navigation — all transactions that belong to this wallet
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

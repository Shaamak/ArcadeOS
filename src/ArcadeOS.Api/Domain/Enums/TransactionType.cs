namespace ArcadeOS.Api.Domain.Enums;

/// <summary>
/// Describes the type of credit/ticket movement recorded in the ledger.
///
/// WHY USE AN ENUM STORED AS STRING?
/// We configure EF Core to store this as "TopUp", "Debit", etc. not 0, 1, 2.
/// This means you can read the DB directly and immediately understand each row.
/// Integer enums are a silent trap — reordering them in code silently corrupts
/// all historical DB rows.
/// </summary>
public enum TransactionType
{
    TopUp,       // Credits added to wallet (customer pays)
    Debit,       // Credits spent (machine/kiosk charges)
    Refund,      // Credits returned after a failed or cancelled play
    Adjustment,  // Manual correction by admin (over/under charge fix)
    Reward       // Bonus credits from membership or loyalty program
}

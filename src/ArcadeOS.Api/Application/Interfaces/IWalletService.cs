using ArcadeOS.Api.Application.DTOs;

namespace ArcadeOS.Api.Application.Interfaces;

public interface IWalletService
{
    /// <summary>Gets wallet info for a customer.</summary>
    Task<WalletDto?> GetWalletAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>
    /// Adds credits to a customer wallet. Idempotent via ReferenceId.
    /// Returns the transaction record for the top-up.
    /// </summary>
    Task<TransactionDto> TopUpAsync(TopUpRequestDto dto, CancellationToken ct = default);

    /// <summary>
    /// Deducts credits from a customer wallet. Idempotent via ReferenceId.
    /// Throws InsufficientBalanceException if balance would go negative.
    /// </summary>
    Task<TransactionDto> DebitAsync(DebitRequestDto dto, CancellationToken ct = default);

    /// <summary>Gets paginated transaction history for a customer's wallet.</summary>
    Task<PagedResultDto<TransactionDto>> GetTransactionsAsync(TransactionListQueryDto query, CancellationToken ct = default);
}

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
    
    /// <summary>
    /// Refunds credits back to a customer wallet. Idempotent via ReferenceId.
    /// </summary>
    Task<TransactionDto> RefundAsync(RefundRequestDto dto, CancellationToken ct = default);

    /// <summary>
    /// Adds tickets to a customer's wallet.
    /// </summary>
    Task AddTicketsAsync(Guid customerId, int tickets, CancellationToken ct = default);

    /// <summary>
    /// Deducts tickets from a customer's wallet. Throws ValidationException if tickets are insufficient.
    /// </summary>
    Task DeductTicketsAsync(Guid customerId, int tickets, CancellationToken ct = default);

    /// <summary>Gets paginated transaction history for a customer's wallet.</summary>
    Task<PagedResultDto<TransactionDto>> GetTransactionsAsync(TransactionListQueryDto query, CancellationToken ct = default);
}

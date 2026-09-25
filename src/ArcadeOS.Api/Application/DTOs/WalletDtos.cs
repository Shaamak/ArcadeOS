using ArcadeOS.Api.Domain.Enums;

namespace ArcadeOS.Api.Application.DTOs;

// --- Wallet DTOs ---

public record WalletDto(
    Guid Id,
    Guid CustomerId,
    decimal Balance,
    decimal LifetimeCredits,
    int TicketBalance,
    DateTime UpdatedAt
);

// --- Wallet Operations ---

/// <summary>
/// TopUp request: add credits to a wallet.
///
/// WHY `ReferenceId` IN THE REQUEST?
/// The client (POS terminal or kiosk) generates a unique UUID before sending the request.
/// If the network drops and they retry, we detect this same ReferenceId in the DB
/// and return the original result — preventing a double charge.
/// This is called IDEMPOTENCY and it's a critical concept in payment systems.
/// </summary>
public record TopUpRequestDto(
    Guid CustomerId,
    decimal Amount,
    string ReferenceId,        // Client-generated UUID for idempotency
    string? Description        // Optional note (e.g., "Top-up at main desk")
);

public record DebitRequestDto(
    Guid CustomerId,
    decimal Amount,
    string ReferenceId,        // Client-generated UUID for idempotency
    string? Description        // Optional note (e.g., "Played Pac-Man machine #3")
);

// --- Transaction DTOs ---

public record TransactionDto(
    Guid Id,
    Guid WalletId,
    TransactionType Type,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    string? ReferenceId,
    string? Description,
    DateTime CreatedAt
);

public record TransactionListQueryDto(
    Guid CustomerId,
    TransactionType? TypeFilter = null,
    int Page = 1,
    int PageSize = 20
);

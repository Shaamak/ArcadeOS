namespace ArcadeOS.Api.Application.DTOs;

public record PlayGameRequestDto(
    Guid CustomerId,
    Guid MachineId,
    string ReferenceId // Idempotency key from the kiosk/mobile app
);

public record PlayGameResponseDto(
    Guid TransactionId,
    decimal CreditsDeducted,
    int TicketsAwarded,
    decimal RemainingBalance,
    int TotalTickets
);

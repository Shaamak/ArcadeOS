using ArcadeOS.Api.Domain.Enums;

namespace ArcadeOS.Api.Application.DTOs;

public record MachineDto(
    Guid Id,
    string Name,
    string? Model,
    string? SerialNumber,
    decimal CreditCost,
    int TicketPayout,
    MachineStatus Status,
    string? QrCode,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateMachineDto(
    string Name,
    string? Model,
    string? SerialNumber,
    decimal CreditCost,
    int TicketPayout,
    string? QrCode
);

public record MachineHeartbeatDto(
    Guid MachineId,
    MachineStatus Status,
    decimal? CreditBalance,
    int? PlayCount,
    string? ErrorCode,
    string? PayloadJson
);

public record MachineListQueryDto(
    MachineStatus? StatusFilter = null,
    int Page = 1,
    int PageSize = 20
);

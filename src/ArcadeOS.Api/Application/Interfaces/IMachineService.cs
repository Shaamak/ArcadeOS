using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Domain.Enums;

namespace ArcadeOS.Api.Application.Interfaces;

public interface IMachineService
{
    Task<MachineDto> CreateMachineAsync(CreateMachineDto dto, CancellationToken ct = default);
    Task<MachineDto?> GetMachineByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResultDto<MachineDto>> GetMachinesAsync(MachineListQueryDto query, CancellationToken ct = default);
    
    /// <summary>
    /// Processes a heartbeat from a machine, updating its live status and logging the heartbeat data.
    /// </summary>
    Task ProcessHeartbeatAsync(MachineHeartbeatDto dto, CancellationToken ct = default);
    
    /// <summary>
    /// Used by the Background Worker. Flags any machines as 'Offline' if they haven't sent a heartbeat recently.
    /// </summary>
    Task MarkStaleMachinesOfflineAsync(TimeSpan timeout, CancellationToken ct = default);
}

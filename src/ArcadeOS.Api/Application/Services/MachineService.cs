using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Interfaces;
using ArcadeOS.Api.Domain.Entities;
using ArcadeOS.Api.Domain.Enums;
using ArcadeOS.Api.Domain.Exceptions;
using ArcadeOS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArcadeOS.Api.Application.Services;

public class MachineService : IMachineService
{
    private readonly ArcadeDbContext _db;
    private readonly ILogger<MachineService> _logger;

    public MachineService(ArcadeDbContext db, ILogger<MachineService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<MachineDto> CreateMachineAsync(CreateMachineDto dto, CancellationToken ct = default)
    {
        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Model = dto.Model?.Trim(),
            SerialNumber = dto.SerialNumber?.Trim(),
            CreditCost = dto.CreditCost,
            TicketPayout = dto.TicketPayout,
            QrCode = dto.QrCode?.Trim(),
            Status = MachineStatus.Offline, // Default to offline until first heartbeat
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Machines.Add(machine);
        await _db.SaveChangesAsync(ct);

        return MapToDto(machine);
    }

    public async Task<MachineDto?> GetMachineByIdAsync(Guid id, CancellationToken ct = default)
    {
        var machine = await _db.Machines
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && m.IsActive, ct);

        return machine is null ? null : MapToDto(machine);
    }

    public async Task<PagedResultDto<MachineDto>> GetMachinesAsync(MachineListQueryDto query, CancellationToken ct = default)
    {
        var dbQuery = _db.Machines
            .AsNoTracking()
            .Where(m => m.IsActive);

        if (query.StatusFilter.HasValue)
            dbQuery = dbQuery.Where(m => m.Status == query.StatusFilter.Value);

        var total = await dbQuery.CountAsync(ct);

        var items = await dbQuery
            .OrderBy(m => m.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(m => MapToDto(m))
            .ToListAsync(ct);

        return new PagedResultDto<MachineDto>(items, query.Page, query.PageSize, total);
    }

    public async Task ProcessHeartbeatAsync(MachineHeartbeatDto dto, CancellationToken ct = default)
    {
        var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Id == dto.MachineId, ct);
        if (machine is null)
            throw new NotFoundException($"Machine with ID {dto.MachineId} not found.");

        // 1. Log the heartbeat time-series data
        var heartbeat = new MachineHeartbeat
        {
            Id = Guid.NewGuid(),
            MachineId = machine.Id,
            Status = dto.Status,
            CreditBalance = dto.CreditBalance,
            PlayCount = dto.PlayCount,
            ErrorCode = dto.ErrorCode,
            PayloadJson = dto.PayloadJson,
            ReceivedAt = DateTime.UtcNow
        };
        _db.MachineHeartbeats.Add(heartbeat);

        // 2. Update the machine's current status, so the UI/API can query it instantly
        // without joining the heartbeat table.
        if (machine.Status != dto.Status)
        {
            _logger.LogInformation("Machine {MachineId} changed status from {OldStatus} to {NewStatus}", 
                machine.Id, machine.Status, dto.Status);
            machine.Status = dto.Status;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkStaleMachinesOfflineAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(timeout);

        // Find machines that are currently marked "Online" or "Error" but haven't sent a heartbeat since cutoffTime
        // Using a subquery on the MachineHeartbeats table to find the latest heartbeat
        var staleMachines = await _db.Machines
            .Where(m => m.IsActive && (m.Status == MachineStatus.Online || m.Status == MachineStatus.Error))
            .Where(m => !_db.MachineHeartbeats.Any(h => h.MachineId == m.Id && h.ReceivedAt >= cutoffTime))
            .ToListAsync(ct);

        if (staleMachines.Any())
        {
            _logger.LogWarning("Found {Count} stale machines. Marking them as Offline.", staleMachines.Count);
            foreach (var machine in staleMachines)
            {
                machine.Status = MachineStatus.Offline;
            }
            await _db.SaveChangesAsync(ct);
        }
    }

    private static MachineDto MapToDto(Machine m) =>
        new(m.Id, m.Name, m.Model, m.SerialNumber, m.CreditCost, m.TicketPayout, m.Status, m.QrCode, m.IsActive, m.CreatedAt);
}

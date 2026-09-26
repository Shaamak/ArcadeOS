using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Interfaces;
using ArcadeOS.Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArcadeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MachinesController : ControllerBase
{
    private readonly IMachineService _machineService;

    public MachinesController(IMachineService machineService)
    {
        _machineService = machineService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<PagedResultDto<MachineDto>>> GetMachines(
        [FromQuery] MachineStatus? statusFilter = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new MachineListQueryDto(statusFilter, page, pageSize);
        var result = await _machineService.GetMachinesAsync(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<MachineDto>> GetMachineById(Guid id, CancellationToken ct = default)
    {
        var machine = await _machineService.GetMachineByIdAsync(id, ct);
        if (machine is null)
            return NotFound(new { error = new { code = "NOT_FOUND", message = $"Machine with ID {id} not found." } });
            
        return Ok(machine);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<MachineDto>> CreateMachine(
        [FromBody] CreateMachineDto dto, CancellationToken ct = default)
    {
        var created = await _machineService.CreateMachineAsync(dto, ct);
        return CreatedAtAction(nameof(GetMachineById), new { id = created.Id }, created);
    }

    /// <summary>
    /// This endpoint is called by the physical arcade machines (IoT devices).
    /// They authenticate with a JWT that has the "Machine" role.
    /// </summary>
    [HttpPost("{id:guid}/heartbeat")]
    [Authorize(Roles = "Machine,Admin")]
    public async Task<IActionResult> PostHeartbeat(
        Guid id, [FromBody] MachineHeartbeatDto dto, CancellationToken ct = default)
    {
        // Security check: ensure the machine can only post heartbeats for itself
        // (Unless an Admin is testing it)
        if (dto.MachineId != id)
            return BadRequest(new { error = new { code = "INVALID_ID", message = "Machine ID mismatch." } });

        await _machineService.ProcessHeartbeatAsync(dto, ct);
        return Ok();
    }
}

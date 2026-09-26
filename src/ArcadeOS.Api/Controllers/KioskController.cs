using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArcadeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KioskController : ControllerBase
{
    private readonly IGameplayService _gameplayService;

    public KioskController(IGameplayService gameplayService)
    {
        _gameplayService = gameplayService;
    }

    /// <summary>
    /// POST /api/kiosk/play
    /// Represents a customer scanning their mobile app QR code (CustomerId) at a physical machine (MachineId).
    /// </summary>
    [HttpPost("play")]
    [Authorize(Roles = "Kiosk,Machine,Admin")]
    public async Task<ActionResult<PlayGameResponseDto>> PlayGame(
        [FromBody] PlayGameRequestDto dto, CancellationToken ct)
    {
        var result = await _gameplayService.PlayGameAsync(dto, ct);
        return Ok(result);
    }
}

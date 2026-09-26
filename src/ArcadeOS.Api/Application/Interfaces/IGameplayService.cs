using ArcadeOS.Api.Application.DTOs;

namespace ArcadeOS.Api.Application.Interfaces;

public interface IGameplayService
{
    /// <summary>
    /// Orchestrates a game play: debits wallet and awards tickets.
    /// This represents the Kiosk/Self-service flow.
    /// </summary>
    Task<PlayGameResponseDto> PlayGameAsync(PlayGameRequestDto dto, CancellationToken ct = default);
}

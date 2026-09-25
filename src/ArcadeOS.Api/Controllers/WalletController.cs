using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Interfaces;
using ArcadeOS.Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArcadeOS.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly IWalletService _walletService;

    public WalletController(IWalletService walletService)
    {
        _walletService = walletService;
    }

    /// <summary>
    /// GET /api/customers/{customerId}/wallet
    /// Returns the wallet balance and ticket count for a customer.
    /// </summary>
    [HttpGet("customers/{customerId:guid}/wallet")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<WalletDto>> GetWallet(Guid customerId, CancellationToken ct)
    {
        var wallet = await _walletService.GetWalletAsync(customerId, ct);
        if (wallet is null)
            return NotFound(new { error = new { code = "NOT_FOUND", message = "Wallet not found." } });

        return Ok(wallet);
    }

    /// <summary>
    /// POST /api/wallet/topup
    /// Adds credits to a customer wallet.
    /// Body must include a client-generated ReferenceId for idempotency.
    /// </summary>
    [HttpPost("wallet/topup")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<TransactionDto>> TopUp(
        [FromBody] TopUpRequestDto dto, CancellationToken ct)
    {
        var result = await _walletService.TopUpAsync(dto, ct);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/wallet/debit
    /// Deducts credits from a customer wallet.
    /// Returns 422 if insufficient balance.
    /// </summary>
    [HttpPost("wallet/debit")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<TransactionDto>> Debit(
        [FromBody] DebitRequestDto dto, CancellationToken ct)
    {
        var result = await _walletService.DebitAsync(dto, ct);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/customers/{customerId}/transactions?page=1&pageSize=20&typeFilter=TopUp
    /// Returns paginated transaction history for a customer.
    /// </summary>
    [HttpGet("customers/{customerId:guid}/transactions")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<PagedResultDto<TransactionDto>>> GetTransactions(
        Guid customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] TransactionType? typeFilter = null,
        CancellationToken ct = default)
    {
        var query = new TransactionListQueryDto(customerId, typeFilter, page, pageSize);
        var result = await _walletService.GetTransactionsAsync(query, ct);
        return Ok(result);
    }
}

using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArcadeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    /// <summary>
    /// GET /api/customers?page=1&pageSize=10&search=john
    /// Retrieves a paginated list of active customers.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<PagedResultDto<CustomerDto>>> GetCustomers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _customerService.GetCustomersAsync(page, pageSize, search, ct);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/customers/{id}
    /// Retrieves a customer by unique ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<CustomerDto>> GetCustomerById(Guid id, CancellationToken ct = default)
    {
        var customer = await _customerService.GetCustomerByIdAsync(id, ct);
        if (customer is null)
        {
            return NotFound(new { error = new { code = "NOT_FOUND", message = $"Customer with ID '{id}' was not found." } });
        }
        return Ok(customer);
    }

    /// <summary>
    /// POST /api/customers
    /// Creates a new customer record.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<CustomerDto>> CreateCustomer(
        [FromBody] CreateCustomerDto dto, CancellationToken ct = default)
    {
        var created = await _customerService.CreateCustomerAsync(dto, ct);
        return CreatedAtAction(nameof(GetCustomerById), new { id = created.Id }, created);
    }

    /// <summary>
    /// PUT /api/customers/{id}
    /// Updates an existing customer's information.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<CustomerDto>> UpdateCustomer(
        Guid id, [FromBody] UpdateCustomerDto dto, CancellationToken ct = default)
    {
        var updated = await _customerService.UpdateCustomerAsync(id, dto, ct);
        return Ok(updated);
    }

    /// <summary>
    /// DELETE /api/customers/{id}
    /// Soft-deletes a customer (Admin only).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCustomer(Guid id, CancellationToken ct = default)
    {
        var deleted = await _customerService.DeleteCustomerAsync(id, ct);
        if (!deleted)
        {
            return NotFound(new { error = new { code = "NOT_FOUND", message = $"Customer with ID '{id}' was not found." } });
        }
        return NoContent();
    }
}

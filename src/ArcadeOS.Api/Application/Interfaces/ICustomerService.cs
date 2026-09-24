using ArcadeOS.Api.Application.DTOs;

namespace ArcadeOS.Api.Application.Interfaces;

public interface ICustomerService
{
    Task<PagedResultDto<CustomerDto>> GetCustomersAsync(int page, int pageSize, string? search, CancellationToken ct = default);
    Task<CustomerDto?> GetCustomerByIdAsync(Guid id, CancellationToken ct = default);
    Task<CustomerDto> CreateCustomerAsync(CreateCustomerDto dto, CancellationToken ct = default);
    Task<CustomerDto?> UpdateCustomerAsync(Guid id, UpdateCustomerDto dto, CancellationToken ct = default);
    Task<bool> DeleteCustomerAsync(Guid id, CancellationToken ct = default);
}

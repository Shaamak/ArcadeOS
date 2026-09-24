using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Interfaces;
using ArcadeOS.Api.Domain.Entities;
using ArcadeOS.Api.Domain.Exceptions;
using ArcadeOS.Api.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using DomainValidationException = ArcadeOS.Api.Domain.Exceptions.ValidationException;

namespace ArcadeOS.Api.Application.Services;

public class CustomerService : ICustomerService
{
    private readonly ArcadeDbContext _db;
    private readonly IValidator<CreateCustomerDto> _createValidator;
    private readonly IValidator<UpdateCustomerDto> _updateValidator;

    public CustomerService(
        ArcadeDbContext db,
        IValidator<CreateCustomerDto> createValidator,
        IValidator<UpdateCustomerDto> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PagedResultDto<CustomerDto>> GetCustomersAsync(
        int page, int pageSize, string? search, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var query = _db.Customers
            .AsNoTracking()
            .Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.FirstName.ToLower().Contains(term) ||
                c.LastName.ToLower().Contains(term) ||
                c.Email.ToLower().Contains(term) ||
                (c.Phone != null && c.Phone.Contains(term)));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => MapToDto(c))
            .ToListAsync(ct);

        return new PagedResultDto<CustomerDto>(items, page, pageSize, totalCount);
    }

    public async Task<CustomerDto?> GetCustomerByIdAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive, ct);

        return customer is null ? null : MapToDto(customer);
    }

    public async Task<CustomerDto> CreateCustomerAsync(CreateCustomerDto dto, CancellationToken ct = default)
    {
        var validationResult = await _createValidator.ValidateAsync(dto, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            throw new DomainValidationException(errors);
        }

        var emailExists = await _db.Customers
            .AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower(), ct);

        if (emailExists)
        {
            throw new ConflictException($"A customer with email '{dto.Email}' already exists.");
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Email = dto.Email.Trim().ToLower(),
            Phone = dto.Phone?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);

        return MapToDto(customer);
    }

    public async Task<CustomerDto?> UpdateCustomerAsync(Guid id, UpdateCustomerDto dto, CancellationToken ct = default)
    {
        var validationResult = await _updateValidator.ValidateAsync(dto, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            throw new DomainValidationException(errors);
        }

        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive, ct);

        if (customer is null)
        {
            throw new NotFoundException($"Customer with ID '{id}' was not found.");
        }

        var emailConflict = await _db.Customers
            .AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower() && c.Id != id, ct);

        if (emailConflict)
        {
            throw new ConflictException($"A customer with email '{dto.Email}' already exists.");
        }

        customer.FirstName = dto.FirstName.Trim();
        customer.LastName = dto.LastName.Trim();
        customer.Email = dto.Email.Trim().ToLower();
        customer.Phone = dto.Phone?.Trim();
        customer.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return MapToDto(customer);
    }

    public async Task<bool> DeleteCustomerAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive, ct);

        if (customer is null)
        {
            return false;
        }

        // Soft delete
        customer.IsActive = false;
        customer.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static CustomerDto MapToDto(Customer c) =>
        new(c.Id, c.FirstName, c.LastName, c.Email, c.Phone, c.IsActive, c.CreatedAt, c.UpdatedAt);
}

using ArcadeOS.Api.Application.DTOs;
using ArcadeOS.Api.Application.Services;
using ArcadeOS.Api.Application.Validators;
using ArcadeOS.Api.Domain.Entities;
using ArcadeOS.Api.Domain.Exceptions;
using ArcadeOS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArcadeOS.UnitTests;

public class CustomerServiceTests
{
    private readonly CreateCustomerDtoValidator _createValidator = new();
    private readonly UpdateCustomerDtoValidator _updateValidator = new();

    private ArcadeDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ArcadeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ArcadeDbContext(options);
    }

    [Fact]
    public async Task CreateCustomerAsync_ValidDto_CreatesAndReturnsCustomer()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var service = new CustomerService(db, _createValidator, _updateValidator);
        var dto = new CreateCustomerDto("John", "Doe", "john.doe@example.com", "1234567890");

        // Act
        var result = await service.CreateCustomerAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("John", result.FirstName);
        Assert.Equal("Doe", result.LastName);
        Assert.Equal("john.doe@example.com", result.Email);
        Assert.True(result.IsActive);
        Assert.Equal(1, await db.Customers.CountAsync());
    }

    [Fact]
    public async Task CreateCustomerAsync_DuplicateEmail_ThrowsConflictException()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        db.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = "Jane",
            LastName = "Smith",
            Email = "duplicate@example.com",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = new CustomerService(db, _createValidator, _updateValidator);
        var dto = new CreateCustomerDto("John", "Doe", "DUPLICATE@example.com", null);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateCustomerAsync(dto));
    }

    [Fact]
    public async Task CreateCustomerAsync_InvalidDto_ThrowsValidationException()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var service = new CustomerService(db, _createValidator, _updateValidator);
        var dto = new CreateCustomerDto("", "", "not-an-email", null);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateCustomerAsync(dto));
    }

    [Fact]
    public async Task GetCustomersAsync_ReturnsActiveCustomersOnly()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        db.Customers.AddRange(
            new Customer { Id = Guid.NewGuid(), FirstName = "Active1", LastName = "User", Email = "a1@test.com", IsActive = true },
            new Customer { Id = Guid.NewGuid(), FirstName = "Inactive", LastName = "User", Email = "in@test.com", IsActive = false },
            new Customer { Id = Guid.NewGuid(), FirstName = "Active2", LastName = "User", Email = "a2@test.com", IsActive = true }
        );
        await db.SaveChangesAsync();

        var service = new CustomerService(db, _createValidator, _updateValidator);

        // Act
        var result = await service.GetCustomersAsync(1, 10, null);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.DoesNotContain(result.Items, c => c.Email == "in@test.com");
    }

    [Fact]
    public async Task GetCustomerByIdAsync_ExistingId_ReturnsCustomer()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var id = Guid.NewGuid();
        db.Customers.Add(new Customer { Id = id, FirstName = "Alice", LastName = "Bob", Email = "alice@test.com", IsActive = true });
        await db.SaveChangesAsync();

        var service = new CustomerService(db, _createValidator, _updateValidator);

        // Act
        var result = await service.GetCustomerByIdAsync(id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Alice", result.FirstName);
    }

    [Fact]
    public async Task DeleteCustomerAsync_ExistingCustomer_SoftDeletesCustomer()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var id = Guid.NewGuid();
        db.Customers.Add(new Customer { Id = id, FirstName = "ToDelete", LastName = "User", Email = "del@test.com", IsActive = true });
        await db.SaveChangesAsync();

        var service = new CustomerService(db, _createValidator, _updateValidator);

        // Act
        var success = await service.DeleteCustomerAsync(id);

        // Assert
        Assert.True(success);
        var deletedCustomer = await db.Customers.FindAsync(id);
        Assert.NotNull(deletedCustomer);
        Assert.False(deletedCustomer.IsActive); // Soft deleted flag
    }
}

namespace ArcadeOS.Api.Application.DTOs;

public record CustomerDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateCustomerDto(
    string FirstName,
    string LastName,
    string Email,
    string? Phone
);

public record UpdateCustomerDto(
    string FirstName,
    string LastName,
    string Email,
    string? Phone
);

public record PagedResultDto<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount
)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

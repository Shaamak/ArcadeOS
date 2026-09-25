namespace ArcadeOS.Api.Domain.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    // Overload for field-level validation errors (FluentValidation)
    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation failures occurred.")
    {
        Errors = errors;
    }

    // Overload for simple rule violations (e.g., "Amount must be > 0")
    public ValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]> { ["general"] = [message] };
    }
}

/// <summary>
/// Thrown when a Debit is attempted on a wallet with insufficient credits.
/// Mapped to HTTP 422 Unprocessable Entity — the request was valid in format,
/// but could not be completed due to business rule violation (not enough balance).
/// </summary>
public class InsufficientBalanceException : Exception
{
    public InsufficientBalanceException(string message) : base(message) { }
}

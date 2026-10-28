namespace MyApp.Domain.Common;

/// <summary>
/// An expected failure. Code is stable and machine-readable; Message is for humans.
/// Target is the input the error is about (a camelCase path such as "customerId" or "lines[0].sku"), or null when it is about no field in particular.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type, string? Target = null)
{
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
    public static Error Validation(string code, string message, string? target = null) => new(code, message, ErrorType.Validation, target);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
}

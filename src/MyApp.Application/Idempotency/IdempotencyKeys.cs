using MyApp.Domain.Common;

namespace MyApp.Application.Idempotency;

public static class IdempotencyKeys
{
    public const int MaxLength = 200;
    public const string HeaderName = "Idempotency-Key";

    /// <summary>Shape check for validators. A missing key is fine (the command just is not idempotent); a blank or oversized one is not.</summary>
    public static Error? Check(string? key)
    {
        if (key is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(key) || key.Length > MaxLength
            ? Error.Validation("Idempotency.KeyInvalid", $"{HeaderName} must be 1 to {MaxLength} characters.", HeaderName)
            : null;
    }
}

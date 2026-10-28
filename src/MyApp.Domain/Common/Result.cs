namespace MyApp.Domain.Common;

/// <summary>Outcome of an operation that can fail in an expected way. Exceptions are for bugs only.</summary>
public class Result : IResult<Result>
{
    private protected Result(IReadOnlyList<Error> errors) => Errors = errors;

    public bool IsSuccess => Errors.Count == 0;
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<Error> Errors { get; }

    public static Result Success() => new([]);
    public static Result Failure(Error error) => new([error]);
    public static Result Failure(IReadOnlyList<Error> errors) => new(RequireErrors(errors));

    public static Result<T> Success<T>(T value) => new(value, []);
    public static Result<T> Failure<T>(Error error) => new(default, [error]);

    static Result IResult<Result>.FromErrors(IReadOnlyList<Error> errors) => Failure(errors);

    private protected static IReadOnlyList<Error> RequireErrors(IReadOnlyList<Error> errors) =>
        errors.Count > 0 ? errors : throw new ArgumentException("A failure needs at least one error.", nameof(errors));
}

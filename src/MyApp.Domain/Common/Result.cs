namespace MyApp.Domain.Common;

/// <summary>Lets the pipeline build a failed result of any response type without reflection.</summary>
public interface IResult<TSelf> where TSelf : IResult<TSelf>
{
    bool IsSuccess { get; }
    IReadOnlyList<Error> Errors { get; }
    static abstract TSelf FromErrors(IReadOnlyList<Error> errors);
}

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

public sealed class Result<T> : Result, IResult<Result<T>>
{
    private readonly T? _value;

    internal Result(T? value, IReadOnlyList<Error> errors) : base(errors) => _value = value;

    /// <summary>Reading the value of a failed result is a bug, so it throws.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read Value of a failed result. Check IsSuccess first.");

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure<T>(error);

    static Result<T> IResult<Result<T>>.FromErrors(IReadOnlyList<Error> errors) => new(default, RequireErrors(errors));
}

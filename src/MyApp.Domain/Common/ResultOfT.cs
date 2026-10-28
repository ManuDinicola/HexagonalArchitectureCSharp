namespace MyApp.Domain.Common;

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

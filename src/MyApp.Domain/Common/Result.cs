namespace MyApp.Domain.Common;

/// <summary>
/// Outcome of an operation that can fail in an expected way: either <see cref="Succeeded"/> or <see cref="Failed"/>.
/// Exceptions are for bugs only. Use <see cref="Result{T}"/> when the operation returns a value.
/// </summary>
public union Result(Succeeded, Failed) : IResult<Result>
{
    public bool IsSuccess => Value is Succeeded;
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<Error> Errors => Value is Failed failed ? failed.Errors : [];

    public static Result Success() => new Succeeded();
    public static Result Failure(Error error) => new Failed([error]);
    public static Result Failure(IReadOnlyList<Error> errors) => new Failed(RequireErrors(errors));

    public static Result<T> Success<T>(T value) => value;
    public static Result<T> Failure<T>(Error error) => new Failed([error]);

    public static implicit operator Result(Error error) => Failure(error);

    static Result IResult<Result>.FromErrors(IReadOnlyList<Error> errors) => Failure(errors);

    internal static IReadOnlyList<Error> RequireErrors(IReadOnlyList<Error> errors) =>
        errors.Count > 0 ? errors : throw new ArgumentException("A failure needs at least one error.", nameof(errors));
}

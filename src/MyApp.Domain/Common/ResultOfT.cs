namespace MyApp.Domain.Common;

/// <summary>A value of type <typeparamref name="T"/>, or <see cref="Failed"/>. Pattern match it, or use <see cref="Unwrap"/>.</summary>
public union Result<T>(T, Failed) : IResult<Result<T>>
{
    public bool IsSuccess => Value is T;
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<Error> Errors => Value is Failed failed ? failed.Errors : [];

    /// <summary>Reading the value of a failed result is a bug, so it throws. Check <see cref="IsSuccess"/> first.</summary>
    public T Unwrap() => Value is T value
        ? value
        : throw new InvalidOperationException("Cannot unwrap a failed result. Check IsSuccess first.");

    public static implicit operator Result<T>(Error error) => new Failed([error]);

    static Result<T> IResult<Result<T>>.FromErrors(IReadOnlyList<Error> errors) => new Failed(Result.RequireErrors(errors));
}

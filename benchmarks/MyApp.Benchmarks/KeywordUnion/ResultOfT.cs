namespace MyApp.Benchmarks.KeywordUnion;

/// <summary>A value of type <typeparamref name="T"/>, or <see cref="Failed"/>. Pattern match it. <see cref="Unwrap"/> is for tests only.</summary>
public union Result<T>(T, Failed) : IResult<Result<T>>
{
    public bool IsSuccess => Value is T;
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<Error> Errors => Value is Failed failed ? failed.Errors : [];

    /// <summary>Tests only (banned in src). Reading the value of a failed result is a bug, so it throws.</summary>
    public T Unwrap() => Value is T value
        ? value
        : throw new InvalidOperationException("Cannot unwrap a failed result. Check IsSuccess first.");

    public static implicit operator Result<T>(Error error) => new Failed([error]);

    static Result<T> IResult<Result<T>>.FromErrors(IReadOnlyList<Error> errors) => new Failed(Result.RequireErrors(errors));
}

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace MyApp.Benchmarks.HandwrittenUnion;

/// <summary>
/// Outcome of an operation that can fail in an expected way: either <see cref="Succeeded"/> or <see cref="Failed"/>.
/// Exceptions are for bugs only. Use <see cref="Result{T}"/> when the operation returns a value.
/// </summary>
/// <remarks>
/// A hand-written C# 15 union (<see cref="UnionAttribute"/>) instead of the <c>union</c> keyword, which would box every case in one <c>object</c> field.
/// <c>default(Result)</c> is neither case: <see cref="IsSuccess"/> is false, <see cref="Errors"/> is empty and <see cref="HasValue"/> is false.
/// Once a type is a union, property patterns such as <c>{ IsSuccess: true }</c> apply to <see cref="Value"/>, not to the result: match on the cases instead.
/// </remarks>
[Union]
public readonly struct Result : IUnion, IResult<Result>
{
    private enum State : byte
    {
        Empty = 0,
        Succeeded,
        Failed,
    }

    private readonly Failed? _failed;
    private readonly State _state;

    public Result(Succeeded succeeded)
    {
        _failed = null;
        _state = succeeded is null ? State.Empty : State.Succeeded;
    }

    public Result(Failed failed)
    {
        _failed = failed;
        _state = failed is null ? State.Empty : State.Failed;
    }

    /// <summary>The current case boxed as an object, or null for <c>default</c>. Reading it allocates only for value-type cases; here both cases are references.</summary>
    public object? Value => _state switch
    {
        State.Succeeded => Succeeded.Instance,
        State.Failed => _failed,
        _ => null,
    };

    public bool HasValue => _state != State.Empty;

    public bool IsSuccess => _state == State.Succeeded;
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<Error> Errors => _state == State.Failed ? _failed!.Errors : [];

    public bool TryGetValue([MaybeNullWhen(false)] out Succeeded value)
    {
        value = IsSuccess ? Succeeded.Instance : null;
        return value is not null;
    }

    public bool TryGetValue([MaybeNullWhen(false)] out Failed value)
    {
        value = _state == State.Failed ? _failed : null;
        return value is not null;
    }

    public static Result Success() => new(Succeeded.Instance);
    public static Result<T> Success<T>(T value) => new(value);

    public static Result Failure(Error error) => new(new Failed([error]));
    public static Result Failure(IReadOnlyList<Error> errors) => new(new Failed(RequireErrors(errors)));
    public static Result<T> Failure<T>(Error error) => new(new Failed([error]));
    public static Result<T> Failure<T>(IReadOnlyList<Error> errors) => new(new Failed(RequireErrors(errors)));

    public static implicit operator Result(Error error) => Failure(error);

    static Result IResult<Result>.FromErrors(IReadOnlyList<Error> errors) => Failure(errors);

    internal static IReadOnlyList<Error> RequireErrors(IReadOnlyList<Error> errors) =>
        errors.Count > 0 ? errors : throw new ArgumentException("A failure needs at least one error.", nameof(errors));
}

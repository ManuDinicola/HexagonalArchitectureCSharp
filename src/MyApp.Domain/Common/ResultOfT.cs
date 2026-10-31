using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace MyApp.Domain.Common;

/// <summary>A value of type <typeparamref name="T"/>, or <see cref="Failed"/>. Pattern match it. <see cref="Unwrap"/> is for tests only.</summary>
/// <remarks>
/// A hand-written C# 15 union (<see cref="UnionAttribute"/>): one typed field per case, so a value-type <typeparamref name="T"/> is never boxed
/// unless someone reads <see cref="Value"/>. <c>default(Result&lt;T&gt;)</c> is neither case: <see cref="IsSuccess"/> is false, <see cref="Errors"/> is empty.
/// A null <typeparamref name="T"/> is not a case either (it behaves like <c>default</c>), as with the keyword form.
/// Once a type is a union, property patterns such as <c>{ IsSuccess: true }</c> apply to <see cref="Value"/>, not to the result: match on the cases instead.
/// </remarks>
[Union]
public readonly struct Result<T> : IUnion, IResult<Result<T>>
{
    private enum State : byte
    {
        Empty = 0,
        Success,
        Failed,
    }

    private readonly T? _value;
    private readonly Failed? _failed;
    private readonly State _state;

    public Result(T value)
    {
        _value = value;
        _failed = null;
        _state = value is null ? State.Empty : State.Success;
    }

    public Result(Failed failed)
    {
        _value = default;
        _failed = failed;
        _state = failed is null ? State.Empty : State.Failed;
    }

    /// <summary>The current case boxed as an object, or null for <c>default</c>. Reading it boxes a value-type <typeparamref name="T"/>; the pattern matching members do not.</summary>
    public object? Value => _state switch
    {
        State.Success => _value,
        State.Failed => _failed,
        _ => null,
    };

    public bool HasValue => _state != State.Empty;

    public bool IsSuccess => _state == State.Success;
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<Error> Errors => _state == State.Failed ? _failed!.Errors : [];

    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _state == State.Success ? _value : default;
        return _state == State.Success;
    }

    public bool TryGetValue([MaybeNullWhen(false)] out Failed value)
    {
        value = _state == State.Failed ? _failed : null;
        return _state == State.Failed;
    }

    /// <summary>Tests only (banned in src). Reading the value of a failed result is a bug, so it throws.</summary>
    public T Unwrap() => _state == State.Success
        ? _value!
        : throw new InvalidOperationException("Cannot unwrap a failed result. Check IsSuccess first.");

    public static implicit operator Result<T>(Error error) => new(new Failed([error]));

    static Result<T> IResult<Result<T>>.FromErrors(IReadOnlyList<Error> errors) => new(new Failed(Result.RequireErrors(errors)));
}

using System.Runtime.CompilerServices;
using MyApp.Domain.Common;

namespace MyApp.Domain.Tests;

/// <summary>
/// Result and Result&lt;T&gt; are hand-written [Union] structs. These tests pin the contract the compiler assumes
/// (Value / HasValue / TryGetValue agree) and that the success path of a value-type case never boxes.
/// Do not use property patterns such as <c>{ IsSuccess: true }</c> on a result: on a union they apply to Value, not to the result.
/// </summary>
public class ResultUnionTests
{
    private static readonly Error Boom = Error.Failure("Boom", "boom");

    [Fact]
    public void Types_are_unions()
    {
        typeof(Result).IsDefined(typeof(UnionAttribute), inherit: false).ShouldBeTrue();
        typeof(Result<int>).IsDefined(typeof(UnionAttribute), inherit: false).ShouldBeTrue();
        typeof(IUnion).IsAssignableFrom(typeof(Result)).ShouldBeTrue();
        typeof(IUnion).IsAssignableFrom(typeof(Result<int>)).ShouldBeTrue();
    }

    [Fact]
    public void Generic_result_matches_on_its_cases()
    {
        Describe(Result.Success(7)).ShouldBe("value 7");
        Describe(Result.Failure<int>(Boom)).ShouldBe("failed Boom");
    }

    [Fact]
    public void Void_result_matches_on_its_cases()
    {
        Describe(Result.Success()).ShouldBe("succeeded");
        Describe(Result.Failure(Boom)).ShouldBe("failed Boom");
    }

    [Fact]
    public void Negated_case_pattern_works()
    {
        Result<string> failed = Boom;

        (failed is not string).ShouldBeTrue();
        (Result.Success("x") is string text && string.Equals(text, "x", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Fact]
    public void Value_converts_implicitly()
    {
        Result<Guid> id = Guid.Empty;
        Result<int> number = 3;

        id.IsSuccess.ShouldBeTrue();
        number.Unwrap().ShouldBe(3);
    }

    [Fact]
    public void Failed_and_error_convert_implicitly()
    {
        Result<int> fromFailed = new Failed([Boom]);
        Result<int> fromError = Boom;
        Result fromVoidFailed = new Failed([Boom]);
        Result fromVoidError = Boom;
        Result fromSucceeded = Succeeded.Instance;

        fromFailed.Errors.ShouldHaveSingleItem().ShouldBe(Boom);
        fromError.Errors.ShouldHaveSingleItem().ShouldBe(Boom);
        fromVoidFailed.Errors.ShouldHaveSingleItem().ShouldBe(Boom);
        fromVoidError.Errors.ShouldHaveSingleItem().ShouldBe(Boom);
        fromSucceeded.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Default_is_neither_case()
    {
        Result<int> generic = default;
        Result plain = default;

        generic.IsSuccess.ShouldBeFalse();
        generic.IsFailure.ShouldBeTrue();
        generic.Errors.ShouldBeEmpty();
        generic.HasValue.ShouldBeFalse();
        generic.Value.ShouldBeNull();
        generic.TryGetValue(out int _).ShouldBeFalse();
        generic.TryGetValue(out Failed? _).ShouldBeFalse();

        plain.IsSuccess.ShouldBeFalse();
        plain.IsFailure.ShouldBeTrue();
        plain.Errors.ShouldBeEmpty();
        plain.HasValue.ShouldBeFalse();
        plain.Value.ShouldBeNull();
    }

    [Fact]
    public void Null_case_value_behaves_like_default()
    {
        Result<string> result = new((string)null!);

        result.HasValue.ShouldBeFalse();
        result.Value.ShouldBeNull();
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Unwrap_returns_the_value_and_throws_on_failure_and_default()
    {
        Result.Success("x").Unwrap().ShouldBe("x");
        Should.Throw<InvalidOperationException>(() => Result.Failure<string>(Boom).Unwrap());
        Should.Throw<InvalidOperationException>(() => default(Result<string>).Unwrap());
    }

    [Fact]
    public void Value_has_value_and_try_get_value_agree_for_the_success_case()
    {
        var result = Result.Success(5);

        result.HasValue.ShouldBeTrue();
        result.Value.ShouldBe(5);
        result.TryGetValue(out int value).ShouldBeTrue();
        value.ShouldBe(5);
        result.TryGetValue(out Failed? failed).ShouldBeFalse();
        failed.ShouldBeNull();
    }

    [Fact]
    public void Value_has_value_and_try_get_value_agree_for_the_failed_case()
    {
        var result = Result.Failure<int>(Boom);

        result.HasValue.ShouldBeTrue();
        result.Value.ShouldBeOfType<Failed>().Errors.ShouldHaveSingleItem().ShouldBe(Boom);
        result.TryGetValue(out Failed? failed).ShouldBeTrue();
        failed.Errors.ShouldHaveSingleItem().ShouldBe(Boom);
        result.TryGetValue(out int _).ShouldBeFalse();
    }

    [Fact]
    public void Void_result_value_has_value_and_try_get_value_agree()
    {
        var ok = Result.Success();
        var bad = Result.Failure(Boom);

        ok.HasValue.ShouldBeTrue();
        ok.Value.ShouldBeSameAs(Succeeded.Instance);
        ok.TryGetValue(out Succeeded? succeeded).ShouldBeTrue();
        succeeded.ShouldBeSameAs(Succeeded.Instance);
        ok.TryGetValue(out Failed? _).ShouldBeFalse();

        bad.HasValue.ShouldBeTrue();
        bad.Value.ShouldBeOfType<Failed>();
        bad.TryGetValue(out Failed? failed).ShouldBeTrue();
        failed.Errors.ShouldHaveSingleItem().ShouldBe(Boom);
        bad.TryGetValue(out Succeeded? _).ShouldBeFalse();
    }

    [Fact]
    public void Success_shares_one_succeeded_instance()
    {
        Result.Success().Value.ShouldBeSameAs(Result.Success().Value);
    }

    [Fact]
    public void Failure_without_errors_is_rejected_for_every_factory()
    {
        Should.Throw<ArgumentException>(() => Result.Failure<int>([]));
        Should.Throw<ArgumentException>(() => Result.Failure([]));
        Should.Throw<ArgumentException>(() => FromErrors<Result>([]));
        Should.Throw<ArgumentException>(() => FromErrors<Result<int>>([]));
        FromErrors<Result<int>>([Boom]).Errors.ShouldHaveSingleItem().ShouldBe(Boom);
    }

    [Fact]
    public void Try_get_value_with_nullable_enabled_needs_no_suppression()
    {
        // Compiles without CS8602 / CS8600 (warnings are errors): MaybeNullWhen(false) flows through direct members.
        var result = Result.Success("text");

        if (result.TryGetValue(out string? text))
        {
            text.Length.ShouldBe(4);
        }

        var failed = Result.Failure<string>(Boom);
        if (failed.TryGetValue(out Failed? failure))
        {
            failure.Errors.Count.ShouldBe(1);
        }
    }

    [Fact]
    public void Try_get_value_does_not_box_a_value_type_case()
    {
        const int iterations = 100_000;
        var result = Result.Success(Guid.CreateVersion7());
        var failed = Result.Failure<Guid>(Boom);
        var sum = 0;

        sum += Consume(result, failed); // warm up (JIT, static state) before measuring

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            sum += Consume(result, failed);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        sum.ShouldBePositive();
        allocated.ShouldBeLessThan(iterations); // boxing a Guid would allocate 32 bytes per call (3.2 MB here)
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Consume(Result<Guid> ok, Result<Guid> bad)
    {
        var count = 0;
        if (ok.TryGetValue(out Guid id) && id != Guid.Empty)
        {
            count++;
        }

        if (bad.TryGetValue(out Failed? failure) && failure.Errors.Count > 0)
        {
            count++;
        }

        return count;
    }

    private static TResult FromErrors<TResult>(IReadOnlyList<Error> errors) where TResult : IResult<TResult> => TResult.FromErrors(errors);

    private static string Describe(Result<int> result) => result switch
    {
        Failed f => $"failed {f.Errors[0].Code}",
        int value => $"value {value}",
    };

    private static string Describe(Result result) => result switch
    {
        Failed f => $"failed {f.Errors[0].Code}",
        Succeeded => "succeeded",
    };
}

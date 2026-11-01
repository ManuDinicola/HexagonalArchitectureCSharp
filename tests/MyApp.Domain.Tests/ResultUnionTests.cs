using MyApp.Domain.Common;

namespace MyApp.Domain.Tests;

/// <summary>
/// Result and Result&lt;T&gt; are C# 15 unions declared with the <c>union</c> keyword. Pin how callers use them.
/// Do not use property patterns such as <c>{ IsSuccess: true }</c> on a result: on a union they apply to Value, not to the result.
/// The hand-written [Union] alternative was measured and rejected, see docs/benchmarks.md.
/// </summary>
public class ResultUnionTests
{
    private static readonly Error Boom = Error.Failure("Boom", "boom");

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
    public void Failed_and_error_convert_implicitly()
    {
        Result<int> fromFailed = new Failed([Boom]);
        Result<int> fromError = Boom;
        Result fromVoidFailed = new Failed([Boom]);
        Result fromVoidError = Boom;
        Result fromSucceeded = new Succeeded();

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
        plain.IsSuccess.ShouldBeFalse();
        plain.IsFailure.ShouldBeTrue();
        plain.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Unwrap_returns_the_value_and_throws_on_failure_and_default()
    {
        Result.Success("x").Unwrap().ShouldBe("x");
        Should.Throw<InvalidOperationException>(() => Result.Failure<string>(Boom).Unwrap());
        Should.Throw<InvalidOperationException>(() => default(Result<string>).Unwrap());
    }

    [Fact]
    public void Failure_without_errors_is_rejected_for_every_factory()
    {
        Should.Throw<ArgumentException>(() => Result.Failure([]));
        Should.Throw<ArgumentException>(() => Result.Failure<int>([]));
        Should.Throw<ArgumentException>(() => FromErrors<Result>([]));
        Should.Throw<ArgumentException>(() => FromErrors<Result<int>>([]));
        FromErrors<Result<int>>([Boom]).Errors.ShouldHaveSingleItem().ShouldBe(Boom);
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

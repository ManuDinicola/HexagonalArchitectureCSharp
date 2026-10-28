using MyApp.Domain.Common;

namespace MyApp.Domain.Tests;

public class ResultTests
{
    private static readonly Error Boom = Error.Failure("Boom", "boom");

    [Fact]
    public void Success_has_value_and_no_errors()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Failure_exposes_errors_and_value_access_is_a_bug()
    {
        Result<int> result = Boom;

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().ShouldBe(Boom);
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Failure_without_errors_is_rejected()
    {
        Should.Throw<ArgumentException>(() => Result.Failure([]));
    }
}

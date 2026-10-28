using MyApp.Domain.Common;

namespace MyApp.Domain.Tests;

public class ResultTests
{
    private static readonly Error Boom = Error.Failure("Boom", "boom");

    [Fact]
    public void Success_has_value_and_no_errors()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Failure_exposes_errors_and_value_access_is_a_bug()
    {
        Result<int> result = Boom;

        Assert.True(result.IsFailure);
        Assert.Equal(Boom, Assert.Single(result.Errors));
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Failure_without_errors_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure([]));
    }
}

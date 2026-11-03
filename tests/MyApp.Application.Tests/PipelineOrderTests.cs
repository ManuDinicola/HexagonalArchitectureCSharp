using MyApp.Application.UseCases.CancelOrder;
using MyApp.Application.UseCases.GetOrder;
using MyApp.Application.UseCases.PlaceOrder;
using MyApp.Domain.Common;

namespace MyApp.Application.Tests;

/// <summary>The order of the behaviors is the registration order in AddApplication(). Pin it: reordering changes what the pipeline guarantees.</summary>
public sealed class PipelineOrderTests : IAsyncDisposable
{
    private readonly TestApp _app = new();

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    [Fact]
    public void A_command_is_traced_logged_validated_saved_and_made_idempotent_innermost()
    {
        _app.PipelineOf<PlaceOrderCommand, Result<Guid>>()
            .ShouldBe(["TelemetryBehavior`2", "LoggingBehavior`2", "ValidationBehavior`2", "UnitOfWorkBehavior`2", "IdempotencyBehavior`2"]);
    }

    [Fact]
    public void A_command_that_is_not_idempotent_has_no_idempotency_step()
    {
        _app.PipelineOf<CancelOrderCommand, Result>()
            .ShouldBe(["TelemetryBehavior`2", "LoggingBehavior`2", "ValidationBehavior`2", "UnitOfWorkBehavior`2"]);
    }

    [Fact]
    public void A_query_is_logged_and_validated_but_never_saved()
    {
        _app.PipelineOf<GetOrderQuery, Result<OrderDto>>()
            .ShouldBe(["TelemetryBehavior`2", "LoggingBehavior`2", "ValidationBehavior`2"]);
    }
}

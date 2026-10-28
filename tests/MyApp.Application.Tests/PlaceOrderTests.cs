using MyApp.Application.UseCases.GetOrder;
using MyApp.Application.UseCases.PlaceOrder;
using MyApp.Domain.Common;

namespace MyApp.Application.Tests;

public class PlaceOrderTests : IAsyncLifetime
{
    private readonly TestApp _app = new();

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _app.DisposeAsync();

    private static PlaceOrderCommand ValidCommand() =>
        new("cust-1", [new PlaceOrderLine("SKU-1", 2, 10m), new PlaceOrderLine("SKU-2", 1, 5m)]);

    [Fact]
    public async Task Valid_command_stores_order_stamped_with_the_clock()
    {
        var result = await _app.SendAsync(ValidCommand());

        Assert.True(result.IsSuccess);
        var order = Assert.Single(_app.Orders.Stored.Values);
        Assert.Equal(result.Value, order.Id);
        Assert.Equal(_app.Clock.UtcNow, order.PlacedAt);
        Assert.Equal(25m, order.Total);
    }

    [Fact]
    public async Task Invalid_command_is_rejected_by_validation_and_never_reaches_the_handler()
    {
        var result = await _app.SendAsync(new PlaceOrderCommand("", [new PlaceOrderLine("", 0, -1m)]));

        Assert.True(result.IsFailure);
        Assert.All(result.Errors, e => Assert.Equal(ErrorType.Validation, e.Type));
        Assert.Contains(result.Errors, e => e.Code == "CustomerId");
        Assert.Contains(result.Errors, e => e.Code == "Lines[0].Quantity");
        Assert.Empty(_app.Orders.Stored);
    }

    [Fact]
    public async Task Logging_is_outermost_so_it_also_sees_validation_failures()
    {
        await _app.SendAsync(new PlaceOrderCommand("", []));

        Assert.Equal("Information: Handling PlaceOrderCommand", _app.Logs.Messages.First());
        Assert.Contains(_app.Logs.Messages, m => m.StartsWith("Warning: PlaceOrderCommand failed") && m.Contains("CustomerId"));
    }

    [Fact]
    public async Task Placed_order_can_be_queried_and_comes_back_as_a_dto()
    {
        var id = (await _app.SendAsync(ValidCommand())).Value;

        var result = await _app.SendAsync(new GetOrderQuery(id));

        Assert.True(result.IsSuccess);
        Assert.Equal("cust-1", result.Value.CustomerId);
        Assert.Equal(2, result.Value.Lines.Count);
        Assert.Equal(25m, result.Value.Total);
    }

    [Fact]
    public async Task Unknown_order_is_an_expected_not_found_failure()
    {
        var result = await _app.SendAsync(new GetOrderQuery(Guid.NewGuid()));

        Assert.Equal(ErrorType.NotFound, Assert.Single(result.Errors).Type);
    }
}

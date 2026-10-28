using FakeItEasy;
using MyApp.Application.UseCases.GetOrder;
using MyApp.Application.UseCases.PlaceOrder;
using MyApp.Domain.Common;
using MyApp.Domain.Orders;

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

        result.IsSuccess.ShouldBeTrue();
        A.CallTo(() => _app.Orders.AddAsync(A<Order>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        var order = _app.Saved.ShouldHaveSingleItem();
        order.Id.ShouldBe(result.Unwrap());
        order.PlacedAt.ShouldBe(TestApp.Now);
        order.Total.ShouldBe(25m);
    }

    [Fact]
    public async Task Invalid_command_is_rejected_by_validation_and_never_reaches_the_handler()
    {
        var result = await _app.SendAsync(new PlaceOrderCommand("", [new PlaceOrderLine("", 0, -1m)]));

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldAllBe(e => e.Type == ErrorType.Validation);
        result.Errors.ShouldContain(e => e.Code == "CustomerId");
        result.Errors.ShouldContain(e => e.Code == "Lines[0].Quantity");
        A.CallTo(() => _app.Orders.AddAsync(A<Order>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Logging_is_outermost_so_it_also_sees_validation_failures()
    {
        await _app.SendAsync(new PlaceOrderCommand("", []));

        _app.Logs.Messages.First().ShouldBe("Information: Handling PlaceOrderCommand");
        _app.Logs.Messages.ShouldContain(m => m.StartsWith("Warning: PlaceOrderCommand failed") && m.Contains("CustomerId"));
    }

    [Fact]
    public async Task Placed_order_can_be_queried_and_comes_back_as_a_dto()
    {
        var id = (await _app.SendAsync(ValidCommand())).Unwrap();

        var result = await _app.SendAsync(new GetOrderQuery(id));

        result.IsSuccess.ShouldBeTrue();
        result.Unwrap().CustomerId.ShouldBe("cust-1");
        result.Unwrap().Lines.Count.ShouldBe(2);
        result.Unwrap().Total.ShouldBe(25m);
    }

    [Fact]
    public async Task Unknown_order_is_an_expected_not_found_failure()
    {
        var result = await _app.SendAsync(new GetOrderQuery(Guid.NewGuid()));

        result.Errors.ShouldHaveSingleItem().Type.ShouldBe(ErrorType.NotFound);
    }
}

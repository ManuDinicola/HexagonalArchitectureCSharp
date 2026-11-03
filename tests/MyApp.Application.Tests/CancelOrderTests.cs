using FakeItEasy;
using MyApp.Application.UseCases.CancelOrder;
using MyApp.Application.UseCases.PlaceOrder;
using MyApp.Domain.Aggregates.Orders;
using MyApp.Domain.Common;

namespace MyApp.Application.Tests;

public sealed class CancelOrderTests : IAsyncDisposable
{
    private readonly TestApp _app = new();

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private async Task<Guid> PlaceAsync() =>
        (await _app.SendAsync(new PlaceOrderCommand("cust-1", [new PlaceOrderLine("SKU-1", 1, 5m)]))).Unwrap();

    [Fact]
    public async Task Cancelling_a_placed_order_stamps_it_with_the_clock_and_saves_once()
    {
        var id = await PlaceAsync();
        Fake.ClearRecordedCalls(_app.UnitOfWork);

        var result = await _app.SendAsync(new CancelOrderCommand(id));

        result.IsSuccess.ShouldBeTrue();
        var order = _app.Saved.ShouldHaveSingleItem();
        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.CancelledAt.ShouldBe(TestApp.Now);
        A.CallTo(() => _app.UnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Cancelling_twice_is_a_conflict_and_saves_nothing_the_second_time()
    {
        var id = await PlaceAsync();
        await _app.SendAsync(new CancelOrderCommand(id));
        Fake.ClearRecordedCalls(_app.UnitOfWork);

        var result = await _app.SendAsync(new CancelOrderCommand(id));

        result.Errors.ShouldHaveSingleItem().ShouldBe(OrderErrors.AlreadyCancelled(id));
        A.CallTo(() => _app.UnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Cancelling_an_unknown_order_is_not_found()
    {
        var result = await _app.SendAsync(new CancelOrderCommand(Guid.NewGuid()));

        result.Errors.ShouldHaveSingleItem().Type.ShouldBe(ErrorType.NotFound);
    }
}

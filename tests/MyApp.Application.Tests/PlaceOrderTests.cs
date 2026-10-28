using FakeItEasy;
using MyApp.Application.UseCases.GetOrder;
using MyApp.Application.UseCases.PlaceOrder;
using MyApp.Domain.Common;
using MyApp.Domain.Aggregates.Orders;

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
        A.CallTo(() => _app.Orders.Add(A<Order>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _app.UnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        var order = _app.Saved.ShouldHaveSingleItem();
        order.Id.ShouldBe(result.Unwrap());
        order.PlacedAt.ShouldBe(TestApp.Now);
        order.Total.ShouldBe(25m);
    }

    [Fact]
    public async Task Invalid_command_is_rejected_by_validation_and_never_reaches_the_handler()
    {
        var result = await _app.SendAsync(new PlaceOrderCommand("", [new PlaceOrderLine("", 1, 1m)]));

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldAllBe(e => e.Type == ErrorType.Validation);
        result.Errors.ShouldContain(e => e.Target == "customerId");
        result.Errors.ShouldContain(e => e.Target == "lines[0].sku");
        A.CallTo(() => _app.Orders.Add(A<Order>._)).MustNotHaveHappened();
        A.CallTo(() => _app.UnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Domain_failure_stages_nothing_and_saves_nothing()
    {
        var tooLong = new string('s', OrderLine.SkuMaxLength + 1);

        var result = await _app.SendAsync(new PlaceOrderCommand("cust-1", [new PlaceOrderLine(tooLong, 1, 1m)]));

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(OrderErrors.SkuTooLong.Code);
        A.CallTo(() => _app.UnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
        _app.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task Domain_failures_are_all_reported_not_just_the_first()
    {
        var result = await _app.SendAsync(new PlaceOrderCommand("cust-1",
            [new PlaceOrderLine("A", 0, 1m), new PlaceOrderLine("B", 1, -1m)]));

        result.Errors.Select(e => e.Target).ShouldBe(["lines[0].quantity", "lines[1].unitPrice"], ignoreOrder: true);
    }

    [Fact]
    public async Task Order_id_is_a_time_ordered_version_7_guid()
    {
        var id = (await _app.SendAsync(ValidCommand())).Unwrap();

        id.ShouldNotBe(Guid.Empty);
        id.Version.ShouldBe(7);
    }

    [Fact]
    public async Task A_failed_save_becomes_the_commands_failed_result()
    {
        var conflict = Error.Conflict("Persistence.DuplicateKey", "duplicate");
        A.CallTo(() => _app.UnitOfWork.SaveChangesAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<Result>(conflict));

        var result = await _app.SendAsync(ValidCommand());

        result.Errors.ShouldHaveSingleItem().ShouldBe(conflict);
    }

    [Fact]
    public async Task Unhandled_exceptions_are_logged_and_rethrown()
    {
        A.CallTo(() => _app.Orders.GetByIdReadOnlyAsync(A<Guid>._, A<CancellationToken>._)).Throws<InvalidOperationException>();

        await Should.ThrowAsync<InvalidOperationException>(() => _app.SendAsync(new GetOrderQuery(Guid.NewGuid())));

        _app.Logs.Messages.ShouldContain("Error: GetOrderQuery threw an unhandled exception");
    }

    [Fact]
    public async Task Queries_never_save()
    {
        await _app.SendAsync(new GetOrderQuery(Guid.NewGuid()));

        A.CallTo(() => _app.UnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Logging_is_outermost_so_it_also_sees_validation_failures()
    {
        await _app.SendAsync(new PlaceOrderCommand("", []));

        _app.Logs.Messages[0].ShouldBe("Information: Handling PlaceOrderCommand");
        _app.Logs.Messages.ShouldContain(m => m.StartsWith("Warning: PlaceOrderCommand failed") && m.Contains("PlaceOrder.CustomerIdRequired"));
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

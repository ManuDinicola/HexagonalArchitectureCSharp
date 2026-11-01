using MyApp.Domain.Aggregates.Orders;
using MyApp.Domain.Common;

namespace MyApp.Domain.Tests;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly Guid Id = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

    private static OrderLineDraft Line(string sku = "SKU-1", int qty = 2, decimal price = 5m) => new(sku, qty, price);

    private static Result<Order> Place(string customer = "cust-1", params OrderLineDraft[] lines) =>
        Order.Place(Id, customer, lines, Now);

    [Fact]
    public void Place_with_valid_data_succeeds_and_computes_total()
    {
        var result = Place("  cust-1 ", Line(qty: 2, price: 5m), Line("SKU-2", 1, 3m));

        result.IsSuccess.ShouldBeTrue();
        result.Unwrap().Id.ShouldBe(Id);
        result.Unwrap().CustomerId.ShouldBe("cust-1");
        result.Unwrap().PlacedAt.ShouldBe(Now);
        result.Unwrap().Total.ShouldBe(13m);
    }

    [Fact]
    public void Place_with_an_empty_id_is_a_bug()
    {
        Should.Throw<ArgumentException>(() => Order.Place(Guid.Empty, "cust-1", [Line()], Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Place_without_customer_fails(string customer)
    {
        var error = Place(customer, Line()).Errors.ShouldHaveSingleItem();

        error.ShouldBe(OrderErrors.CustomerRequired);
        error.Target.ShouldBe("customerId");
    }

    [Fact]
    public void Place_with_too_long_customer_fails()
    {
        var result = Place(new string('c', Order.CustomerIdMaxLength + 1), Line());

        result.Errors.ShouldHaveSingleItem().ShouldBe(OrderErrors.CustomerTooLong);
    }

    [Fact]
    public void Place_without_lines_fails()
    {
        var result = Place("cust-1");

        result.Errors.ShouldHaveSingleItem().ShouldBe(OrderErrors.NoLines);
    }

    [Theory]
    [InlineData("", 1, 1, "OrderLine.SkuRequired", "lines[0].sku")]
    [InlineData("A", 0, 1, "OrderLine.InvalidQuantity", "lines[0].quantity")]
    [InlineData("A", -1, 1, "OrderLine.InvalidQuantity", "lines[0].quantity")]
    [InlineData("A", 1, -0.01, "OrderLine.InvalidUnitPrice", "lines[0].unitPrice")]
    [InlineData("A", 1, 1.005, "OrderLine.UnitPriceTooPrecise", "lines[0].unitPrice")]
    public void A_line_enforces_its_invariants(string sku, int quantity, double price, string expectedCode, string expectedTarget)
    {
        var error = Place("cust-1", Line(sku, quantity, (decimal)price)).Errors.ShouldHaveSingleItem();

        error.Code.ShouldBe(expectedCode);
        error.Target.ShouldBe(expectedTarget);
    }

    [Fact]
    public void A_line_with_too_long_sku_fails()
    {
        var result = Place("cust-1", Line(new string('s', OrderLine.SkuMaxLength + 1)));

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(OrderErrors.SkuTooLong.Code);
    }

    [Fact]
    public void Every_broken_rule_is_reported_at_once_with_the_index_of_the_bad_line()
    {
        var result = Place("", Line(), Line("", 0, 1m), Line("B", 1, -1m));

        result.Errors.Select(e => e.Target).ShouldBe(
            ["customerId", "lines[1].sku", "lines[1].quantity", "lines[2].unitPrice"], ignoreOrder: true);
    }

    [Fact]
    public void A_new_order_is_placed_and_its_total_is_stored_not_recomputed()
    {
        var order = Place("cust-1", Line(qty: 2, price: 5m), Line("SKU-2", 1, 3m)).Unwrap();

        order.Status.ShouldBe(OrderStatus.Placed);
        order.CancelledAt.ShouldBeNull();
        order.Total.ShouldBe(13m);
    }

    [Fact]
    public void Cancel_marks_the_order_cancelled_at_the_given_time_and_keeps_the_total()
    {
        var order = Place("cust-1", Line(qty: 2, price: 5m)).Unwrap();
        var later = Now.AddHours(1);

        var result = order.Cancel(later);

        result.IsSuccess.ShouldBeTrue();
        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.CancelledAt.ShouldBe(later);
        order.Total.ShouldBe(10m);
    }

    [Fact]
    public void Cancel_twice_is_a_conflict_and_keeps_the_first_time()
    {
        var order = Place("cust-1", Line()).Unwrap();
        order.Cancel(Now.AddHours(1));

        var result = order.Cancel(Now.AddHours(2));

        var error = result.Errors.ShouldHaveSingleItem();
        error.ShouldBe(OrderErrors.AlreadyCancelled(Id));
        error.Type.ShouldBe(ErrorType.Conflict);
        order.CancelledAt.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void Placing_an_order_raises_OrderPlaced_with_what_consumers_need()
    {
        var order = Place("cust-1", Line(qty: 2, price: 5m), Line("SKU-2", 1, 3m)).Unwrap();

        var placed = order.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<OrderPlaced>();
        placed.OrderId.ShouldBe(Id);
        placed.CustomerId.ShouldBe("cust-1");
        placed.Total.ShouldBe(13m);
        placed.LineCount.ShouldBe(2);
        placed.OccurredAt.ShouldBe(Now);
        placed.Type.ShouldBe("order.placed.v1");
    }

    [Fact]
    public void Cancelling_raises_OrderCancelled_after_OrderPlaced_and_a_refused_cancel_raises_nothing()
    {
        var order = Place("cust-1", Line()).Unwrap();
        order.Cancel(Now.AddHours(1));
        order.Cancel(Now.AddHours(2)); // refused: already cancelled

        order.DomainEvents.Select(e => e.Type).ShouldBe(["order.placed.v1", "order.cancelled.v1"]);
        order.DomainEvents[1].ShouldBeOfType<OrderCancelled>().OccurredAt.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void Clearing_the_events_empties_the_list()
    {
        var order = Place("cust-1", Line()).Unwrap();

        order.ClearDomainEvents();

        order.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Lines_cannot_be_modified_from_outside()
    {
        var order = Place("cust-1", Line()).Unwrap();

        (order.Lines is ICollection<OrderLine> { IsReadOnly: false }).ShouldBeFalse();
    }
}

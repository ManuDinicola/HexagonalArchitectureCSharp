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
    public void Lines_cannot_be_modified_from_outside()
    {
        var order = Place("cust-1", Line()).Unwrap();

        (order.Lines is ICollection<OrderLine> { IsReadOnly: false }).ShouldBeFalse();
    }
}

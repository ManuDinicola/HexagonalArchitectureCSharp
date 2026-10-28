using MyApp.Domain.Common;
using MyApp.Domain.Orders;

namespace MyApp.Domain.Tests;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private static OrderLine Line(string sku = "SKU-1", int qty = 2, decimal price = 5m) =>
        OrderLine.Create(sku, qty, price).Value;

    [Fact]
    public void Place_with_valid_data_succeeds_and_computes_total()
    {
        var result = Order.Place("  cust-1 ", [Line(qty: 2, price: 5m), Line("SKU-2", 1, 3m)], Now);

        result.IsSuccess.ShouldBeTrue();
        result.Value.CustomerId.ShouldBe("cust-1");
        result.Value.PlacedAt.ShouldBe(Now);
        result.Value.Id.ShouldNotBe(Guid.Empty);
        result.Value.Total.ShouldBe(13m);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Place_without_customer_fails(string customer)
    {
        var result = Order.Place(customer, [Line()], Now);

        result.IsFailure.ShouldBeTrue();
        result.Errors[0].ShouldBe(OrderErrors.CustomerRequired);
    }

    [Fact]
    public void Place_without_lines_fails()
    {
        var result = Order.Place("cust-1", [], Now);

        result.Errors.ShouldHaveSingleItem().ShouldBe(OrderErrors.NoLines);
    }

    [Theory]
    [InlineData("", 1, 1, "OrderLine.SkuRequired")]
    [InlineData("A", 0, 1, "OrderLine.InvalidQuantity")]
    [InlineData("A", -1, 1, "OrderLine.InvalidQuantity")]
    [InlineData("A", 1, -0.01, "OrderLine.InvalidUnitPrice")]
    public void OrderLine_enforces_invariants(string sku, int quantity, double price, string expectedCode)
    {
        var result = OrderLine.Create(sku, quantity, (decimal)price);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(expectedCode);
    }

    [Fact]
    public void Lines_cannot_be_modified_from_outside()
    {
        var order = Order.Place("cust-1", [Line()], Now).Value;

        (order.Lines is ICollection<OrderLine> { IsReadOnly: false }).ShouldBeFalse();
    }
}

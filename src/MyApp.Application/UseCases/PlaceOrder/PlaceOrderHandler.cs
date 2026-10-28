using MyApp.Application.Messaging;
using MyApp.Application.Ports.Out;
using MyApp.Domain.Common;
using MyApp.Domain.Orders;

namespace MyApp.Application.UseCases.PlaceOrder;

internal sealed class PlaceOrderHandler(IOrderRepository orders, IClock clock)
    : IRequestHandler<PlaceOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var lines = new List<OrderLine>();
        foreach (var line in request.Lines)
        {
            var created = OrderLine.Create(line.Sku, line.Quantity, line.UnitPrice);
            if (created.IsFailure) return Result.Failure<Guid>(created.Errors[0]);
            lines.Add(created.Value);
        }

        var order = Order.Place(request.CustomerId, lines, clock.UtcNow);
        if (order.IsFailure) return Result.Failure<Guid>(order.Errors[0]);

        await orders.AddAsync(order.Value, cancellationToken);
        return order.Value.Id;
    }
}

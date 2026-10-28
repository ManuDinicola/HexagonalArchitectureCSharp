using MyApp.Application.Messaging;
using MyApp.Application.Ports.Out;
using MyApp.Domain.Common;
using MyApp.Domain.Orders;

namespace MyApp.Application.UseCases.PlaceOrder;

internal sealed class PlaceOrderHandler(IOrderRepository orders, TimeProvider time)
    : IRequestHandler<PlaceOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var lines = new List<OrderLine>();
        foreach (var line in request.Lines)
        {
            var created = OrderLine.Create(line.Sku, line.Quantity, line.UnitPrice);
            if (created.IsFailure) return Result.Failure<Guid>(created.Errors[0]);
            lines.Add(created.Unwrap());
        }

        var order = Order.Place(request.CustomerId, lines, time.GetUtcNow());
        if (order.IsFailure) return Result.Failure<Guid>(order.Errors[0]);

        await orders.AddAsync(order.Unwrap(), cancellationToken);
        return order.Unwrap().Id;
    }
}

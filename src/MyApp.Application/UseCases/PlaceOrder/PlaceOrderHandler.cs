using MyApp.Application.Messaging;
using MyApp.Application.Ports.Out;
using MyApp.Domain.Aggregates.Orders;
using MyApp.Domain.Common;

namespace MyApp.Application.UseCases.PlaceOrder;

internal sealed class PlaceOrderHandler(IOrderRepository orders, TimeProvider time)
    : IRequestHandler<PlaceOrderCommand, Result<Guid>>
{
    public Task<Result<Guid>> HandleAsync(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var drafts = request.Lines.Select(l => new OrderLineDraft(l.Sku, l.Quantity, l.UnitPrice)).ToList();

        var placed = Order.Place(Guid.CreateVersion7(now), request.CustomerId, drafts, now);
        if (placed.IsFailure)
        {
            return Task.FromResult(Result.Failure<Guid>(placed.Errors));
        }

        var order = placed.Unwrap();
        orders.Add(order);
        return Task.FromResult<Result<Guid>>(order.Id);
    }
}

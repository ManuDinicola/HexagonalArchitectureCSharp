using MyApp.Application.Messaging;
using MyApp.Application.Ports.Out;
using MyApp.Domain.Aggregates.Orders;
using MyApp.Domain.Common;

namespace MyApp.Application.UseCases.CancelOrder;

internal sealed class CancelOrderHandler(IOrderRepository orders, TimeProvider time)
    : IRequestHandler<CancelOrderCommand, Result>
{
    public async Task<Result> HandleAsync(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            return OrderErrors.NotFound(request.OrderId);
        }

        return order.Cancel(time.GetUtcNow());
    }
}

using MyApp.Application.Messaging;
using MyApp.Application.Ports.Out;
using MyApp.Domain.Common;
using MyApp.Domain.Aggregates.Orders;

namespace MyApp.Application.UseCases.GetOrder;

internal sealed class GetOrderHandler(IOrderRepository orders) : IRequestHandler<GetOrderQuery, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> HandleAsync(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdReadOnlyAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            return OrderErrors.NotFound(request.OrderId);
        }

        return new OrderDto(
            order.Id,
            order.CustomerId,
            order.PlacedAt,
            order.Total,
            [.. order.Lines.Select(l => new OrderLineDto(l.Sku, l.Quantity, l.UnitPrice))]);
    }
}

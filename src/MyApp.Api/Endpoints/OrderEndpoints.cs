using MyApp.Api.Contracts;
using MyApp.Application.Messaging;
using MyApp.Application.UseCases.GetOrder;
using MyApp.Application.UseCases.PlaceOrder;

namespace MyApp.Api.Endpoints;

internal static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders");

        group.MapPost("/", async (PlaceOrderRequest body, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var command = new PlaceOrderCommand(
                body.CustomerId,
                [.. body.Lines.Select(l => new PlaceOrderLine(l.Sku, l.Quantity, l.UnitPrice))]);

            var result = await dispatcher.SendAsync(command, ct);

            return result.IsSuccess
                ? Results.Created($"/orders/{result.Unwrap()}", new PlaceOrderResponse(result.Unwrap()))
                : result.ToProblem();
        });

        group.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.SendAsync(new GetOrderQuery(id), ct);

            return result.IsSuccess ? Results.Ok(ToResponse(result.Unwrap())) : result.ToProblem();
        });
    }

    private static OrderResponse ToResponse(OrderDto o) => new(
        o.Id, o.CustomerId, o.PlacedAt, o.Total,
        [.. o.Lines.Select(l => new OrderLineResponse(l.Sku, l.Quantity, l.UnitPrice))]);
}

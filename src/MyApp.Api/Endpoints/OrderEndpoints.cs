using Microsoft.AspNetCore.Mvc;
using MyApp.Api.Contracts;
using MyApp.Api.Hosting;
using MyApp.Application.Idempotency;
using MyApp.Application.Messaging;
using MyApp.Application.ReadModels;
using MyApp.Application.UseCases.CancelOrder;
using MyApp.Application.UseCases.GetOrder;
using MyApp.Application.UseCases.ListOrders;
using MyApp.Application.UseCases.PlaceOrder;
using MyApp.Domain.Common;

namespace MyApp.Api.Endpoints;

internal static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{ApiVersions.V1}/orders").WithTags("Orders");

        MapPlace(group);
        MapList(group);
        MapGet(group);
        MapCancel(group);
    }

    private static void MapPlace(RouteGroupBuilder group)
    {
        group.MapPost("/", async (
            PlaceOrderRequest body,
            [FromHeader(Name = IdempotencyKeys.HeaderName)] string? idempotencyKey,
            IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var command = new PlaceOrderCommand(
                body.CustomerId,
                [.. (body.Lines ?? []).Select(l => new PlaceOrderLine(l.Sku, l.Quantity, l.UnitPrice))],
                idempotencyKey);

            var result = await dispatcher.SendAsync(command, ct);

            return result switch
            {
                Failed f => f.ToProblem(),
                Guid r => Results.Created($"/{ApiVersions.V1}/orders/{r}", new PlaceOrderResponse(r)),
            };
        })
        .WithName("PlaceOrder")
        .WithSummary("Place an order. Send an Idempotency-Key header to make retries safe: the same key and body answers with the first result, the same key with another body is a 409.")
        .Produces<PlaceOrderResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapList(RouteGroupBuilder group)
    {
        group.MapGet("/", async (int? page, int? pageSize, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.SendAsync(
                new ListOrdersQuery(page ?? 1, pageSize ?? ListOrdersQuery.DefaultPageSize), ct);

            return result switch
            {
                Failed f => f.ToProblem(),
                IReadOnlyList<OrderSummary> rows => Results.Ok(rows.Select(o => new OrderSummaryResponse(o.Id, o.CustomerId, o.PlacedAt, o.Total, o.LineCount, o.Status))),
            };
        })
        .WithName("ListOrders")
        .WithSummary("List orders, newest first.")
        .Produces<OrderSummaryResponse[]>()
        .ProducesValidationProblem();
    }

    private static void MapGet(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.SendAsync(new GetOrderQuery(id), ct);

            return result switch
            {
                Failed f => f.ToProblem(),
                OrderDto dto => Results.Ok(ToResponse(dto)),
            };
        })
        .WithName("GetOrder")
        .WithSummary("Get one order.")
        .Produces<OrderResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapCancel(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/cancel", async (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.SendAsync(new CancelOrderCommand(id), ct);

            return result switch
            {
                Failed f => f.ToProblem(),
                Succeeded => Results.NoContent(),
            };
        })
        .WithName("CancelOrder")
        .WithSummary("Cancel a placed order. Cancelling twice is a 409.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static OrderResponse ToResponse(OrderDto o) => new(
        o.Id, o.CustomerId, o.PlacedAt, o.Total, o.Status,
        [.. o.Lines.Select(l => new OrderLineResponse(l.Sku, l.Quantity, l.UnitPrice))]);
}

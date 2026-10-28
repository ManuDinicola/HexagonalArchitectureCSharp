using MyApp.Application.Messaging;
using MyApp.Domain.Common;

namespace MyApp.Application.UseCases.GetOrder;

public sealed record GetOrderQuery(Guid OrderId) : IRequest<Result<OrderDto>>;

/// <summary>What leaves the Application layer. Entities never do.</summary>
public sealed record OrderDto(Guid Id, string CustomerId, DateTimeOffset PlacedAt, decimal Total, IReadOnlyList<OrderLineDto> Lines);

public sealed record OrderLineDto(string Sku, int Quantity, decimal UnitPrice);

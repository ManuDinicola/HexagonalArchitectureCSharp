using MyApp.Application.Messaging;
using MyApp.Domain.Common;

namespace MyApp.Application.UseCases.PlaceOrder;

public sealed record PlaceOrderCommand(string CustomerId, IReadOnlyList<PlaceOrderLine> Lines) : IRequest<Result<Guid>>;

public sealed record PlaceOrderLine(string Sku, int Quantity, decimal UnitPrice);

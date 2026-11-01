using MyApp.Application.Messaging;
using MyApp.Domain.Common;

namespace MyApp.Application.UseCases.PlaceOrder;

public sealed record PlaceOrderCommand(string CustomerId, IReadOnlyList<PlaceOrderLine> Lines, string? IdempotencyKey = null) : IIdempotentCommand<Guid>;

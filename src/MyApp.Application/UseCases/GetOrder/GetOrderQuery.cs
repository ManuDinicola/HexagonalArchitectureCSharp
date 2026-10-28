using MyApp.Application.Messaging;
using MyApp.Domain.Common;

namespace MyApp.Application.UseCases.GetOrder;

public sealed record GetOrderQuery(Guid OrderId) : IRequest<Result<OrderDto>>;

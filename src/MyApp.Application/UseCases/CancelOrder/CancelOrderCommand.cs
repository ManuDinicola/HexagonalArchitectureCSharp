using MyApp.Application.Messaging;
using MyApp.Domain.Common;

namespace MyApp.Application.UseCases.CancelOrder;

public sealed record CancelOrderCommand(Guid OrderId) : ICommand<Result>;

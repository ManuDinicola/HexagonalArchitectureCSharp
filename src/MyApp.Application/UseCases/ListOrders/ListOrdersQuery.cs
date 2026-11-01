using MyApp.Application.Messaging;
using MyApp.Application.ReadModels;
using MyApp.Domain.Common;

namespace MyApp.Application.UseCases.ListOrders;

public sealed record ListOrdersQuery(int Page = 1, int PageSize = ListOrdersQuery.DefaultPageSize) : IRequest<Result<IReadOnlyList<OrderSummary>>>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}

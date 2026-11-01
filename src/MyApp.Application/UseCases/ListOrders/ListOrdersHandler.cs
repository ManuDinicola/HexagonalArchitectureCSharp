using MyApp.Application.Messaging;
using MyApp.Application.Ports.Out;
using MyApp.Application.ReadModels;
using MyApp.Domain.Common;

namespace MyApp.Application.UseCases.ListOrders;

internal sealed class ListOrdersHandler(IOrderReadStore store) : IRequestHandler<ListOrdersQuery, Result<IReadOnlyList<OrderSummary>>>
{
    public async Task<Result<IReadOnlyList<OrderSummary>>> HandleAsync(ListOrdersQuery request, CancellationToken cancellationToken)
    {
        var page = await store.ListNewestFirstAsync((request.Page - 1) * request.PageSize, request.PageSize, cancellationToken);
        return Result.Success(page);
    }
}

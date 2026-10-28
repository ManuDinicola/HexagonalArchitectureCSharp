using MyApp.Application.Messaging;
using MyApp.Domain.Common;

namespace MyApp.Application.UseCases.ListOrders;

/// <summary>Paging bounds protect the service, they are not a business rule, so they are checked here and not in the Domain.</summary>
internal sealed class ListOrdersValidator : IValidator<ListOrdersQuery>
{
    public Task<IReadOnlyList<Error>> ValidateAsync(ListOrdersQuery request, CancellationToken cancellationToken)
    {
        var errors = new List<Error>();

        if (request.Page < 1)
        {
            errors.Add(Error.Validation("ListOrders.PageInvalid", "Page starts at 1.", "page"));
        }

        if (request.PageSize is < 1 or > ListOrdersQuery.MaxPageSize)
        {
            errors.Add(Error.Validation("ListOrders.PageSizeInvalid", $"PageSize must be between 1 and {ListOrdersQuery.MaxPageSize}.", "pageSize"));
        }

        return Task.FromResult<IReadOnlyList<Error>>(errors);
    }
}

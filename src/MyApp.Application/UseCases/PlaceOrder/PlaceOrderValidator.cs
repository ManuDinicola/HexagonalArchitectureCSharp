using MyApp.Application.Messaging;
using MyApp.Domain.Common;

namespace MyApp.Application.UseCases.PlaceOrder;

/// <summary>Shape only: what is missing, reported per field (Target). Ranges and limits are business rules owned by the Domain.</summary>
internal sealed class PlaceOrderValidator : IValidator<PlaceOrderCommand>
{
    public Task<IReadOnlyList<Error>> ValidateAsync(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var errors = new List<Error>();

        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            errors.Add(Error.Validation("PlaceOrder.CustomerIdRequired", "CustomerId is required.", "customerId"));
        }

        if (request.Lines is not { Count: > 0 })
        {
            errors.Add(Error.Validation("PlaceOrder.LinesRequired", "At least one line is required.", "lines"));
        }
        else
        {
            for (var i = 0; i < request.Lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(request.Lines[i].Sku))
                {
                    errors.Add(Error.Validation("PlaceOrder.SkuRequired", "Sku is required.", $"lines[{i}].sku"));
                }
            }
        }

        return Task.FromResult<IReadOnlyList<Error>>(errors);
    }
}

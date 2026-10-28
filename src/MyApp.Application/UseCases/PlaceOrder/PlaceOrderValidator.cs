using MyApp.Application.Messaging;
using MyApp.Domain.Common;

namespace MyApp.Application.UseCases.PlaceOrder;

internal sealed class PlaceOrderValidator : IValidator<PlaceOrderCommand>
{
    public Task<IReadOnlyList<Error>> ValidateAsync(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var errors = new List<Error>();

        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            errors.Add(Error.Validation("CustomerId", "CustomerId is required."));
        }

        if (request.Lines is not { Count: > 0 })
        {
            errors.Add(Error.Validation("Lines", "At least one line is required."));
        }
        else
        {
            for (var i = 0; i < request.Lines.Count; i++)
            {
                var line = request.Lines[i];
                if (string.IsNullOrWhiteSpace(line.Sku))
                {
                    errors.Add(Error.Validation($"Lines[{i}].Sku", "Sku is required."));
                }

                if (line.Quantity <= 0)
                {
                    errors.Add(Error.Validation($"Lines[{i}].Quantity", "Quantity must be greater than zero."));
                }

                if (line.UnitPrice < 0)
                {
                    errors.Add(Error.Validation($"Lines[{i}].UnitPrice", "UnitPrice cannot be negative."));
                }
            }
        }

        return Task.FromResult<IReadOnlyList<Error>>(errors);
    }
}

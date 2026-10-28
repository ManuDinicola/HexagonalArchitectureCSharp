using MyApp.Application.Messaging;
using MyApp.Domain.Common;

namespace MyApp.Application.UseCases.PlaceOrder;

/// <summary>Shape only: what is missing, reported per field. Ranges and limits are business rules owned by the Domain.</summary>
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
            }
        }

        return Task.FromResult<IReadOnlyList<Error>>(errors);
    }
}

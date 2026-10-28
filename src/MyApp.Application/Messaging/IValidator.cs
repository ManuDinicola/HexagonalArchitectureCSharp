using MyApp.Domain.Common;

namespace MyApp.Application.Messaging;

/// <summary>Cheap checks on the shape of a request. Business invariants live in the Domain.</summary>
public interface IValidator<in TRequest>
{
    Task<IReadOnlyList<Error>> ValidateAsync(TRequest request, CancellationToken cancellationToken);
}

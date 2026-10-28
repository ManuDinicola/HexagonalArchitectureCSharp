using MyApp.Application.Messaging;
using MyApp.Domain.Common;

namespace MyApp.Application.UseCases.UseCaseName;

// Shape checks only (required, ranges). Business rules belong in the Domain.
internal sealed class UseCaseNameValidator : IValidator<UseCaseNameCommand>
{
    public Task<IReadOnlyList<Error>> ValidateAsync(UseCaseNameCommand request, CancellationToken cancellationToken)
    {
        var errors = new List<Error>();

        return Task.FromResult<IReadOnlyList<Error>>(errors);
    }
}

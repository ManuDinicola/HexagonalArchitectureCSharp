using MyApp.Application.Messaging;
using MyApp.Domain.Common;

namespace MyApp.Application.UseCases.UseCaseName;

// Orchestrate only: load via ports, call the Domain, save via ports. No business rules here.
internal sealed class UseCaseNameHandler : IRequestHandler<UseCaseNameCommand, Result>
{
    public Task<Result> HandleAsync(UseCaseNameCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.Success());
    }
}

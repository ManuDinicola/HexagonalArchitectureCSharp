using MyApp.Application.Messaging;
using MyApp.Domain.Common;

namespace MyApp.Application.UseCases.UseCaseName;

// Use Result<TValue> instead of Result if the command returns something (e.g. an id).
public sealed record UseCaseNameCommand() : IRequest<Result>;

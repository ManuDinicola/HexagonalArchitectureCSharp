# MyApp.Api

HTTP edge and composition root. References Application, and Infrastructure only through `AddInfrastructure()`.

- Endpoints in `Endpoints/`, request/response records in `Contracts/`. Call `IDispatcher` only: never ports, never entities.
- Map `Result` to HTTP in `Endpoints/ResultExtensions.cs` (first non-validation error sets the status; `Error.Target` is the ValidationProblem key).
- Routes live under `/v1` (`Hosting/ApiVersions.cs`). A breaking change adds `v2`.
- Describe every endpoint: `WithName`, `WithSummary`, `Produces...`.
- Idempotent commands read the `Idempotency-Key` header in the endpoint.
- Tests: `tests/MyApp.Api.Tests` (end-to-end through `ApiFactory`). Full rules: [ARCHITECTURE.md](../../ARCHITECTURE.md).

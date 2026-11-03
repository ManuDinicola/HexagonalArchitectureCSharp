---
name: add-use-case
description: Add a new use case (command or query) to this hexagonal .NET template. Use when asked to add a feature, endpoint, command, query or handler.
---

# Add a use case

Read `ARCHITECTURE.md` ("Where does my code go?" and "Commands, queries and the unit of work") if you have not this session.

1. **Decide command or query.** State change = command (`ICommand<Result<T>>`, or `IIdempotentCommand<T>` when it creates something). Read-only = query (`IRequest<Result<T>>`).
2. **Copy `src/MyApp.Application/UseCases/PlaceOrder`** (command) or `GetOrder` / `ListOrders` (query) to `UseCases/<Name>/` and rename: request, `internal` handler, `internal` validator. Nothing to register: they are found by assembly scanning.
3. **Business rule?** Put it in the Domain aggregate (method returning `Result`, time and ids as parameters), with a Domain test. Validators check shape only.
4. **Outside world?** Add an interface in `Application/Ports/Out` using domain types, implement it `internal` in Infrastructure and register it in `AddInfrastructure()`.
5. **Commands only stage** (`repository.Add`). Never call `SaveChanges` or `IUnitOfWork`; the pipeline saves once on success.
6. **Time and ids:** inject `TimeProvider`; `Guid.CreateVersion7(time.GetUtcNow())`. Never `DateTime.UtcNow` or `Guid.NewGuid()`.
7. **Endpoint:** add it in `Api/Endpoints` with request/response records in `Api/Contracts`, call `IDispatcher`, and describe it with `WithName`, `WithSummary`, `Produces...`. Map any new error status in `ResultExtensions.cs`.
8. **Tests:** Domain test for rules, Application test for the use case (`TestApp`), Api test for HTTP behaviour.
9. **Gate:** `dotnet build` and `dotnet test` must be green. If a guardrail blocks you, the code is in the wrong place: do not weaken it (see `AGENTS.md`).
10. Keep `ARCHITECTURE.md` and `README.md` true if you changed a rule or a convention.

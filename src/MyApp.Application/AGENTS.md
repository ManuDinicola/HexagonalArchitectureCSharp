# MyApp.Application

Use cases and ports. References Domain only.

- A use case is a folder in `UseCases/<Name>/`: command or query, handler, validator (copy `PlaceOrder`).
- Commands implement `ICommand<...>` and only stage changes. Never call `SaveChanges` or `IUnitOfWork`: `UnitOfWorkBehavior` saves once, on success.
- Queries implement `IRequest<Result<T>>` and read untracked (`GetByIdReadOnlyAsync` or a read port returning `ReadModels`).
- Validators check shape only. Ranges and limits belong to the Domain.
- Outside world = interface in `Ports/Out`, domain types only. Time = injected `TimeProvider`; ids = `Guid.CreateVersion7(time.GetUtcNow())`.
- New cross-cutting step = `IPipelineBehavior` in `Behaviors/`, registered in `AddApplication()` (order = execution order, pinned by `PipelineOrderTests`).
- Tests: `tests/MyApp.Application.Tests` (use `TestApp`). Full rules: [ARCHITECTURE.md](../../ARCHITECTURE.md).

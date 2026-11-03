---
name: add-aggregate
description: Add a new aggregate root (entity with its persistence) to this hexagonal .NET template. Use when asked to model a new business entity or concept that is stored.
---

# Add an aggregate

Read `ARCHITECTURE.md` ("Add an aggregate", "Concurrency", "Domain events and the outbox", "Migrations") if you have not this session. Model it on `Domain/Aggregates/Orders`.

1. **Domain** (`src/MyApp.Domain/Aggregates/<Name>/`, one type per file): private setters, static factory returning `Result` that reports every broken rule at once, children created by the root through `internal` factories. Limits are constants. Time and ids are parameters. Errors in `<Name>Errors.cs` with `Error.Target` set. Events are `readonly record struct` implementing `IDomainEvent` with a versioned `Type`, raised in the aggregate and exposed through `IHasDomainEvents`.
2. **Domain tests** in `tests/MyApp.Domain.Tests` for every rule.
3. **Port:** one repository interface per aggregate in `Application/Ports/Out`, domain types only. Add a read port plus a flat record in `ReadModels` if lists are needed.
4. **Infrastructure:** `internal` repository, and an `IEntityTypeConfiguration<T>` in `Persistence/` that calls `builder.UseConcurrencyToken()` and reuses the Domain constants. Register the repository in `AddInfrastructure()`. Timestamps that are filtered or sorted use `UtcTicksConverter`.
5. **Migration:** `dotnet ef migrations add <Name> ...` then the three folding steps in `ARCHITECTURE.md` > Migrations. `MigrationTests` fails if you forget.
6. **Use cases and endpoints:** use the `add-use-case` skill.
7. **Infrastructure tests** for the repository; `ConcurrencyTests.Every_aggregate_root_has_a_concurrency_token` must pass.
8. **Gate:** `dotnet build` and `dotnet test` green. Never weaken a guardrail (see `AGENTS.md`).

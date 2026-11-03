# MyApp.Infrastructure

Adapters for the Application ports (EF Core + SQLite). References Application only.

- Everything is `internal`; the only public member is `AddInfrastructure()`.
- One `IEntityTypeConfiguration<T>` per entity in `Persistence/`. A new aggregate root needs `builder.UseConcurrencyToken()`.
- Schema changes need a migration (`MigrationTests` fails otherwise). The folding steps are in ARCHITECTURE.md > Migrations.
- Timestamps that queries filter or sort on use `UtcTicksConverter` (SQLite cannot compare `DateTimeOffset` text).
- Repositories stage changes; they never save. Expected storage failures become a `Conflict` `Result` in `UnitOfWork`.
- Background work (outbox relay, retention) must hold a lease so several instances are safe.
- Tests: `tests/MyApp.Infrastructure.Tests`. Full rules: [ARCHITECTURE.md](../../ARCHITECTURE.md).

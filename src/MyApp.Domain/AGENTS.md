# MyApp.Domain

Pure business rules. References nothing (no EF, no ASP.NET, no logging).

- Aggregates live in `Aggregates/<Name>/`: private setters, static factories returning `Result`, every broken rule reported at once. The root creates its own children (children have `internal` factories).
- Time and ids are parameters. Never `DateTime.UtcNow`, `Guid.NewGuid()` or `TimeProvider` here.
- Errors go in `<Name>Errors.cs` with `Error.Target` set to the camelCase field path.
- Events: `[StructLayout(LayoutKind.Auto)] readonly record struct` implementing `IDomainEvent`, versioned `Type` (`order.placed.v1`), raised inside the aggregate method.
- Limits and ranges are constants here; Infrastructure reuses them in the EF mapping.
- Every rule gets a test in `tests/MyApp.Domain.Tests`. See `Order` and `OrderTests` as the model.
- Full rules: [ARCHITECTURE.md](../../ARCHITECTURE.md).

# Instructions for AI agents and new contributors

Read [ARCHITECTURE.md](ARCHITECTURE.md) first: it says where every kind of code goes. The README lists what the build enforces.

## Commands

```bash
dotnet build     # warnings and banned APIs are errors
dotnet test      # domain, application, infrastructure, end-to-end API and architecture tests
```

Run both before every commit.

## Rules that cannot be negotiated

- Dependencies point inwards: Domain <- Application <- Infrastructure / Api. Business rules live in the Domain.
- Expected failures are `Result` (with `Error.Target` pointing at the field). Exceptions are for bugs.
- Commands implement `ICommand<...>` and only stage changes; the pipeline saves them. Never call `SaveChanges` or use `IUnitOfWork` in a use case.
- No `DateTime.UtcNow` (inject `TimeProvider`), no `Guid.NewGuid()` (use `Guid.CreateVersion7(time.GetUtcNow())` in Application), no blocking on tasks.
- New aggregate root: add `builder.UseConcurrencyToken()` to its EF configuration.
- One type per file, file named after the type.

## When the build or a test blocks you

The rule is probably right and your code is in the wrong place. Read the message, then ARCHITECTURE.md. Never weaken a guardrail to make a change compile: do not edit `BannedSymbols*.txt`, `tests/MyApp.Architecture.Tests`, `.editorconfig`, `Directory.Build.props` or suppress a warning. If you believe a rule is wrong, stop and ask a human.

## Definition of done

Build and tests are green, a new rule or use case has a test at the right level (Domain test for rules, Application test for use cases, Api test for HTTP behavior), and ARCHITECTURE.md / README.md still tell the truth.

# Hexagonal Architecture template (.NET 11)

A small, opinionated service template for teams with junior developers.
**Rule of the house: anything a junior can get wrong must fail the build, not rely on documentation.**

```
src/    MyApp.Domain  <-  MyApp.Application  <-  MyApp.Infrastructure  <-  MyApp.Api
tests/  Domain.Tests, Application.Tests, Infrastructure.Tests, Api.Tests (end to end), Architecture.Tests
```

## Requirements

.NET 11 SDK (currently release candidate; `global.json` allows prerelease). The code uses C# 15 **union types**
(`LangVersion=preview`) for `Result` / `Result<T>`.

## Quick start

```bash
dotnet build
dotnet test
dotnet run --project src/MyApp.Api      # SQLite file myapp.db is created on first start
```

```bash
curl -X POST localhost:5000/orders -H 'content-type: application/json' \
  -d '{"customerId":"c1","lines":[{"sku":"A","quantity":2,"unitPrice":4.5}]}'
curl localhost:5000/orders/<id>
```

## Add a use case

Copy the `UseCases/PlaceOrder` folder and rename it: a command (`ICommand<...>`, changes state) or a query (`IRequest<...>`), an `internal` handler and an `internal` validator.
Handlers and validators are found by assembly scanning: there is nothing to register.
Commands stage their changes through repositories (`Add`, never `Save`); the pipeline commits them once, in one transaction, and only if the handler succeeded.
The validator only checks the shape of the request (missing fields) and points each error at its field with `Error.Target`. Limits and ranges (max lengths, quantity > 0, price scale) are business rules and live in the Domain, which reports all of them at once.
Then add an endpoint in `MyApp.Api/Endpoints`. Read [ARCHITECTURE.md](ARCHITECTURE.md) for where things go.

## What fails the build

Every rule below is enforced by the compiler, an analyzer or a test: run `dotnet build` and `dotnet test` before every commit.

| Mistake | Caught by |
|---|---|
| `DateTime.Now/UtcNow/Today`, `DateTimeOffset.Now/UtcNow` | `src/BannedSymbols.Common.txt` (RS0030): use an injected `TimeProvider` |
| `Guid.NewGuid()`, `Guid.CreateVersion7()` without a time | RS0030: ids are passed in; Application uses `Guid.CreateVersion7(time.GetUtcNow())` |
| `Task.Result`, any `Task.Wait/WaitAll/WaitAny` overload, `GetAwaiter().GetResult()` (Task and ValueTask), any `Thread.Sleep` overload | RS0030: use `await` / `Task.Delay`. Each overload is listed, because an entry without a parameter list only matches the parameterless one |
| Api calls an outbound port or an entity | RS0030 on usage + architecture tests on any reference |
| Infrastructure calls use cases / mediator / behaviors | RS0030 on usage + architecture tests on any reference |
| Domain/Application referencing EF Core or ASP.NET | architecture tests (types) and `ProjectFileTests` (a package reference alone is enough to fail) |
| Wrong project or package reference (Domain: none; Application: Domain + two abstractions packages; Infrastructure: Application; Api: Application + Infrastructure) | `ProjectFileTests` + assembly-reference architecture tests |
| Api using anything of Infrastructure except `AddInfrastructure()` | architecture tests |
| Public surface growing by accident (Api exports nothing; Infrastructure exports only `AddInfrastructure`; handlers, validators, behaviors and the dispatcher are `internal`) | architecture tests |
| Entity with a public setter, `init` setter or public constructor | architecture tests |
| Aggregate root without an optimistic-concurrency token | `ConcurrencyTests` (call `builder.UseConcurrencyToken()` in its configuration) |
| Handler or other use-case code committing changes itself (`IUnitOfWork`) | architecture tests (the pipeline does it) |
| Request without exactly one handler; a `*Command` that is not an `ICommand`, a `*Query` that is; a request named neither | architecture tests |
| Port that is not an interface, is generic, knows the use cases/EF, or has an async method without a `CancellationToken` | architecture tests |
| Two types in one file, or a file not named after its type | architecture tests (`SourceLayoutTests`) |
| Code smells, style, naming, missing braces, complexity | `AnalysisMode=All` + Roslynator, Meziantou, Sonar analyzers, tuned in `.editorconfig` |
| Warnings (nullable, unused, ...) | `TreatWarningsAsErrors` |
| Silencing a warning in production code (`#pragma warning disable`, `SuppressMessage`, `<NoWarn>`) | architecture tests (`SourceLayoutTests`) |
| The HTTP contract breaking (status codes, ProblemDetails, field keys) | `Api.Tests`: the real app, real pipeline, real SQLite |

Note on the two layers: the Roslyn banned-API analyzer flags *uses* (calls, `new`, `typeof`) with a precise
message while you type. It does not flag bare declarations such as a constructor parameter. The NetArchTest rules cover
that gap. Both work on namespaces, so new ports, entities and use cases are covered automatically.

The guardrails themselves were tested the hard way: each rule above was broken on purpose (a banned call, a forbidden reference, a public setter, a missing token, ...) and the build or the tests had to fail. That exercise found a hole (the blocking-call bans only matched one overload), which is why the list is explicit.

## Design decisions

- **No MediatR / FluentAssertions** (licensing). A ~100-line mediator lives in `Application/Messaging`. Tests use xUnit + Shouldly (assertions) + FakeItEasy (fakes for outbound ports only; the pipeline itself is always real).
- **Result instead of exceptions** for expected failures. `Result` and `Result<T>` are C# 15 unions (`Succeeded | Failed`, `T | Failed`): pattern match them, or check `IsSuccess` and call `Unwrap()` (which throws, because unwrapping a failure is a bug). Exceptions mean bugs and become a 500 ProblemDetails.
- **Optimistic concurrency** lives in Infrastructure, not in the Domain: every aggregate root has a shadow `Version` column (`UseConcurrencyToken()`) that `AppDbContext` stamps on each save, also when only an owned child (an order line) changed. Two writers holding the same version cannot both win; the loser gets a `Conflict` result. A test fails if a new aggregate forgets it.
- **One port per aggregate.** No generic repository. Repositories only stage changes; a separate `IUnitOfWork` port commits them. `UnitOfWorkBehavior` (innermost pipeline step, commands only) calls it once per successful command, so a failed result never persists anything and a command touching several aggregates is atomic. A duplicate key or concurrent update surfaces as a `Conflict` result (HTTP 409), not an exception.
- **Validation split.** The validator reports what is missing, per field (`Error.Target`, which becomes the key in the `ValidationProblem`). The Domain owns every business rule and limit (the same constants feed the EF mapping), so the database never rejects what the Domain accepted.
- **Ids.** The Domain never generates ids or reads the clock; Application creates them with `Guid.CreateVersion7(time.GetUtcNow())`, so they are time-ordered and testable with a fake clock.
- **Time.** `TimeProvider` is registered by `AddApplication()` (`TryAdd`, so a host or a test can register its own first).
- **SQLite + `EnsureCreated`** to keep the template runnable. It does not evolve an existing database (an old `myapp.db` lacks newer columns: delete it). Switch to EF migrations when your model starts to change.

## Renaming `MyApp`

```bash
grep -rlI MyApp . --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj | xargs sed -i 's/MyApp/Acme/g'
find . -depth -name '*MyApp*' -not -path './.git/*' | while read -r p; do mv "$p" "$(dirname "$p")/$(basename "$p" | sed 's/MyApp/Acme/g')"; done
```

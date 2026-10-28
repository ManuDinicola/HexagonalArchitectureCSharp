# Hexagonal Architecture template (.NET 11)

A small, opinionated service template for teams with junior developers.
**Rule of the house: anything a junior can get wrong must fail the build, not rely on documentation.**

```
src/    MyApp.Domain  <-  MyApp.Application  <-  MyApp.Infrastructure  <-  MyApp.Api
tests/  Domain.Tests, Application.Tests, Infrastructure.Tests, Architecture.Tests
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

| Mistake | Caught by |
|---|---|
| `DateTime.Now/UtcNow`, `DateTimeOffset.Now/UtcNow` | `src/BannedSymbols.Common.txt` (RS0030), use an injected `TimeProvider` |
| `Guid.NewGuid()`, `Guid.CreateVersion7()` without a time | `src/BannedSymbols.Common.txt` (RS0030), use `Guid.CreateVersion7(time.GetUtcNow())` in Application and pass the id to the Domain |
| `Task.Result`, `Task.Wait()`, `WaitAll/WaitAny`, `GetAwaiter().GetResult()` (Task and ValueTask), `Thread.Sleep` | `src/BannedSymbols.Common.txt` (RS0030), use `await` / `Task.Delay` |
| Api calls an outbound port or an entity | RS0030 on usage + architecture tests on any reference |
| Infrastructure calls use cases / mediator / behaviors | RS0030 on usage + architecture tests on any reference |
| Domain/Application referencing EF Core or ASP.NET | architecture tests |
| Wrong project reference direction (Infrastructure may reference only Application/Domain; Api only those plus Infrastructure) | architecture tests |
| Api using anything of Infrastructure except `AddInfrastructure()` | architecture tests |
| Public DbContext / configuration / repository | architecture tests |
| Entity with public setter or public constructor | architecture tests |
| Handler or other use-case code committing changes itself (`IUnitOfWork`) | architecture tests (the pipeline does it) |
| Request without exactly one handler | architecture tests |
| Two types in one file | architecture tests (one class/record/interface/enum per file) |
| Code smells, style, naming, missing braces, complexity | `AnalysisMode=All` + Roslynator, Meziantou, Sonar analyzers (same set as the WorkflowEg repo), tuned in `.editorconfig` |
| Warnings (nullable, unused, ...) | `TreatWarningsAsErrors` |

Note on the two layers: the Roslyn banned-API analyzer flags *uses* (calls, `new`, `typeof`) with a precise
message while you type. It does not flag bare declarations such as a constructor parameter. The NetArchTest rules cover
that gap. Both work on namespaces, so new ports, entities and use cases are covered automatically.

## Design decisions

- **No MediatR / FluentAssertions** (licensing). A ~100-line mediator lives in `Application/Messaging`. Tests use xUnit + Shouldly (assertions) + FakeItEasy (fakes for outbound ports only; the pipeline itself is always real).
- **Result instead of exceptions** for expected failures. `Result` and `Result<T>` are C# 15 unions (`Succeeded | Failed`, `T | Failed`): pattern match them, or check `IsSuccess` and call `Unwrap()` (which throws, because unwrapping a failure is a bug). Exceptions mean bugs and become a 500 ProblemDetails.
- **One port per aggregate.** No generic repository. Repositories only stage changes; a separate `IUnitOfWork` port commits them. `UnitOfWorkBehavior` (innermost pipeline step, commands only) calls it once per successful command, so a failed result never persists anything and a command touching several aggregates is atomic. A duplicate key or concurrent update surfaces as a `Conflict` result (HTTP 409), not an exception.
- **Validation split.** The validator reports what is missing, per field (`Error.Target`, which becomes the key in the `ValidationProblem`). The Domain owns every business rule and limit (the same constants feed the EF mapping), so the database never rejects what the Domain accepted.
- **Ids.** The Domain never generates ids or reads the clock; Application creates them with `Guid.CreateVersion7(time.GetUtcNow())`, so they are time-ordered and testable with a fake clock.
- **Time.** `TimeProvider` is registered by `AddApplication()` (`TryAdd`, so a host or a test can register its own first).
- **SQLite + `EnsureCreated`** to keep the template runnable. Switch to EF migrations when your model starts to change.

## Renaming `MyApp`

```bash
grep -rlI MyApp . --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj | xargs sed -i 's/MyApp/Acme/g'
find . -depth -name '*MyApp*' -not -path './.git/*' | while read -r p; do mv "$p" "$(dirname "$p")/$(basename "$p" | sed 's/MyApp/Acme/g')"; done
```

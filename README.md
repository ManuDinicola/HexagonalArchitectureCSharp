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

Copy the `UseCases/PlaceOrder` folder and rename it: a command (or query), an `internal` handler and an `internal` validator.
Handlers and validators are found by assembly scanning: there is nothing to register.
Then add an endpoint in `MyApp.Api/Endpoints`. Read [ARCHITECTURE.md](ARCHITECTURE.md) for where things go.

## What fails the build

| Mistake | Caught by |
|---|---|
| `DateTime.Now/UtcNow`, `DateTimeOffset.Now/UtcNow` | `src/BannedSymbols.Common.txt` (RS0030), use an injected `TimeProvider` |
| `Task.Result`, `Task.Wait()`, `GetAwaiter().GetResult()` | `src/BannedSymbols.Common.txt` (RS0030), use `await` |
| Api calls an outbound port or an entity | RS0030 on usage + architecture tests on any reference |
| Infrastructure calls use cases / mediator / behaviors | RS0030 on usage + architecture tests on any reference |
| Domain/Application referencing EF Core or ASP.NET | architecture tests |
| Wrong project reference direction | architecture tests |
| Public DbContext / configuration / repository | architecture tests |
| Entity with public setter or public constructor | architecture tests |
| Request without exactly one handler | architecture tests |
| Two types in one file | architecture tests (one class/record/interface/enum per file) |
| Warnings (nullable, unused, ...) | `TreatWarningsAsErrors` |

Note on the two layers: the Roslyn banned-API analyzer flags *uses* (calls, `new`, `typeof`) with a precise
message while you type. It does not flag bare declarations such as a constructor parameter, and `BannedSymbols.txt`
lists types one by one. The NetArchTest rules cover those gaps, generically, for every type, including ones added later.

## Design decisions

- **No MediatR / FluentAssertions** (licensing). A ~100-line mediator lives in `Application/Messaging`. Tests use xUnit + Shouldly (assertions) + FakeItEasy (fakes for outbound ports only; the pipeline itself is always real).
- **Result instead of exceptions** for expected failures. `Result` and `Result<T>` are C# 15 unions (`Succeeded | Failed`, `T | Failed`): pattern match them, or check `IsSuccess` and call `Unwrap()` (which throws, because unwrapping a failure is a bug). Exceptions mean bugs and become a 500 ProblemDetails.
- **One port per aggregate.** No generic repository.
- **SQLite + `EnsureCreated`** to keep the template runnable. Switch to EF migrations when your model starts to change.

## Renaming `MyApp`

```bash
grep -rlI MyApp . --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj | xargs sed -i 's/MyApp/Acme/g'
find . -depth -name '*MyApp*' -not -path './.git/*' | while read -r p; do mv "$p" "$(dirname "$p")/$(basename "$p" | sed 's/MyApp/Acme/g')"; done
```

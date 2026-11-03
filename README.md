# Hexagonal Architecture template (.NET 11)

A small, opinionated service template for teams with junior developers.
**Rule of the house: anything a junior can get wrong must fail the build, not rely on documentation.**

```
src/    MyApp.Domain  <-  MyApp.Application  <-  MyApp.Infrastructure  <-  MyApp.Api
tests/  Domain.Tests, Application.Tests, Infrastructure.Tests, Api.Tests (end to end), Architecture.Tests
```

## Requirements

.NET 11 SDK (currently release candidate; `global.json` allows prerelease). EF tooling is a local tool: `dotnet tool restore`. The code uses C# 15 **union types**
(`LangVersion=preview`) for `Result` / `Result<T>`.

> **Preview toolchain.** The template targets a .NET 11 release candidate and C# 15 unions, so tooling (IDE, analyzers) may lag. Expect odd diagnostics around `Result`; the SDK may also change generated code (it once made `Program` public: it is now pinned `internal`). Move to the stable SDK when .NET 11 ships.

## Quick start

```bash
dotnet build
dotnet test
dotnet run --project src/MyApp.Api      # SQLite file myapp.db is created and migrated on first start
```

```bash
curl -X POST localhost:5000/v1/orders -H 'content-type: application/json' -H 'idempotency-key: order-123' \
  -d '{"customerId":"c1","lines":[{"sku":"A","quantity":2,"unitPrice":4.5}]}'
curl localhost:5000/v1/orders/<id>
curl localhost:5000/health/ready
# API reference (Development): http://localhost:5000/scalar/v1   OpenAPI: /openapi/v1.json
```

## Add a use case

Copy the `UseCases/PlaceOrder` folder and rename it: a command (`ICommand<...>`, changes state) or a query (`IRequest<...>`), an `internal` handler and an `internal` validator.
Handlers and validators are found by assembly scanning: there is nothing to register.
Commands stage their changes through repositories (`Add`, never `Save`); the pipeline commits them once, in one transaction, and only if the handler succeeded.
The validator only checks the shape of the request (missing fields) and points each error at its field with `Error.Target`. Limits and ranges (max lengths, quantity > 0, price scale) are business rules and live in the Domain, which reports all of them at once.
Then add an endpoint in `MyApp.Api/Endpoints`. Read [ARCHITECTURE.md](ARCHITECTURE.md) for where things go.

## Local gate (no CI)

```bash
sh scripts/setup.sh   # once per clone: enables the hook (core.hooksPath), checks the SDK, restores tools
```

`.githooks/pre-commit` builds, runs every test, and refuses a commit that stages a guardrail file (`.editorconfig`, `Directory.Build.props`, `BannedSymbols*.txt`, `tests/MyApp.Architecture.Tests`, `AGENTS.md`, ...) unless a human sets `GUARDRAIL_CHANGE=1`. `.claude/settings.json` additionally denies Claude Code edits to those files.

## Coverage and mutation testing

Neither runs in the gate. `sh scripts/coverage.sh` collects coverage (Cobertura XML in `TestResults/`); `sh scripts/mutation.sh` runs Stryker on the Domain with its tests (thresholds in `stryker-config.json`). Run mutation after changing a business rule. Stryker 5.0.0 works on the .NET 11 rc3 SDK (4.8.1 exits silently): last run, 119 mutants created, 87 tested, score 100%, about 9 minutes. It logs "test coverage capture failed" and then tests every mutant against every test, which is why it is slow. `coverage.sh` skips `MyApp.Architecture.Tests` because coverlet's injected tracker type trips the dependency rules.

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
| Two types in one file, or a file not named after its type | architecture tests (`SourceLayoutTests`, on the Roslyn syntax tree; it also fails if a file does not parse, so the rule cannot pass silently) |
| Domain/Application doing I/O (`System.IO`, `System.Net.Http`), `Console`, `Random`, `Environment` | per-project `BannedSymbols.txt` in `MyApp.Domain` and `MyApp.Application` (RS0030): define a port instead |
| Weakening the guardrails (RS0030 no longer an error, warnings no longer errors, a banned list emptied or deleted, a banned namespace that matches nothing) | `GuardrailIntegrityTests` |
| A source file hidden by `.gitignore` (the build passes locally and fails on every clone) | `GuardrailIntegrityTests` (`git check-ignore`) |
| Code smells, style, naming, missing braces, complexity | `AnalysisMode=All` + Roslynator, Meziantou, Sonar analyzers, tuned in `.editorconfig` |
| Warnings (nullable, unused, ...) | `TreatWarningsAsErrors` |
| Silencing a warning in production code (`#pragma warning disable`, `SuppressMessage`, `<NoWarn>`) | architecture tests (`SourceLayoutTests`) |
| The EF model changing without a migration (the schema comes from migrations) | `MigrationTests` (`HasPendingModelChanges`) |
| Two instances publishing the same outbox message or out of order, a lease that two instances hold, retention deleting undelivered messages | `LeaderLeaseTests`, `MultiInstanceTests`, `RetentionPurgerTests` |
| An event saved without its aggregate, or an aggregate saved without its event (outbox atomicity); events delivered out of order or never retried | `OutboxTests`, `OutboxProcessorTests`, `OutboxRelayTests` |
| The pipeline order changing, or an idempotent command losing its idempotency step | `PipelineOrderTests` |
| The HTTP contract breaking (status codes, ProblemDetails, field keys, versioned routes, OpenAPI document, health probes) | `Api.Tests`: the real app, real pipeline, real SQLite |

Note on the two layers: the Roslyn banned-API analyzer flags *uses* (calls, `new`, `typeof`) with a precise
message while you type. It does not flag bare declarations such as a constructor parameter. The ArchUnitNET rules cover
that gap. Both work on namespaces, so new ports, entities and use cases are covered automatically.

The guardrails themselves were tested the hard way: each rule above was broken on purpose (a banned call, a forbidden reference, a public setter, a missing token, ...) and the build or the tests had to fail. That exercise found a hole (the blocking-call bans only matched one overload), which is why the list is explicit.

## Design decisions

- **No MediatR / FluentAssertions** (licensing). A ~100-line mediator lives in `Application/Messaging`. Tests use xUnit v3 + Shouldly (assertions) + FakeItEasy (fakes for outbound ports only; the pipeline itself is always real).
- **Result instead of exceptions** for expected failures. `Result` and `Result<T>` are C# 15 unions (`Succeeded | Failed`, `T | Failed`): pattern match them (`result switch { Failed f => ..., T value => ... }`); `Unwrap()` throws on failure, so it is banned in `src` and left for tests. Do not use property patterns (`{ IsSuccess: true }`) on a result: on a union they apply to `Value`. Performance is measured, not assumed: see the next section. Exceptions mean bugs and become a 500 ProblemDetails.
- **Optimistic concurrency** lives in Infrastructure, not in the Domain: every aggregate root has a shadow `Version` column (`UseConcurrencyToken()`) that `AppDbContext` stamps on each save, also when only an owned child (an order line) changed. Two writers holding the same version cannot both win; the loser gets a `Conflict` result. A test fails if a new aggregate forgets it.
- **One port per aggregate.** No generic repository. Repositories only stage changes; a separate `IUnitOfWork` port commits them. `UnitOfWorkBehavior` (innermost pipeline step, commands only) calls it once per successful command, so a failed result never persists anything and a command touching several aggregates is atomic. A duplicate key or concurrent update surfaces as a `Conflict` result (HTTP 409), not an exception.
- **Validation split.** The validator reports what is missing, per field (`Error.Target`, which becomes the key in the `ValidationProblem`). The Domain owns every business rule and limit (the same constants feed the EF mapping), so the database never rejects what the Domain accepted.
- **Ids.** The Domain never generates ids or reads the clock; Application creates them with `Guid.CreateVersion7(time.GetUtcNow())`, so they are time-ordered and testable with a fake clock.
- **Time.** `TimeProvider` is registered by `AddApplication()` (`TryAdd`, so a host or a test can register its own first).
- **SQLite + EF migrations.** The schema comes from migrations (`Infrastructure/Persistence/Migrations`), applied at startup by default (`Database:MigrateOnStartup`; set it to `false` and run the migrations as a deploy step when you have several instances). A database created by an older `EnsureCreated` version has no migration history: delete `myapp.db`. A test fails if the model changes without a migration. How to add one: [ARCHITECTURE.md](ARCHITECTURE.md#migrations).
- **URL versioning.** Everything is under `/v1`. A breaking change adds `/v2` next to it; additive changes stay in v1. The OpenAPI document is per version (`/openapi/v1.json`), with a Scalar reference at `/scalar/v1`. Both are served in Development only.
- **Idempotency.** `POST /v1/orders` accepts an `Idempotency-Key` header. The first success is recorded in the same transaction as the order; a retry with the same key and body gets the same answer and creates nothing; the same key with another body is a 409. A command opts in by implementing `IIdempotentCommand<T>`.
- **Outbox.** Aggregates raise domain events (`OrderPlaced`, `OrderCancelled`). The unit of work saves them to an `OutboxMessages` table in the same transaction as the change, and a background relay publishes them in order through the `IMessagePublisher` port: at least once, with retries and dead-lettering. The default adapter only logs; plug your broker in there. Details and the consumer's duty to ignore duplicates (`MessageId`): [ARCHITECTURE.md](ARCHITECTURE.md#domain-events-and-the-outbox).
- **Retention.** Idempotency records and delivered outbox messages are purged after `Retention:Days` (default 7; keep it longer than your clients' retry window). Undelivered and dead-lettered messages are never purged. [ARCHITECTURE.md](ARCHITECTURE.md#retention).
- **Several instances.** The relay and the purge are safe on every instance: they take turns through a lease row in the database, so one publisher keeps the global message order and a dead one is replaced within about 30 seconds. Set `Database:MigrateOnStartup=false` and migrate as a deploy step. [ARCHITECTURE.md](ARCHITECTURE.md#running-several-instances).
- **Health.** `/health/live` has no checks (the process answers); `/health/ready` runs the checks tagged `ready` (the database). Use live for restarts and ready for load balancing.
- **Observability.** OpenTelemetry: ASP.NET Core requests, one span per use case (`ActivitySource` `MyApp.Application`), the counter `myapp.requests` and the histogram `myapp.request.duration` (tagged by request and outcome), and logs with the trace id. Nothing is exported until you set `OTEL_EXPORTER_OTLP_ENDPOINT`. Error responses carry `traceId`.

## Renaming `MyApp`

On Windows without bash, in PowerShell:

```powershell
$skip = '[\\/](\.git|bin|obj)[\\/]'
Get-ChildItem -Recurse -File -Exclude *.dll,*.pdb | Where-Object { $_.FullName -notmatch $skip } |
  ForEach-Object { (Get-Content $_ -Raw) -replace 'MyApp','Acme' | Set-Content $_ -NoNewline }
Get-ChildItem -Recurse -Filter '*MyApp*' | Where-Object { $_.FullName -notmatch $skip } |
  Sort-Object { $_.FullName.Length } -Descending | ForEach-Object { Rename-Item $_.FullName ($_.Name -replace 'MyApp','Acme') }
```

Or with bash/GNU sed:

```bash
grep -rlI MyApp . --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj | xargs sed -i 's/MyApp/Acme/g'
find . -depth -name '*MyApp*' -not -path './.git/*' | while read -r p; do mv "$p" "$(dirname "$p")/$(basename "$p" | sed 's/MyApp/Acme/g')"; done
```

## Result performance

`Result` / `Result<T>` use the C# 15 `union` keyword. We benchmarked it (BenchmarkDotNet, Release, .NET 11 RC) against a hand-written `[Union]` struct with one typed field per case, which avoids boxing value-type successes. Only these two implementations were compared; third-party result libraries were not measured.

| What the endpoint does | `union` keyword (used) | Hand-written `[Union]` |
|---|---|---|
| Switch on a success or failure (`result switch { ... }`) | **1.1 - 2.7 ns** | 2.4 - 11.5 ns (2-10x slower) |
| Create a success with a reference payload (DTO, string) | **0 B, ~0.2 ns** | 0 B, ~2.2 ns |
| Create a success with a small value payload (`Guid`, `int`) | 24-32 B, ~3.8 ns | **0 B, ~0.2 - 0.7 ns** |
| Create a success with a 128-byte struct | 144 B, 8.9 ns | **0 B, 7.1 ns** |
| Create a failure | **~5.5 ns** | ~7.3 ns |

The keyword form is faster on the path every request takes (consuming the result) and for the usual reference-type payloads. The hand-written form only wins on allocations for value-type payloads, a few dozen bytes of short-lived garbage per request, which costs more in code to maintain than it saves. Method, machine and caveats (the runs are in-process): [docs/benchmarks.md](docs/benchmarks.md).

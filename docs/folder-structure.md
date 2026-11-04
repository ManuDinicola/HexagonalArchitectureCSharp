# Folder structure

Where every kind of file lives, and why. For the rules behind it read [ARCHITECTURE.md](../ARCHITECTURE.md); for what the build enforces read the [README](../README.md).

## The hexagon in one picture

| Textbook layer | Folder | Role |
|---|---|---|
| Domain | `src/MyApp.Domain` | Pure business rules and model. References nothing. |
| Application | `src/MyApp.Application` | Use cases, the ports they need, orchestration of the domain. References Domain only. |
| Adapters in (driving) | `src/MyApp.Api` | HTTP endpoints that turn requests into use case calls. |
| Adapters out (driven) | `src/MyApp.Infrastructure` | EF Core / SQLite persistence, outbox, message publisher. Implements the ports. |
| Composition root | `src/MyApp.Api` (`Program.cs`) | Wires everything together at startup. |

Dependencies point inwards: **Domain <- Application <- Infrastructure / Api**. The Api reaches Infrastructure only through `AddInfrastructure()`.

## Repository root

```text
.
├── src/                  production code (the four projects above)
├── tests/                one test project per layer, plus architecture tests
├── benchmarks/           BenchmarkDotNet comparison of Result implementations (not part of the app)
├── docs/                 documentation (this file, benchmark results)
├── scripts/              setup.sh (once per clone), coverage.sh, mutation.sh
├── .claude/              Claude Code settings (deny rules) and skills (add-use-case, add-aggregate)
├── .githooks/            pre-commit: build + tests, refuses staged guardrail changes
├── .github/              CODEOWNERS, dependabot.yml, pull request template
├── ARCHITECTURE.md       the rules and "where does my code go?"
├── AGENTS.md / CLAUDE.md instructions for AI agents and new contributors
├── Directory.Build.props shared build settings and analyzers (a guardrail)
├── Directory.Packages.props  central package versions
├── global.json           pinned .NET SDK
├── dotnet-tools.json     local tools (dotnet-ef, dotnet-stryker)
├── stryker-config.json   mutation testing of the Domain
└── MyApp.slnx            solution
```

## `src/MyApp.Domain`: business rules

```text
Aggregates/Orders/    one folder per aggregate
    Order.cs              the aggregate root: factories and state changes that return Result
    OrderLine.cs          a child, created only by the root (internal factory)
    OrderLineDraft.cs     the input the root accepts for a line
    OrderStatus.cs        enum
    OrderErrors.cs        every error this aggregate can return
    OrderPlaced.cs, OrderCancelled.cs   domain events (versioned Type, e.g. order.placed.v1)
Common/               building blocks shared by all aggregates
    Result.cs, ResultOfT.cs, Succeeded.cs, Failed.cs, IResult.cs   the result union
    Error.cs, ErrorType.cs                                          error with Target (field)
    IDomainEvent.cs, IHasDomainEvents.cs                            events
BannedSymbols.txt     APIs this project may not use
```

Put here: invariants, limits (as constants), state transitions, events. Time and ids arrive as parameters.

## `src/MyApp.Application`: use cases and ports

```text
UseCases/<Name>/      one folder per use case: PlaceOrder, CancelOrder, GetOrder, ListOrders
    <Name>Command.cs / <Name>Query.cs   the request (ICommand<...> changes state, IRequest<...> reads)
    <Name>Handler.cs                    orchestrates: loads, calls the Domain, stages changes
    <Name>Validator.cs                  shape of the request only (limits belong to the Domain)
    *Dto.cs                             what a query returns
Ports/Out/            interfaces for everything outside: repositories, unit of work, publisher, stores
ReadModels/           flat records for list and projection queries
Messaging/            the mediator: IDispatcher, IRequestHandler, ICommand, IPipelineBehavior, ...
Behaviors/            cross-cutting pipeline steps: telemetry, logging, validation, unit of work, idempotency
Idempotency/          idempotency key rules and the stored record
Outbox/               the message shape handed to the publisher
DependencyInjection.cs    AddApplication(): scans handlers and validators, registers the pipeline
```

Request flow: `Endpoint -> IDispatcher -> Telemetry -> Logging -> Validation -> UnitOfWork (commands) -> Idempotency (idempotent commands) -> Handler -> Domain + Ports`.

There is no `Ports/In`: the inbound port is `IDispatcher` plus the command and query types.

## `src/MyApp.Infrastructure`: driven adapters

```text
Persistence/          EF Core + SQLite
    AppDbContext.cs, AppDbContextFactory.cs
    OrderConfiguration.cs, ...Configuration.cs   one IEntityTypeConfiguration per entity
    OrderRepository.cs, OrderReadStore.cs         implement the ports
    UnitOfWork.cs, IdempotencyStore.cs, DatabaseHealthCheck.cs, DatabaseInitializer.cs
    Migrations/                                   EF migrations (see ARCHITECTURE.md > Migrations)
Outbox/               background work
    OutboxRelay.cs, OutboxProcessor.cs            publish pending messages in order
    LoggingMessagePublisher.cs                    default publisher: logs only, replace with your broker
    RetentionWorker.cs, RetentionPurger.cs        purge old records
    LeaderLease.cs                                lets several instances share the work safely
DependencyInjection.cs    AddInfrastructure(): the only public member of this project
```

Everything else is `internal`.

## `src/MyApp.Api`: driving adapter and composition root

```text
Endpoints/            OrderEndpoints, HealthEndpoints, ResultExtensions (Result -> HTTP status)
Contracts/            request and response records: the public HTTP shape (never domain entities)
Hosting/              ApiVersions, OpenAPI/Scalar setup, observability (OpenTelemetry) setup
Program.cs            composition root
appsettings.json      configuration
```

## `tests/`

| Project | Tests | Example |
|---|---|---|
| `MyApp.Domain.Tests` | Business rules, `Result` | `OrderTests` |
| `MyApp.Application.Tests` | Use cases and the pipeline, with fakes (`Fakes/`) and `TestApp` | `PlaceOrderTests`, `PipelineOrderTests` |
| `MyApp.Infrastructure.Tests` | Repositories, outbox, leases, migrations, against SQLite | `OutboxProcessorTests`, `MigrationTests` |
| `MyApp.Api.Tests` | End-to-end HTTP through `ApiFactory` | `OrderEndpointsTests` |
| `MyApp.Architecture.Tests` | Dependency rules, one type per file, guardrail integrity (a guardrail: do not edit) | `DependencyRulesTests` |

## Where does my new file go?

| I am adding... | It goes in |
|---|---|
| A business rule | `Domain/Aggregates/<Name>/` |
| A use case | `Application/UseCases/<Name>/` (copy `PlaceOrder`) |
| A dependency on the outside world | an interface in `Application/Ports/Out`, its implementation in `Infrastructure/...` |
| An HTTP route | `Api/Endpoints` and `Api/Contracts` |
| A database table | `Infrastructure/Persistence` (configuration + migration) |
| A cross-cutting step around every use case | `Application/Behaviors` |

The full table, with the edge cases, is in [ARCHITECTURE.md](../ARCHITECTURE.md#where-does-my-code-go).

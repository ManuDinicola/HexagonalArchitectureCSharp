# Architecture

Hexagonal (ports and adapters): the business core knows nothing about databases, HTTP or frameworks.
Dependencies point inwards only.

```mermaid
flowchart LR
    Client([HTTP client]) --> Api

    subgraph Hexagon
        direction TB
        Application["Application<br/>use cases, ports (Ports/Out), mediator"]
        Domain["Domain<br/>entities, Result, Error"]
        Application --> Domain
    end

    Api["Api<br/>minimal API, contracts, composition root"] -->|IDispatcher + commands/queries| Application
    Infrastructure["Infrastructure<br/>EF Core + SQLite, internal adapters"] -->|implements Ports/Out| Application
    Api -. "AddInfrastructure() only" .-> Infrastructure
    Infrastructure --> DB[(SQLite)]
```

| Project | References | Contains |
|---|---|---|
| `MyApp.Domain` | nothing | Entities, `Result` / `Result<T>` (C# 15 unions), `Error`, domain errors |
| `MyApp.Application` | Domain | Use cases (command/query + handler + validator), `Ports/Out`, mediator (`Messaging`, `Behaviors`) |
| `MyApp.Infrastructure` | Application | Adapters: EF Core, clock. All `internal`; only `AddInfrastructure()` is public |
| `MyApp.Api` | Application, Infrastructure | Endpoints, contracts, ProblemDetails mapping, `Program.cs` wiring |

Request flow: `Endpoint -> IDispatcher -> LoggingBehavior -> ValidationBehavior -> Handler -> Domain + Ports`.

## The 5 golden rules

1. **Dependencies point inwards.** Domain knows nothing; Application knows only Domain; adapters know Application.
2. **Business rules live in the Domain.** Entities have private setters and static factory methods that enforce invariants. Handlers orchestrate, they don't decide.
3. **Expected failures are `Result`, bugs are exceptions.** Never throw for "not found" or "invalid input".
4. **Talk to the outside world only through ports.** Storage is `IOrderRepository`; time is the built-in `TimeProvider`. One specific port per aggregate, using only domain types.
5. **Never leak internals.** The Api never returns entities (map to `Api.Contracts`) and never touches ports; adapters are `internal`.

All five are enforced by the build (see the table in the README). If a rule blocks you, talk to the team. Do not edit the guardrail.

## Where does my code go?

| I want to... | Put it in |
|---|---|
| Add a business rule or invariant | `Domain/<Aggregate>`: entity factory or method, plus a Domain test |
| Add an error returned to callers | `Domain/<Aggregate>/<Aggregate>Errors.cs` |
| Add a use case | copy `Application/UseCases/PlaceOrder` (command + handler + validator) |
| Check request shape (required, ranges) | the use case's `Validator` |
| Need something from the outside (DB, clock, HTTP service) | an interface in `Application/Ports/Out`, domain types only |
| Implement that interface | `Infrastructure/...` as an `internal` class, registered in `AddInfrastructure()` |
| Add an EF mapping | an `IEntityTypeConfiguration<T>` in `Infrastructure/Persistence` (picked up automatically) |
| Add an HTTP endpoint | `Api/Endpoints`; request/response records in `Api/Contracts`; call `IDispatcher` |
| Map an error to an HTTP status | `Api/Endpoints/ResultExtensions.cs` |
| Cross-cutting concern around every use case | an `IPipelineBehavior` in `Application/Behaviors`, registered in `AddApplication()` (order = execution order) |
| Read the current time | inject `TimeProvider` and call `GetUtcNow()` (never `DateTime.UtcNow`) |

## When you add a new port, entity or use case

The architecture tests cover new types automatically. The Roslyn `BannedSymbols.txt` lists types one by one, so add a line:

- new port in `Ports/Out` -> `src/MyApp.Api/BannedSymbols.txt`
- new entity -> `src/MyApp.Api/BannedSymbols.txt`
- new command/query -> `src/MyApp.Infrastructure/BannedSymbols.txt`

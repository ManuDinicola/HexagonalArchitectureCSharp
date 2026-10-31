# Result benchmarks: `union` keyword vs hand-written `[Union]` structs

Question: is it worth replacing the C# 15 `union` keyword (one `object? Value` field, so value-type cases are boxed) with hand-written `[Union]` structs (one typed field per case plus a state byte)?

- **Baseline** (`Keyword_*`): the keyword implementation at commit `c6994c6`, copied unchanged into `benchmarks/MyApp.Benchmarks/KeywordUnion` (namespace `MyApp.Benchmarks.KeywordUnion`).
- **Candidate** (`Handwritten_*`): the hand-written `[Union]` structs from commit `a5a8e3d`, kept in `benchmarks/MyApp.Benchmarks/HandwrittenUnion` (namespace `MyApp.Benchmarks.HandwrittenUnion`). They are not used by `src`.

## Machine

| | |
|---|---|
| CPU | AMD Ryzen 7 5700X 3.40GHz, 8 cores / 16 threads |
| OS | Windows 11 (10.0.26300.9711) |
| SDK / runtime | .NET SDK 11.0.100-rc.2.26504.105, runtime 11.0.0-rc.2 (RyuJIT x86-64-v3) |
| BenchmarkDotNet | 0.15.8, `Job.Default`, `MemoryDiagnoser`, Release |

**In-process caveat.** BenchmarkDotNet 0.15.8 does not recognise the net11.0 preview moniker, so out-of-process runs fail validation. Everything runs with `InProcessEmitToolchain`. Results are less isolated than the default out-of-process toolchain (the harness shares the process, JIT state and GC with the benchmarks). Treat differences of a few tenths of a nanosecond, and the sub-nanosecond "0.2 ns" rows (which the JIT can inline to almost nothing), with caution.

Reproduce: `dotnet run -c Release --project benchmarks/MyApp.Benchmarks -- --filter '*' --exporters markdown` (add `--smoke` for a short job). Output goes to `BenchmarkDotNet.Artifacts/` (git-ignored).

## Results

Ratio is candidate / baseline within a group (lower is better). Allocated is per operation.

### Creating a success `Result<T>`

| T | Keyword mean | Hand-written mean | Ratio | Keyword alloc | Hand-written alloc |
|---|---:|---:|---:|---:|---:|
| `Guid` | 3.85 ns | 0.70 ns | 0.18 | 32 B | 0 |
| `int` | 3.87 ns | 0.24 ns | 0.06 | 24 B | 0 |
| small readonly struct | 3.82 ns | 0.69 ns | 0.18 | 24 B | 0 |
| `string` | 0.22 ns | 2.29 ns | 10.28 | 0 | 0 |
| record class | 0.22 ns | 2.30 ns | 10.60 | 0 | 0 |

### Everything else

| Scenario | Keyword mean | Hand-written mean | Ratio | Keyword alloc | Hand-written alloc |
|---|---:|---:|---:|---:|---:|
| Create failure `Result<Guid>` | 5.54 ns | 7.30 ns | 1.32 | 24 B | 24 B |
| Void `Result` create success | 3.46 ns | 0.23 ns | 0.07 | 24 B | 0 |
| Void `Result` create failure | 5.54 ns | 7.29 ns | 1.31 | 24 B | 24 B |
| Void `Result` switch (ok + failed) | 4.33 ns | 4.84 ns | 1.12 | 0 | 0 |
| Switch success, `Result<Guid>` | 2.71 ns | 7.25 ns | 2.68 | 0 | 0 |
| Switch failure, `Result<Guid>` | 1.15 ns | 2.74 ns | 2.39 | 0 | 0 |
| Switch success, `Result<string>` | 1.36 ns | 5.53 ns | 4.07 | 0 | 0 |
| Switch failure, `Result<string>` | 1.14 ns | 2.49 ns | 2.20 | 0 | 0 |
| `IsSuccess` + `Errors`, `Result<Guid>` | 1.16 ns | 0.71 ns | 0.61 | 0 | 0 |
| `IsSuccess` + `Errors`, `Result<string>` | 1.21 ns | 0.72 ns | 0.60 | 0 | 0 |
| Pipeline behavior, invalid (`TResponse.FromErrors`) | 6.07 ns | 7.62 ns | 1.26 | 24 B | 24 B |
| Pipeline behavior, valid (`Result<Guid>`) | 6.04 ns | 3.04 ns | 0.50 | 32 B | 0 |

Notes:
- The "switch" rows are the handler -> behavior -> endpoint consumption path (`result switch { Failed f => ..., T value => ... }`), through a `NoInlining` helper.
- The reported CS8780 for pattern matching on an open generic `T` did not reproduce here. What the compiler does report is CS8655: with an unconstrained `T` the switch is not exhaustive because `T` may be null. So the generic `CreateSuccessBenchmarks<T>` only creates results, and the read benchmarks use closed types (`Guid`, `string`).
- The benchmark sources use the same consuming code on both sides, apart from the type names.

## Verdict

**Not clearly better. It is a trade, and the numbers favour it only for value-type payloads that are created often.**

What the hand-written version wins:
- It removes the allocation on the success path for value types: `Guid` 32 B -> 0, `int`/struct 24 B -> 0, void `Result.Success()` 24 B -> 0 (a shared `Succeeded.Instance`). A success that goes through the mini pipeline is half the time (3.0 vs 6.0 ns) and allocation-free.
- `IsSuccess` / `Errors` are about 40% faster (state byte instead of a type test on `object`).

What it loses:
- Reference-type payloads (`string`, record class) were already allocation-free in the keyword form, and creating them is about 2 ns slower (0.2 -> 2.3 ns), because the struct is larger (two references plus the state, instead of one reference) and the baseline inlines to almost nothing.
- Failures are about 30% slower to create (7.3 vs 5.5 ns), with the same 24 B.
- **Pattern-matching consumption is 2.2x to 4x slower** (for example 7.3 vs 2.7 ns for a `Guid` success, 5.5 vs 1.4 ns for a `string` success), and that is the path every endpoint takes. The compiler-generated switch over a user-written union goes through `HasValue` / `TryGetValue` checks, which this measurement shows to be more expensive than the keyword form's single type test on `Value`. I did not inspect the generated IL, so that explanation is a hypothesis.
- About 100 more lines of hand-written code to maintain, plus a contract (Value / HasValue / TryGetValue must agree) that only tests, not the compiler, enforce.

Net effect in this template: the absolute differences are a few nanoseconds per request (a few bytes of garbage versus a few ns of extra matching), against an HTTP request, validation, and a SQLite round trip that cost microseconds to milliseconds. Neither version will show up in a real profile of this application.

**Decision: `src` keeps the `union` keyword.** The hand-written version was reverted out of `MyApp.Domain` (it lives on in `HandwrittenUnion/` for reproducibility). It is the fastest to consume, equal or faster for failures and reference payloads, needs no hand-written code or contract tests, and the compiler owns it. The only cost is one 24-32 B allocation per successful value-type result (in this template, `Result<Guid>` from `PlaceOrder`), which is noise next to the database round trip.

A leaner hand-written layout (one `object?` state plus the `T` field, no state byte) was also tried and was not better: its success switch measured about 2x slower than the layout above (7.7 vs 3.6 ns). The cost comes from copying a 24-32 byte struct, against the keyword form's single 8-byte reference.

If a profile ever shows the allocation matters, return a reference payload on that one hot path before reintroducing custom union code. Revisit when the final .NET 11 / C# 15 compiler ships.

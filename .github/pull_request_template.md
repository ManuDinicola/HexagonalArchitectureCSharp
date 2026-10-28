## What and why

<!-- One or two sentences. Link the issue. -->

## Checklist

- [ ] `dotnet build` and `dotnet test` pass locally
- [ ] New business rule? It lives in the Domain (entity factory / method), with a unit test
- [ ] New use case? With an Application test
- [ ] New port, entity or use case? It is in the right folder (see ARCHITECTURE.md), so the guardrails apply automatically
- [ ] I did NOT edit any `BannedSymbols*.txt` or the architecture tests to make my change compile

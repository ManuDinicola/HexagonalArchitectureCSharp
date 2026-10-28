## What and why

<!-- One or two sentences. Link the issue. -->

## Checklist

- [ ] `dotnet build` and `dotnet test` pass locally
- [ ] New business rule? It lives in the Domain (entity factory / method), with a unit test
- [ ] New use case? With an Application test
- [ ] New port or entity? Added a line to the `BannedSymbols.txt` of Api / Infrastructure (see ARCHITECTURE.md)
- [ ] I did NOT edit any `BannedSymbols*.txt` or the architecture tests to make my change compile

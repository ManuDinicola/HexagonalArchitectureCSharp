#!/bin/sh
# Line/branch coverage for the behaviour tests (Cobertura XML under TestResults/).
# MyApp.Architecture.Tests is skipped on purpose: coverlet injects a tracker type into the
# instrumented assemblies, which the dependency rules would report as a violation. Those tests
# check structure, not behaviour, so they add nothing to coverage.
# Usage: sh scripts/coverage.sh
set -e
cd "$(dirname "$0")/.."
rm -rf TestResults
for project in tests/*/*.csproj; do
  case "$project" in
    *Architecture*) continue ;;
  esac
  dotnet test "$project" --collect:"XPlat Code Coverage" --results-directory TestResults -nologo
done
echo "Cobertura files:"
find TestResults -name 'coverage.cobertura.xml'

#!/bin/sh
# Line/branch coverage for every test project (Cobertura XML under TestResults/).
# Usage: sh scripts/coverage.sh
set -e
cd "$(dirname "$0")/.."
rm -rf TestResults
dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults -nologo
echo "Cobertura files:"
find TestResults -name 'coverage.cobertura.xml'

#!/bin/sh
# One-time setup for a fresh clone: enables the local gate and checks the toolchain.
# Usage: sh scripts/setup.sh
set -e
cd "$(dirname "$0")/.."

git config core.hooksPath .githooks
echo "ok  git hooks enabled (.githooks/pre-commit runs build + tests)"

wanted=$(sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' global.json)
if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERR dotnet not found. Install the .NET 11 SDK (global.json asks for $wanted or a later 11.0.1xx prerelease)." >&2
  exit 1
fi
if ! dotnet --version >/dev/null 2>&1; then
  echo "ERR no installed SDK satisfies global.json ($wanted, roll forward to latest feature, prerelease allowed)." >&2
  echo "    Installed: $(dotnet --list-sdks | tr '\n' ' ')" >&2
  echo "    Install the .NET 11 release-candidate SDK from https://dotnet.microsoft.com/download/dotnet/11.0" >&2
  exit 1
fi
echo "ok  SDK $(dotnet --version)"

dotnet tool restore >/dev/null
echo "ok  local tools restored (dotnet-ef, dotnet-stryker)"

echo "Next: dotnet build && dotnet test"

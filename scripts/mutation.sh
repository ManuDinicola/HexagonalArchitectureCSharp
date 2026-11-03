#!/bin/sh
# Mutation testing of the Domain (business rules) with its own tests.
# Slow: run it after a rule change or before a release, not on every commit.
# Usage: sh scripts/mutation.sh
set -e
cd "$(dirname "$0")/.."
dotnet tool restore
dotnet stryker --config-file stryker-config.json

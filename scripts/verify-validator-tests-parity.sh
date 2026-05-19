#!/usr/bin/env bash
# Fail when any FluentValidation *Validator.cs lacks matching *ValidatorTests.cs (endpoint-schema-validation §16.2).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
API="$ROOT/BeDemo.Api/Validation"
TESTS="$ROOT/BeDemo.Api.Tests/Validation"
missing=0

while IFS= read -r -d '' v; do
  base="$(basename "$v" .cs)"
  # Skip interface stubs named like validators (e.g. IFileValidator).
  if [[ "$base" =~ ^I[A-Z] ]]; then
    continue
  fi
  if [[ "$base" == *AbstractValidator* ]]; then
    continue
  fi
  found="$(find "$TESTS" -name "${base}Tests.cs" -print -quit)"
  if [[ -z "$found" ]]; then
    echo "MISSING TEST: $base -> expected ${base}Tests.cs under $TESTS"
    missing=1
  fi
done < <(find "$API" -name '*Validator.cs' -print0)

exit "$missing"

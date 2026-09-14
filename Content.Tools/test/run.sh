#!/usr/bin/env bash
# Merge the fixture trio: 0A = ours, 0B = base, 0C = other.
set -euo pipefail
cd "$(dirname "$0")"
cp 0A.yml out.yml
dotnet run --project .. -- out.yml 0B.yml 0C.yml out.yml

#!/usr/bin/env bash
# Prints failed test blocks and a job-summary digest from a captured dotnet test log.
# Green runs (status 0) print nothing. With no failed blocks it tails the log, since
# dotnet itself died and its error output is the only witness.
# Usage: report_test_failures.sh <exit-status> <log-file> <summary-header>
#
# Contract: ci-retry-infra.yml matches "  Failed <name> [" in the live job log to
# separate test failures (no retry) from runner shutdowns (retry). Failed blocks
# must keep printing verbatim.

set -euo pipefail

status="$1"
log="$2"
header="$3"

if [ "$status" -eq 0 ]; then
  exit 0
fi

blocks=$(grep -cE '^  Failed ' "$log" || true)
echo "--- failed tests, up to 25 blocks, full output in the test-output.log artifact ---"
shown=0
prev=0
for n in $(grep -nE '^  Failed |Unhandled exception|^The active test run was aborted' "$log" | cut -d: -f1 | head -25); do
  if [ "$n" -gt "$prev" ]; then
    sed -n "${n},$((n + 119))p" "$log"
    shown=$((shown + 1))
    prev=$((n + 119))
  fi
done

if [ "$shown" -eq 0 ]; then
  tail -n 50 "$log"
fi

{
  echo "## \`${header}\`: ${blocks} failed"
  grep -E '^  Failed ' "$log" | sed -E 's/^  Failed (.+) \[[^]]*\]$/- \1/' | head -25 || true
  if [ "$blocks" -eq 0 ]; then
    if [ "$shown" -gt 0 ]; then
      echo "- no failed test blocks: the test process crashed, details in the step log"
    else
      echo "- no failed tests: the test process itself died, see test-output.log artifact"
    fi
  fi
} >> "${GITHUB_STEP_SUMMARY:-/dev/null}"

exit "$status"

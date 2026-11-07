#!/usr/bin/env bash
# CMU14: read-only CI status with real job links; no inferred runner capacity.
# Usage: Tools/ci_queue_status.sh [owner/repo]
set -euo pipefail

REPO="${1:-AU-14/ColonialMarinesUniverse}"
runs=$(gh run list --repo "$REPO" --workflow ci.yml --limit 20 \
  --json databaseId,status,conclusion,headBranch,displayTitle,createdAt,url)

echo "Recent CI runs ($REPO):"
jq -r '.[] | "\(.databaseId) [\((.conclusion | select(. != "")) // .status)] \(.headBranch) - \(.displayTitle)\n  \(.url)"' <<<"$runs"

# Include failed runs so their job links remain visible after the suite finishes.
while read -r id; do
  echo ""
  echo "Jobs in run $id:"
  gh api "repos/$REPO/actions/runs/$id/jobs?per_page=100" --paginate \
    --jq '.jobs[] | "  [\((.conclusion | select(. != "")) // .status)] \(.name)\n    \(.html_url)"'
done < <(jq -r '.[] | select(.status != "completed" or .conclusion == "failure") | .databaseId' <<<"$runs")

#!/usr/bin/env bash
# CMU14: read-only CI status with real job links; no inferred runner capacity.
# Usage: Tools/ci_queue_status.sh [owner/repo]
set -euo pipefail

REPO="${1:-AU-14/ColonialMarinesUniverse}"
runs=$(gh run list --repo "$REPO" --workflow ci.yml --limit 20 \
  --json databaseId,status,conclusion,headBranch,displayTitle,createdAt,url)

echo "Recent CI runs ($REPO):"
jq -r '.[] | "\(.databaseId) [\((.conclusion | select(. != "")) // .status)] \(.headBranch) - \(.displayTitle)\n  \(.url)"' <<<"$runs"

# CMU14: include the latest green run so time to the verdict is visible alongside lane timing.
while read -r id created; do
  echo ""
  echo "Jobs in run $id:"
  gh api "repos/$REPO/actions/runs/$id/jobs?per_page=100" --paginate | jq -r --arg created "$created" '
      # CMU14: report queue and execution time independently, including jobs still waiting.
      def elapsed($start; $end):
        if $start and $end then
          (($end | fromdateiso8601) - ($start | fromdateiso8601)) as $seconds
          | "\(($seconds / 60) | floor)m \($seconds % 60)s"
        else "—" end;
      (now | todateiso8601) as $now
      | .jobs[]
      | (if .conclusion == "skipped" then "—"
         else elapsed(.created_at; .started_at // .completed_at // $now) end) as $queue
      | elapsed(.started_at; .completed_at // $now) as $runtime
      | (if .name == "Required Checks" and .conclusion == "success"
         then " · Full green \(elapsed($created; .completed_at))" else "" end) as $verdict
      | "  [\((.conclusion | select(. != "")) // .status)] \(.name)  Queue \($queue) · Run \($runtime)\($verdict)\n    \(.html_url)"'
done < <(jq -r '([.[] | select(.conclusion == "success")][0].databaseId) as $green |
  .[] | select(.status != "completed" or .conclusion == "failure" or .databaseId == $green) |
  [.databaseId, .createdAt] | @tsv' <<<"$runs")

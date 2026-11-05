#!/usr/bin/env bash
# CI fleet status: live CI runs in the supersession pool, who is running, who is
# waiting, for how long. Read-only.
# usage: Tools/ci_queue_status.sh [owner/repo]   default AU-14 repo.
# CMU14: there is no admission gate and no fleet queue. one live run per PR
# (workflow-level concurrency, cancel-in-progress), everything else goes through
# github's oldest-first runner pool. this tool only reports; it must never grow gates.
set -euo pipefail

REPO="${1:-AU-14/ColonialMarinesUniverse}"

# status-blind: concurrency-waiting runs report pending, not queued, and a
# status-filtered list hides exactly the runs that look stuck
runs=$(gh run list --repo "$REPO" --workflow ci.yml --limit 100 \
  --json databaseId,status,event,headBranch,displayTitle,createdAt,url \
  --jq 'map(select(.status != "completed")) | sort_by(.createdAt)')

age() { # createdAt -> minutes
  jq -rn --argjson now "$1" --arg t "$2" \
    '($now - ($t | sub("\\.[0-9]+Z$"; "Z") | fromdate)) / 60 | floor'
}
now=$(date +%s)

# branch names with an open PR; a PR-event run without one is dead work
open_heads=$(gh pr list --repo "$REPO" --state open --limit 100 --json headRefName --jq '.[].headRefName')

echo "== CI fleet: $REPO =="
n=$(jq 'length' <<<"$runs")
echo "active runs: $n"
if (( n == 0 )); then
  echo "(fleet idle)"
fi
while read -r r; do
  [ -z "$r" ] && continue
  br=$(jq -r .headBranch <<<"$r")
  st=$(jq -r .status <<<"$r")
  ev=$(jq -r .event <<<"$r")
  dead=""
  if [ "$ev" = "pull_request" ]; then
    grep -qxF "$br" <<<"$open_heads" || dead="  [no open PR]"
  fi
  echo "  $(age "$now" "$(jq -r .createdAt <<<"$r")") min  [$st/$ev] $br$dead"
  echo "    $(jq -r .displayTitle <<<"$r" | cut -c1-60)"
  echo "    $(jq -r .url <<<"$r")"
done < <(jq -c '.[]' <<<"$runs")

# CMU14: checkout health. a slow queue with slow checkouts is degraded git transfer
# (github-side), not queue pressure, and the two need different responses. 180s splits
# healthy checkout (~60s for repo plus submodules) from degraded transfer.
SLOW_CHECKOUT=180

echo ""
echo "== Recent run health (checkout seconds, $REPO) =="
recent_runs=$(gh run list --repo "$REPO" --workflow ci.yml --limit 5 \
  --json number,databaseId,status,conclusion,headBranch)

while read -r r; do
  if [ -z "$r" ]; then
    continue
  fi
  num=$(jq -r .number <<<"$r")
  id=$(jq -r .databaseId <<<"$r")
  status=$(jq -r '.conclusion // .status' <<<"$r")
  branch=$(jq -r .headBranch <<<"$r")

  # startswith keeps working across checkout action version bumps; in-flight
  # checkouts have no completed_at and are skipped
  lines=$(gh api "repos/$REPO/actions/runs/$id/jobs" --paginate \
    --jq '.jobs[]
      | (.steps[]? | select((.name | startswith("Run actions/checkout")) and .started_at != null and .completed_at != null)) as $s
      | ((((($s.completed_at | sub("\\.[0-9]+Z$"; "Z") | fromdate) - ($s.started_at | sub("\\.[0-9]+Z$"; "Z") | fromdate)))) | tostring) + "\t" + .name' \
    | sort -rn) || lines=""

  if [ -z "$lines" ]; then
    echo "  #$num $branch [$status]: no completed checkouts yet"
    continue
  fi

  total=$(wc -l <<<"$lines" | tr -d ' ')
  worst=$(head -1 <<<"$lines")
  worst_secs=${worst%%$'\t'*}
  worst_job=${worst#*$'\t'}
  overs=$(awk -F'\t' -v t="$SLOW_CHECKOUT" '$1 > t' <<<"$lines" | wc -l | tr -d ' ')
  flag=""
  if (( worst_secs > SLOW_CHECKOUT )); then
    flag="  [SLOW CHECKOUT]"
  fi
  echo "  #$num $branch [$status]: $total jobs, slowest checkout ${worst_secs}s ($worst_job), ${overs} over ${SLOW_CHECKOUT}s$flag"
done < <(jq -c '.[]' <<<"$recent_runs")

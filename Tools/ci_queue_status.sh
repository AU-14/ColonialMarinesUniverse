#!/usr/bin/env bash
# CI queue status: who holds the PR fleet gate, who is waiting, for how long.
# Read-only. usage: Tools/ci_queue_status.sh [owner/repo]   default AU-14 repo.
set -euo pipefail

REPO="${1:-AU-14/ColonialMarinesUniverse}"
# rough drain per gated PR run: ~35 jobs, 20 runners, longest shard dominates
MIN_PER_RUN=60

runs=$(gh run list --repo "$REPO" --workflow ci.yml --event pull_request --limit 100 \
  --json databaseId,status,displayTitle,headBranch,createdAt,url)
# fleet holders: PR-event runs only. push and merge runs get unique concurrency groups
# and never hold the fleet slot, so counting them says a staging push blocks PRs when it doesn't
# CMU14: holders were any in-progress run, which misattributed the gate during staging churn
holders_all=$(gh run list --repo "$REPO" --workflow ci.yml --limit 100 \
  --json status,event,headBranch,displayTitle,createdAt,url \
  --jq 'map(select(.status == "in_progress" and .event == "pull_request"))')

age() { # createdAt -> minutes
  jq -rn --argjson now "$1" --arg t "$2" \
    '($now - ($t | sub("\\.[0-9]+Z$"; "Z") | fromdate)) / 60 | floor'
}
now=$(date +%s)

# branch names with an open PR; a waiting run without one is dead work
open_heads=$(gh pr list --repo "$REPO" --state open --limit 100 --json headRefName --jq '.[].headRefName')

echo "== PR CI queue: $REPO =="

holders=$(jq -c 'sort_by(.createdAt)' <<<"$holders_all")
hn=$(jq 'length' <<<"$holders")
if (( hn == 0 )); then
  if (( $(jq 'map(select(.status == "queued" or .status == "pending")) | length' <<<"$runs") > 0 )); then
    echo "HOLDING: nothing. PR runs wait but no CI run is in progress:"
    echo "         fleet stalled (org runner quota/capacity or platform). check githubstatus.com"
  else
    echo "HOLDING: (fleet idle)"
  fi
elif (( hn == 1 )); then
  r=$(jq -c '.[0]' <<<"$holders")
  echo "HOLDING: $(jq -r .headBranch <<<"$r") for $(age "$now" "$(jq -r .createdAt <<<"$r")") min  ($(jq -r .displayTitle <<<"$r" | cut -c1-50))"
  echo "         $(jq -r .url <<<"$r")"
else
  echo "NOTE: $hn PR runs in progress at once. some lineages are outside the shared"
  echo "gate (pre-merge or un-rebased ci.yml): the FIFO guarantee does not cover them."
  while read -r r; do
    echo "LIVE:    [$(jq -r .event <<<"$r")] $(jq -r .headBranch <<<"$r") for $(age "$now" "$(jq -r .createdAt <<<"$r")") min"
  done < <(jq -c '.[]' <<<"$holders")
fi

waiting=$(jq -c 'map(select(.status == "queued" or .status == "pending")) | sort_by(.createdAt)' <<<"$runs")
n=$(jq 'length' <<<"$waiting")
echo "WAITING: $n"
while read -r r; do
  br=$(jq -r .headBranch <<<"$r")
  dead=""
  grep -qxF "$br" <<<"$open_heads" || dead="  [no open PR]"
  echo "  $(age "$now" "$(jq -r .createdAt <<<"$r")") min  $br  ($(jq -r .displayTitle <<<"$r" | cut -c1-45))$dead"
done < <(jq -c '.[]' <<<"$waiting")

echo "rough drain for the last waiter: ~$((n * MIN_PER_RUN)) min at ~${MIN_PER_RUN} min per run"

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

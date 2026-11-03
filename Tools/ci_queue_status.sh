#!/usr/bin/env bash
# CI queue status: who holds the PR fleet gate, who is waiting, for how long.
# Read-only. usage: Tools/ci_queue_status.sh [owner/repo]   default AU-14 repo.
set -euo pipefail

REPO="${1:-AU-14/ColonialMarinesUniverse}"
# rough drain per gated PR run: ~35 jobs, 20 runners, longest shard dominates
MIN_PER_RUN=60

runs=$(gh run list --repo "$REPO" --workflow ci.yml --event pull_request --limit 100 \
  --json databaseId,status,displayTitle,headBranch,createdAt,url)
# fleet holders: any CI run in progress, push and merge runs included
holders_all=$(gh run list --repo "$REPO" --workflow ci.yml --limit 100 \
  --json status,event,headBranch,displayTitle,createdAt,url \
  --jq 'map(select(.status == "in_progress"))')

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
  if (( $(jq 'map(select(.status == "queued")) | length' <<<"$runs") > 0 )); then
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

waiting=$(jq -c 'map(select(.status == "queued")) | sort_by(.createdAt)' <<<"$runs")
n=$(jq 'length' <<<"$waiting")
echo "WAITING: $n"
while read -r r; do
  br=$(jq -r .headBranch <<<"$r")
  dead=""
  grep -qxF "$br" <<<"$open_heads" || dead="  [no open PR]"
  echo "  $(age "$now" "$(jq -r .createdAt <<<"$r")") min  $br  ($(jq -r .displayTitle <<<"$r" | cut -c1-45))$dead"
done < <(jq -c '.[]' <<<"$waiting")

echo "rough drain for the last waiter: ~$((n * MIN_PER_RUN)) min at ~${MIN_PER_RUN} min per run"

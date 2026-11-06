#!/usr/bin/env bash
# CI fleet status: pool occupancy, live shard waves, per-PR suite progress, and
# unserved demand. Read-only.
# usage: Tools/ci_queue_status.sh [owner/repo]   default AU-14 repo.
# CMU14: shard waves (ci-shards.yml) admit ~6-leg chunks beside a 3-runner reserve
# for builds and helpers; the dispatcher chains chunks until "Testcases Complete".
# this tool reports that state; it must never grow gates.
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

# ---- wave era: pool, waves, suites, demand ----
POOL=20

api() { gh api "$@" ; }

echo "== Pool ($REPO) =="
live_runs=$(api "repos/$REPO/actions/runs?status=in_progress&per_page=100" --jq '.workflow_runs')
busy=0
wave_ids=""
while read -r r; do
  [ -z "$r" ] && continue
  id=$(jq -r .id <<<"$r")
  nm=$(jq -r .name <<<"$r")
  j=$(api "repos/$REPO/actions/runs/$id/jobs?per_page=100" --jq '[.jobs[] | select(.status=="in_progress")] | length') || j=0
  busy=$((busy + j))
  if [ "$nm" = "CI Shard Wave" ]; then
    wave_ids="$wave_ids $id"
  fi
done < <(jq -c '.[]' <<<"$live_runs")
free=$((POOL - busy))
echo "  in-progress jobs: $busy/$POOL | free: $free (reserve target 3)"

echo ""
echo "== Live waves =="
if [ -z "$(echo $wave_ids | tr -d ' ')" ]; then
  echo "  (none)"
else
  for id in $wave_ids; do
    meta=$(api "repos/$REPO/actions/runs/$id" --jq '[.head_branch, .status, .created_at] | @tsv')
    br=$(cut -f1 <<<"$meta"); st=$(cut -f2 <<<"$meta"); ca=$(cut -f3 <<<"$meta")
    legs=$(api "repos/$REPO/actions/runs/$id/jobs?per_page=100" --jq '[.jobs[]] | {run: [.[]|select(.status=="in_progress")]|length, wait: [.[]|select(.status=="queued")]|length, done: [.[]|select(.status=="completed")]|length}')
    echo "  $(age "$now" "$ca")m [$st] $br  run:$(jq -r .run <<<"$legs") queued:$(jq -r .wait <<<"$legs") done:$(jq -r .done <<<"$legs")"
  done
fi

echo ""
echo "== Recent gate decisions (last 3 waves) =="
api "repos/$REPO/actions/workflows/ci-shards.yml/runs?per_page=3" --jq '.workflow_runs[] | [.id, .conclusion, .created_at] | @tsv' | while IFS=$'\t' read -r id concl ca; do
  gate_job=$(api "repos/$REPO/actions/runs/$id/jobs?per_page=100" --jq '[.jobs[] | select(.name=="Wave Gate")][0].id // empty')
  decision=""
  if [ -n "$gate_job" ]; then
    decision=$(api "repos/$REPO/actions/jobs/$gate_job/logs" 2>/dev/null | grep -oE 'yielding to #[0-9]+|free=[0-9]+ < [0-9]+\+[0-9]+|Testcases / ' | head -1 || true)
  fi
  echo "  $(age "$now" "$ca")m [$concl] ${decision:-served-or-skip}"
done

echo ""
echo "== Suites (open PRs pushed < 6h) =="
api "repos/$REPO/actions/runs?status=in_progress&per_page=1" >/dev/null 2>&1 || true
demand=0
while read -r pr; do
  [ -z "$pr" ] && continue
  num=$(jq -r .number <<<"$pr"); br=$(jq -r .head <<<"$pr"); sha=$(jq -r .sha <<<"$pr")
  run=$(api "repos/$REPO/actions/runs?event=pull_request&head_sha=$sha&per_page=1" --jq '.workflow_runs[0] // empty')
  [ -z "$run" ] && continue
  [ "$(jq -r .conclusion <<<"$run")" != "success" ] && continue
  rid=$(jq -r .id <<<"$run")
  inline=$(api "repos/$REPO/actions/runs/$rid/jobs?per_page=100" --jq '[.jobs[] | select(.name|startswith("Testcases /"))] | length')
  [ "$inline" -gt 0 ] && { echo "  #$num ${br}: old-lineage (inline shards, self-served)"; continue; }
  checks=$(api "repos/$REPO/commits/$sha/check-runs?per_page=100" --jq '[.check_runs[]] | {legs: [.[]|select(.name|startswith("Testcases /"))]|length, marker: [.[]|select(.name=="Testcases Complete")]|length}')
  legs=$(jq -r .legs <<<"$checks"); marker=$(jq -r .marker <<<"$checks")
  m=""; [ "$marker" -gt 0 ] && m=" marker:ok"
  if [ "$legs" -lt 31 ]; then
    inflight=$(echo "$live_runs" | jq -r --arg sha "$sha" '[.[] | select(.head_sha==$sha and .name=="CI Shard Wave")] | length')
    tag="chunk in progress"
    [ "$inflight" -eq 0 ] && { tag="[DEMAND]"; demand=$((demand+1)); }
    echo "  #$num ${br}: $legs/31 legs$m $tag"
  else
    echo "  #$num ${br}: 31/31 legs$m"
  fi
done < <(gh pr list --repo "$REPO" --state open --limit 100 --json number,headRefName,headRefOid,updatedAt | jq -c --argjson cutoff "$(date -u +%s)" 'map(select((.updatedAt | sub("\\.[0-9]+Z$";"Z") | fromdate) > ($cutoff - 21600))) | .[] | {number, sha: .headRefOid, head: .headRefName}')
echo ""
echo "unserved demand: $demand (dispatcher chains these)"

echo ""
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

# CMU CI

`workflows/ci.yml` owns the PR build, unit tests, YAML lint, asset validation,
and gameplay jobs in one run. `Required Checks` collects test evidence and waits
for the complete result; there is no separate `Testcases Complete` job.
Every gameplay check links directly to its own steps and logs. New PR
revisions cancel the previous revision's run.

The [2026-11-06 test audit](CI-test-audit-2026-11-06.md) records the removal
criteria and decisions for every original shard.

## Selection

`Tools/_CMU14/ci.py plan` explains its decision in the Source Guards summary:

<!-- CMU14: planning and build are independent -->
| Changed inputs | Build and YAML lint | Unit tests | Gameplay tests |
| --- | --- | --- | --- |
| Only root, `.github/`, `docs/`, `Docs/`, or `Content.CMU/Docs/` Markdown | Run | Skip | Skip |
| Integration test fixtures | Run | Skip | Their shards and dependent fixtures |
| Unit test fixtures | Run | Run | Only integration consumers of those fixtures |
| Gameplay feature C# sources | Run | When the feature has unit tests or unit consumers | Feature shards plus dependent fixtures |
| Shared test helpers, global infrastructure, resources, configuration, deleted or unknown inputs | Run | Run | All normal shards |

<!-- CMU14: selection uses the tree under test. -->
PRs compare the tested merge commit with its first parent. Pushes and merge queues
compare the tested tree with the event's base SHA. Renames include both paths.
Unavailable or empty diffs run everything.
Unit helpers can be consumed by integration tests, so helper changes retain full
coverage. Named feature directories establish a minimum set of shards; the reverse
dependency graph adds consumers outside that feature. Unit tests remain one group.
Directly changed or dependency-reached nightly fixtures run in a conditional
`nightly_affected` group with an exact fixture filter. The standalone debug workflow
is disabled on the fork.
<!-- CMU14: asset validation runs after the build -->
Asset validation runs attribution, map and RSI checks on every non-draft run.
The separate map checker scans changed map files; checker/configuration changes,
manual dispatch and unavailable diffs scan both supported map trees.

## History, dependencies, and reuse

The source guard looks up at most 20 completed runs for the same branch/event
and repository, plus ten successful default-branch pushes. It reads the latest
observations, a green baseline, and trusted default-branch evidence through
Actions artifacts. No dispatching, polling workflow, or external service is used.
Missing, expired, malformed, or inaccessible history falls back to ordinary
execution. History is retained for 30 days.

The baseline comparison uses the complete tested Git trees, so intermediate
failed commits, deletions, and changes to the base are included. These paths
are unioned with the event diff. An unavailable baseline tree selects the full suite.

A lexical reverse graph follows declared C# types, references to event types,
and YAML prototype IDs through their callers/consumers, including cycles.
Namespaces and imports disambiguate C# types; comments are excluded from declarations.
Prototype definitions are separate graph nodes, and reached systems' component-event
subscriptions connect them to dynamically spawned fixtures. The graph broadens feature
selection and supplies priority hints. It is lexical analysis, not a complete runtime
call graph: reflection and dynamic loading are why feature minimums and broad fallbacks
remain. Git blobs are read in one bounded batch (256 MB, 60-second subprocess timeout),
limited to content C# and prototype YAML. Partial analysis retains full coverage.

TRX records fixture identities, individual durations and outcomes. Previously
failing fixtures run first. Affected fixtures can join that early pass when
history estimates their work at no more than a quarter of the group's elapsed
time. Up to eight fixtures receive early selection. A successful early pass
is excluded from the remaining pass. A stale selection that matches nothing
falls back to the complete original filter. Failure stops that group immediately;
other groups still report their results. Duplicate test IDs across phases fail
validation. There are at most two invocations per group, with one shared timeout.

Reuse requires successful evidence from a default-branch **push**, verified
against separately fetched run metadata. PR history is advisory only. The
fingerprint covers the entire Git tree (including resources, tests, workflows,
and submodule pointers), group selection and worker settings, SDK/runtime info,
runner OS/image/architecture, relevant runtime environment variables, and hashes
of the actual compiled assemblies, dependency manifests, and native libraries. A
change to any of those inputs forces execution. This deliberately uses a broader
input set than the dependency graph. Reused results remain in the run's evidence
and are identified in the compact observations. This is not broad cache-prefix matching.

The final job verifies every expected group's outcome and executed count, and
rejects missing plans or missing/invalid observations. It emits the detailed
test-results report only on failure. Compact successful and failed observations
are saved so later revisions can prioritize failures and balance selected work.
<!-- CMU14: keep partial timing separate from complete observations. -->
Incomplete runs can inform priority and supply timings for fixtures absent
from complete history. An observed timeout raises the group's placement estimate.
Unmeasured groups can use the execution cap, subject to the time reserved for uploads.

## Execution and evidence

<!-- CMU14: build and guards run in parallel. -->
Build & YAML and Source Guards start independently. Asset Validation waits for the build.
Unit and gameplay tests wait for both the build and Source Guards. The build runner
compiles the YAML linter project and its dependencies once.
YAML lint finishes before build/source artifacts are uploaded. Unit tests then
run on their own runner in parallel with the gameplay lanes. Both use those
artifacts instead of fetching and compiling again.
The 31 normal gameplay selectors are in `Tools/_CMU14/ci-shards.json`; the
root selector is derived from top-level integration test files as before, and
<!-- CMU14: include linked test sources and route by fixture name. -->
`tests_unlisted` collects eligible fixtures from both compiled integration
source trees whose declared names fall outside the named selectors.
The planner uses previous durations to target 20 minutes per lane. It starts with at
least six lanes when there are enough groups. At most 14 run at once. Overflow lanes wait.
<!-- CMU14: split using current fixture membership. -->
A group longer than the target is split at fixture boundaries using timings
for fixtures still present in the current sources. The final group keeps tests
without timing history.
If no existing lane can fit another group within the target, the planner adds a
lane. Groups still estimated above the hard execution cap are flagged in the plan.
Greedy placement balances estimated lane time, then each lane runs previously
failing groups first, recursively affected groups next, and the remaining
groups in descending duration. Each lane runs its groups in separate processes
and downloads the build once. The cap is per CI run, not a global runner reservation.

Chemistry and research use three groups with two NUnit workers each:
`cmu_chemistry` (29 methods), `cmu_research_contracts` (12 generated-reagent and
nested-contract methods), and `cmu_research` (13 other research/fire methods).
Generated-prototype tests retain isolated pairs. The separate process budgets
bound their repeated startup work after the previous combined group reached its
30-minute limit; hosted timings will determine the resulting runtime.

Gameplay groups use at most two NUnit workers; the existing memory-heavy groups
remain serial. Four simultaneous cold pairs can spend nearly five minutes in
startup and exceed the runner's physical memory before any test completes.
The prototype-loader workaround, heap limit, and isolated heavy fixtures remain
in `adaptive_ci.py` and the gameplay environment. The test audit
removes UI/camera presentation, static prototype inventories, numeric balance
mirrors, private-helper probes, duplicated scenarios and obsolete benchmarks.
Real authority, state transitions, geometry, resource conservation and lifecycle
contracts remain where the reviewed tests exercise them meaningfully.

`Tools/_CMU14/ci.py run` captures full output and prints failures immediately.
Every 30 seconds it reports elapsed time, completed console results, and the
last reported test. A running case may not emit its result until it finishes;
the heartbeat distinguishes this from a silent runner. Failed runs include TRX
totals, failure details and the five slowest cases in the summary. Green runs
skip that report and the detailed test-results artifact upload.

A nonzero exit, missing/malformed TRX, zero executed tests, failed cases, or
an aborted run fails the job. Previous TRX evidence is removed before execution.
Unit execution has a 12-minute wall limit. Gameplay priority and remaining
<!-- CMU14: retain the measured passing-lane drift allowance -->
passes share a budget based on recorded time plus 36% or five minutes, whichever
is greater. Each lane is cut at 30 minutes of execution or earlier if setup
has used the time reserved for uploads. A lane has a
<!-- CMU14: 35-minute lane cap, fourteen lanes at once -->
35-minute job cap, including setup and uploads,
and up to 14 lanes run at once. With 20 concurrent runner slots, the lanes,
Unit Tests and Asset Validation can occupy 16 slots in one run. Other runs share
that capacity. Existing
per-test hang timeouts remain in effect. DLL-mode execution passes VSTest's
`HangDumpType=none` directly: the SDK's separate dump-type flag can be lost when
forwarding to VSTest, producing a full memory dump and exhausting runner disk.
Sequence files and TRX diagnostics remain available without memory dumps.
Linux cancellation/timeout terminates
the test process group. Logs and TRX results are uploaded on failure or cancellation;
a runner disappearing entirely can still prevent uploads.

Runner shutdowns and artifact upload outages without test failures qualify for
one automatic retry. Test timeouts require investigation. Successful jobs are
retained when failed jobs are rerun.

<!-- CMU14: distinguish queue time from execution time. -->
`Tools/ci_queue_status.sh` shows recent runs, direct job links, separate
queue and execution times, and time to the most recent green verdict. Queue time includes waits imposed by the matrix
parallelism cap. The dispatcher, shard-wave workflow, and sentinel are replaced by
native workflow dependencies. The cleanup workflow still cancels closed or
draft PR runs and helper workflows.

## Verification and maintenance

<!-- CMU14: guards run alongside the build -->
Run `python3 -m unittest discover -s Tools/_CMU14 -p 'test_*.py'` for the planner
and runner regression checks. These exercise real child processes, timeout,
stale/empty/aborted evidence, progress, and conservative selection. Source Guards
runs them alongside the content build. Validate workflow edits with actionlint.

`build-test-debug.yml` defines additional bulk entity lifecycle, localization-reference
and antag takeover tests. Fixed map power budgets
and the upstream recipe-count freeze were removed. Publishing, map rendering/checking, RSI previews, and packaging
keep their dedicated workflows.

Use `Required Checks` as the aggregate required check; remove any branch rule
that still requires the retired `Testcases Complete` name. New PR revisions use
the updated job graph. Actual hosted execution must be checked on the first PR
run, including documentation skips, failures, and cancellation. No runtime
speedup is claimed without timing measurements.

### Regression evidence

The focused Python checks include:

| Behavior | Regression check |
| --- | --- |
| Changes since last green, including failed revisions | `test_baseline_includes_intermediate_failed_revision_and_deletions` |
| Recursive types, events, prototype parents and cycles | `test_dependency_closure_follows_types_events_and_prototype_parents_through_cycles` |
| Balanced scheduling without losing groups | `test_balancing_keeps_every_group_once_and_prioritizes_previous_failures` |
<!-- CMU14: new planner regression rows -->
| Lane count follows estimated time and can span multiple waves | `test_balancing_widens_by_estimated_time_even_when_lanes_require_multiple_waves` |
| Long groups split by recorded time; unrecorded tests keep running | `test_long_groups_split_by_recorded_time_and_keep_unrecorded_tests` |
| Unlisted fixtures include linked sources and partially covered namespaces | `test_unlisted_fixtures_include_linked_sources_and_partially_claimed_namespaces` |
| Historical splits drop removed fixtures and keep partial timings | `test_historical_split_drops_removed_fixtures_and_uses_partial_timings` |
| Workflow runs every planned lane and outlasts the lane budget | `test_workflow_runs_every_planned_lane_and_outlasts_its_budget` |
| Prior failures run first and stay red | `test_failed_priority_stays_failed_without_retrying_until_green` |
| Stale priority falls back to complete coverage | `test_stale_priority_falls_back_to_full_selection_and_both_empty_is_failure` |
| Exact-input reuse and invalidation | `test_reuse_invalidates_on_source_toolchain_and_execution_settings` |
| PR evidence cannot authorize reuse | `test_pr_history_cannot_authorize_reuse_and_mismatched_metadata_is_rejected` |
| Missing groups block success | `test_aggregate_rejects_missing_groups_and_saves_failures_for_next_run` |

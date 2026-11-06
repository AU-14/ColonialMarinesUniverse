# CMU CI

`workflows/ci.yml` owns the PR build, unit tests, YAML lint, asset validation,
and gameplay jobs in one run. `Required Checks` waits for the complete result.
`Testcases Complete` remains available as a required check, backed by a real
job. Every gameplay check links directly to its own steps and logs. New PR
revisions cancel the previous revision's run.

## Selection

`Tools/_CMU14/ci.py plan` explains its decision in the Source Guards summary:

| Changed inputs | Build and YAML lint | Unit tests | Gameplay tests |
| --- | --- | --- | --- |
| Only root Markdown, `.github/` Markdown, or `docs/` Markdown | Skip | Skip | Skip |
| Only integration C# sources, optionally with the above documentation | Run | Skip | Run all normal shards |
| Unit sources, gameplay code, resources, configuration, or unknown paths | Run | Run | Run all normal shards |
| Push or merge queue | Run | Run | Run all normal shards |

Renames include both paths. Missing, invalid, empty, or truncated PR file lists
run everything. The integration assembly references the unit-test assembly;
unit helper changes must therefore retain gameplay coverage. Folder proximity
is not used to infer which gameplay systems a source or prototype can affect.
Asset validation selects attribution, map, and RSI checks separately.

## History, dependencies, and reuse

The source guard looks up at most 20 completed runs for the same branch/event
and repository, plus ten successful default-branch pushes. It reads the latest
observations, a green baseline, and trusted default-branch evidence through
Actions artifacts. No dispatching, polling workflow, or external service is used.
Missing, expired, malformed, or inaccessible history falls back to ordinary
execution. History is retained for 30 days.

The baseline comparison uses the complete tested Git trees, so intermediate
failed commits, deletions, and changes to the base are included. These paths
are unioned with the complete PR file list; history can broaden selection but
cannot narrow that list. An unavailable baseline tree selects the full suite.

A lexical reverse graph follows declared C# types, references to event types,
and YAML prototype IDs through their callers/consumers, including cycles.
This graph supplies priority hints only. Dynamic resource loading, reflection,
and generated dependencies can be absent; every selected group still runs.
Git blobs are read in one bounded batch (64 MB, 20-second subprocess timeout),
with changed files and test sources first. Partial analysis retains full coverage.

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
and are identified in its summary. This is not broad cache-prefix matching.

The final job lists every expected group, its outcome and executed count, and
rejects missing plans or missing/invalid observations. Failed observations are saved as well, so a later revision can
prioritize those failures. No history exists until this version has completed
runs; the first run uses default estimates and executes the selected suite.

## Execution and evidence

The build runner compiles the YAML linter project and its dependencies once.
Unit tests and YAML lint finish before build/source artifacts are uploaded.
Gameplay jobs use those artifacts instead of fetching and compiling again.
The 31 normal gameplay selectors are in `Tools/_CMU14/ci-shards.json`; the
root selector is derived from top-level integration test files as before.
The planner assigns all 31 groups to six lanes using previous durations.
Previously failing groups come first, recursively affected groups next, then
remaining groups in descending duration. Greedy placement balances estimated
lane time; each lane runs its groups in separate processes and downloads the
build once. The cap is per CI run, not a global runner reservation.

The worker counts, prototype-loader workaround, heap limit, and isolated heavy
fixtures remain in `adaptive_ci.py` and the gameplay environment. Existing explicit benchmarks and nightly
exclusions retain their selection policy. No behavioral test was removed.

`Tools/_CMU14/ci.py run` captures full output and prints failures immediately.
Every 30 seconds it reports elapsed time, completed console results, and the
last reported test. A running case may not emit its result until it finishes;
the heartbeat distinguishes this from a silent runner. The summary includes
TRX totals, failure details, and the five slowest cases.

A nonzero exit, missing/malformed TRX, zero executed tests, failed cases, or
an aborted run fails the job. Previous TRX evidence is removed before execution.
Unit execution has a 12-minute wall limit; each gameplay group shares one
30-minute wall limit across its priority and remaining passes. A lane has at
most six groups and a 190-minute job cap, including setup and uploads. Existing
per-test hang timeouts remain in effect. Linux cancellation/timeout terminates
the test process group. Logs and TRX results are uploaded with `always()`;
a runner disappearing entirely can still prevent uploads.

Only runner-shutdown failures without test failures qualify for one automatic
retry. Test timeouts are failures requiring investigation. Successful jobs are
retained when failed jobs are rerun.

`Tools/ci_queue_status.sh` shows recent runs and direct job links for active or
failed runs. The dispatcher, shard-wave workflow, and sentinel are replaced by
native workflow dependencies. The cleanup workflow still cancels closed or
draft PR runs and helper workflows.

## Verification and maintenance

Run `python3 -m unittest discover -s Tools/_CMU14 -p 'test_*.py'` for the planner
and runner regression checks. These exercise real child processes, timeout,
stale/empty/aborted evidence, progress, and conservative selection. Source Guards
runs them before building content. Validate workflow edits with actionlint.

`build-test-debug.yml` runs the additional bulk entity, localization, upstream
policy, and SS14-only tests nightly and on manual dispatch. Explicit benchmarks
remain opt-in. Publishing, map rendering/checking, RSI previews, and packaging
keep their dedicated workflows.

The required check names are retained for migration. After this change lands,
new PR revisions use the unified run; existing revisions may still show legacy
placeholder checks. Actual hosted execution must be checked on the first PR
run, including documentation skips, failures, and cancellation. No runtime
speedup is claimed without timing measurements.

### Regression evidence

The focused Python checks include:

| Behavior | Regression check |
| --- | --- |
| Changes since last green, including failed revisions | `test_baseline_includes_intermediate_failed_revision_and_deletions` |
| Recursive types, events, prototype parents and cycles | `test_dependency_closure_follows_types_events_and_prototype_parents_through_cycles` |
| Balanced scheduling without losing groups | `test_balancing_keeps_every_group_once_and_prioritizes_previous_failures` |
| Prior failures run first and stay red | `test_failed_priority_stays_failed_without_retrying_until_green` |
| Stale priority falls back to complete coverage | `test_stale_priority_falls_back_to_full_selection_and_both_empty_is_failure` |
| Exact-input reuse and invalidation | `test_reuse_invalidates_on_source_toolchain_and_execution_settings` |
| PR evidence cannot authorize reuse | `test_pr_history_cannot_authorize_reuse_and_mismatched_metadata_is_rejected` |
| Missing groups block success | `test_aggregate_rejects_missing_groups_and_saves_failures_for_next_run` |

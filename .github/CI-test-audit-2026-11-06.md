# CMU14 test audit — 2026-11-06

Reviewed all 5,971 content C# test methods and their assertions, parameter inputs,
fixture setup and helper contracts. Removed 3,205 methods (53.7%) and retained 2,766.
These are source-method counts, not expanded NUnit cases or execution results.
The final ledger check found no unreviewed methods, missing retained methods,
surviving removal decisions, overlapping execution groups or empty normal shards.

Removed UI/camera rendering checks, fixed inventory/access/default snapshots,
numeric balance formulas, reflected private-helper probes, historical measurements
and redundant variants. Retained meaningful authority/privacy, transactions,
collision/damage, lifecycle ownership and resource conservation. A bug-report name
alone was not a removal criterion. Additional repeated parameter cases and orphaned
helpers, probe types, injected systems and fixture prototypes were removed.

Deleted 621 entire C# test source files. The standalone 3D tool review removed
288 of 338 Python methods and all ten browser test files (55 named cases plus
ten standalone assertion cases). Fifty generic export/data-integrity/path-safety
tests remain. The six performance-tool parsing/packing tests remain.

## Original shard decisions

Counts follow the inventory's original assignment; surviving camera/role-vote
cases now use the CMU catch-all. Camera and lobby/UI no longer have dedicated
shards. Each remaining method has one unit, normal gameplay or nightly assignment.

| Original group | Reviewed | Removed | Retained |
| --- | ---: | ---: | ---: |
| admin_cleanup_world | 163 | 65 | 98 |
| camera (retired selector) | 75 | 61 | 14 |
| cmu | 627 | 327 | 300 |
| cmu_chemistry | 92 | 38 | 54 |
| cmu_diagnostics | 141 | 90 | 51 |
| cmu_fob | 49 | 4 | 45 |
| cmu_lobby_ui | 17 | 13 | 4 |
| cmu_medical | 408 | 166 | 242 |
| cmu_medical_anatomy | 117 | 22 | 95 |
| cmu_movement | 2 | 0 | 2 |
| cmu_ships | 218 | 89 | 129 |
| cmu_spatial | 162 | 82 | 80 |
| cmu_systems | 331 | 155 | 176 |
| cmu_threed | 24 | 16 | 8 |
| cmu_vehicle_bugs | 336 | 173 | 163 |
| cmu_yautja | 620 | 426 | 194 |
| cmu_yautja_maps | 17 | 15 | 2 |
| cmu_yautja_roles | 73 | 49 | 24 |
| cmu_yautja_smoke | 146 | 94 | 52 |
| cmu_yautja_smoke_blooding | 132 | 103 | 29 |
| cmu_yautja_weapons | 237 | 192 | 45 |
| gameplay_core | 262 | 73 | 189 |
| gameplay_runtime | 87 | 17 | 70 |
| items_storage_crafting | 173 | 49 | 124 |
| map_loading | 2 | 0 | 2 |
| mapping_station_shuttle_round | 65 | 11 | 54 |
| nightly | 20 | 12 | 8 |
| post_map_init | 4 | 0 | 4 |
| post_map_init_nongame | 1 | 0 | 1 |
| proto_lint_serial | 58 | 18 | 40 |
| rmc | 231 | 85 | 146 |
| root | 77 | 20 | 57 |
| unit | 1004 | 740 | 264 |

## CI behavior

The current partition has 31 normal gameplay groups. The 54 retained methods in
the original `cmu_chemistry` group now run as `cmu_chemistry` (29),
`cmu_research_contracts` (12), and `cmu_research` (13), with two NUnit workers in
each process. This bounds repeated generated-prototype startup work without
relaxing pair isolation. The original shard table above remains the audit record.

PR, push and merge-queue changes select feature shards and reverse dependencies.
Unit-only fixtures avoid gameplay work; feature changes skip the unit group when
it has no matching tests or consumers. Shared helpers, unknown/deleted inputs,
resources and incomplete dependency evidence retain conservative full selection.
Changed or dependency-reached nightly fixtures run in a conditional group without
enabling unrelated nightly sweeps.
Changed map files alone reach the separate map checker; checker/config changes
and manual runs check both map trees. Publishing, packaging and renderer workflows
remain manual; the RSI preview is already restricted to changed sprite inputs.

Detailed test summaries and log/TRX artifact uploads run only on failure or
cancellation. Compact observations remain for completeness, failure priority and
future lane timing. Green runs still verify that every selected group executed.

See [CI behavior and maintenance](CI.md). The per-method decision CSV and tool
ledgers are retained in the local task audit directory
`.codex/analysis/test-prune-20261106/`; they are review artifacts, not CI inputs.

## Verification

- `python -m unittest discover -s Tools/_CMU14 -p 'test_*.py'`: 27 passed,
  one Linux-only process-group check skipped on Windows. Seven focused planner
  checks were added for feature/dependency selection and fallback behavior.
- `python -m unittest discover -s Tools/three_d/tests -v`: 50 passed, with
  `Tools/three_d` and the local PyYAML dependency directory on `PYTHONPATH`.
- With `DOTNET_PROCESSOR_COUNT=1`, ran
  `powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~MetabolismSnapshotReuseTest'`.
  Both test assemblies and their shared/client/server dependencies built in Debug.
  NUnit result: **Passed: 4, Failed: 0, Skipped: 0**. The build emitted warnings.
- After investigating CI run `42269696131`, ran the same wrapper with filter
  `FullyQualifiedName~Content.IntegrationTests.Tests.PrototypeTests.PrototypeTests.|FullyQualifiedName~Content.IntegrationTests.Tests.FillLevelSpriteTest.|FullyQualifiedName~GrandMachineThreatStandsTheJoeDownAndEndsItsRule`.
  Result: **Passed: 6, Failed: 0, Skipped: 0**. This covers both sides' prototype
  serialization/round trips, the spawnless threat behavior and the duplicate
  hypospray solution failure. The removed fill-sprite fixture was temporarily
  restored for this check and removed again afterward; no permanent test was added.
- All workflow files passed actionlint using the repository's existing Blacksmith
  runner labels in local configuration. The map-checker script compiled and its
  changed/deleted/configuration/manual selection paths passed a temporary harness.
- Actual repository graph checks selected only `cmu_fob` for a FOB crate-system
  change, no gameplay for an independent unit fixture, no tests for documentation,
  and all normal shards for global infrastructure. Extension-method dependencies
  also reach consuming systems and their tests. The ledger/source and shard
  partition checks passed. `git diff --check` passed.

Validation was local. The complete gameplay suite, nightly sweeps and hosted
GitHub Actions execution were not run. The dependency graph remains conservative
lexical analysis, with feature minimums and broad fallbacks for uncertain inputs.

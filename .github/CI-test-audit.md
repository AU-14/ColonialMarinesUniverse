# CI test audit

2026-10-01 local working-tree audit, based on `7e1524cd403`: 3,397 NUnit test
methods inventoried across unit tests and all 21 integration groups. Removed 23 methods from that snapshot,
leaving 3,374: one empty test, one compiler-redundant type test, and 21
constant/configuration or migration-layout checks.
Counts are source methods, not expanded parameterized cases or passing tests.
The audit covered selection, assertions and helper assertions, fixture fields,
explicit/ignored tests, repeated workloads, waits, and isolation. It is a
static CI audit, not a claim that every gameplay scenario was executed.

## Changes

- Removed `RMCUpstreamTileAndPlatingCheck`: its directory list was empty, so it
  acquired a server/client pair and passed without inspecting any maps. This
  was a disabled upstream-content policy with no active coverage.
- Made `DiagnosticsOverheadMeasurementTest` and
  `AnchoredTileCacheTest.CompareWarmMembershipAndContactSnapshotCosts` explicit
  benchmarks. The diagnostics measurement alone ran 880,000 profiler scopes
  plus repeated parsing of a 65,003-event history. Most other measurement
  fixtures were already explicit and did not cost normal CI time.
- Removed the timing comparison from
  `CachedMembershipTracksAnchorMoveDeletionAndKeepsOldReadersIntact`. Cache
  hits, invalidation, relocation, deletion, and retained readers remain checked.
- Removed a 5,120,000-visit reference-scan comparison from
  `AreaInfoSchedulingTest.IdleUpdatesDoNotScanOrAllocatePerScheduledEntity`.
  It still uses 512 scheduled entities and checks zero allocations over 100
  warmed updates, pending work, schedule size, and deletion cleanup.
- Included `Tests.Performance` in `cmu_diagnostics`. Of its 41 methods, 39 are
  ordinary behavior/allocation regressions and two are explicit measurements.
  Omitting the entire namespace hid useful coverage.
- Set `NUnit.ExplicitMode=None` in normal CI so explicit benchmarks remain
  excluded even if a future filter selects only those tests.
- Removed two `typeof(EventType) != null` assertions from
  `ToolsConstructionMergeRegressionTest`; those cannot fail after compilation.
  Removed `CMUXenoWarlockTest.PsychicCrushTargetsTurfInsteadOfEntity`: production
  already assigns that event to the `WorldTargetActionEvent`-typed `Event`
  field, so the compiler enforces the same inheritance relationship.
  Runtime type checks on deserialized construction steps and UI state remain:
  they validate decisions made by production code or authored data.
- Removed 18 more `CMUXenoWarlockTest` methods that pinned tuning values, asset
  IDs, particle profiles, or hard-coded boolean helpers. The purported shield
  "state transitions" test only called constant-returning helpers with no
  gameplay callers; it never exercised projectile freezing or release.
  Damage scaling, clamping, geometry, authority decisions, and replication
  metadata checks remain. These removals deliberately stop pinning cosmetic
  and balance defaults; they are not all compiler-redundant assertions.
- Removed two `CMUTraumaContactModelTest` methods that copied the default
  contact probabilities. The 15 remaining methods exercise actual contact
  decisions, damage overrides, and mechanism inference.
- Removed `SharedDefaultAndServerOverridesRemainOnTheUnifiedSystem` from
  `ExplosionResistanceHardpointMergeRegressionTest`: it acquired a game pair
  only to inspect inheritance, override owners, and a default ID. The test
  exercising armor installation, replacement, removal, and resistance remains.
- Trimmed obsolete field/event absence and inheritance-layout assertions from
  construction and flammable lifecycle tests, plus constant assertions mixed
  into Warlock behavior tests. Loaded construction prototypes, tool qualities,
  fire damage, ignition transitions, and directional calculations remain checked.

## Parallel execution inside each process

Six fixtures now use `ParallelScope.All`, covering 184 methods on the publication
base before case expansion (181 in the original local audit). Each owns its server/client pair or standalone server within the
test, has no mutable fixture fields, and has no shared setup/teardown hooks:

| Fixture | Methods |
| --- | ---: |
| `CameraNetworkSystemTest` | 65 |
| `ConditionDrivenSurgeryTest` | 42 |
| `MechanismWoundsFoundationTest` | 28 |
| `CMUMedicalFieldMixingTest` | 22 |
| `OrganDamageEffectsTest` | 15 |
| `HeartPhysiologyLifecycleTest` | 12 |

The assembly still caps execution at four workers. This lets methods and test
cases within a large fixture overlap; it adds no runners. Fixtures with shared
UI state, nonparallel markers, or instance setup were not broadly opted in.
NUnit's [parallelism rules](https://docs.nunit.org/articles/nunit/writing-tests/attributes/parallelizable.html)
and [explicit-test settings](https://docs.nunit.org/articles/vs-test-adapter/Tips-And-Tricks.html#explicitmode)
describe the runner behavior used here.

## Decisions by group

The following counts describe the original local audit after pruning, not the
later publication tree. Method counts include explicit and ignored methods. The RMC and CMU counts also
include three and one localization methods excluded by normal CI's policy
filter. The last row contains the other methods intentionally outside the
normal matrix.

| Group | Methods | Decision |
| --- | ---: | --- |
| Unit tests | 935 | Keep behavior, math, parsing, geometry, and allocation tests; make the diagnostics timing experiment explicit and remove 21 compiler-redundant or constant/configuration methods. |
| `root_1` | 51 | Keep round lifecycle, inventory, map initialization, and research checks; expensive map work is runtime validation. |
| `root_2` | 31 | Keep save/load, configuration, prototype saving, and entity lifecycle checks; save/load tests use their own temporary paths. |
| `admin_access_ui` | 81 | Keep access, admin-log, UI, command, and test-harness checks. The date-order test's two-second wait separates real log timestamps. |
| `atmos_fluids_physics` | 83 | Keep simulation, replication, lifecycle, and bounded allocation assertions; remove one explosion-system layout check. |
| `mapping_station_shuttle_round` | 74 | Keep map, station, shuttle, and round-start coverage. Preset sweeps create fresh state and need per-case timings before further narrowing. |
| `cleanup_entities_world` | 79 | Keep deletion, damage, power, wiring, and container behavior; its existing explicit workload stays opt-in. |
| `gameplay_core` | 177 | Keep interaction, body, stun, nutrition, and action regressions. Migration names still cover live behavior. |
| `gameplay_runtime` | 88 | Keep movement, networking, AI, minds, and trigger behavior; recursion checks assert through helpers. |
| `items_storage_crafting` | 135 | Keep authored construction paths and item/chemistry behavior. Construction graph checks are not replaced by YAML syntax validation. |
| `storage_crafting_misc` | 32 | Keep storage/stripping/vending, whitelist, sandbox, and discovery checks. |
| `proto_lint_serial` | 50 | Keep database migration, prototype round-trip, resource parsing, and serialization checks. Guidebook markup parsing is functional validation. |
| `rmc` | 232 | Remove the empty plating check; keep vehicle, xeno, networking, and live prototype contracts. |
| `cmu_medical_anatomy` | 115 | Keep anatomy and physiology coverage; parallelize the 12-method heart fixture. Three explicit methods remain opt-in. |
| `cmu_medical_treatment` | 152 | Keep treatment and surgery behavior; parallelize surgery and field mixing. The UI timing fixture stays explicit. |
| `cmu_chemistry` | 22 | Keep reaction, metabolism, and reagent-generator behavior; no justified removal found. |
| `cmu_ships` | 144 | Keep flight, landing, boarding, vehicle, and map contracts; no new runner split. |
| `cmu_spatial` | 191 | Parallelize camera methods; keep topology, projection, and visibility behavior. Seven existing explicit methods stay opt-in. |
| `cmu_diagnostics` | 89 | Include the 41 performance-namespace methods; retain deterministic regressions and exclude four explicit measurements. |
| `cmu_medical` | 202 | Parallelize wound and organ-damage methods; retain injury, hospital, status, and telemetry behavior. Two explicit methods stay opt-in. |
| `cmu` | 398 | Keep round, species, industry, reconstruction, requisitions, and other CMU contracts. Large allocation workloads exercise real capacity/budget boundaries. |
| `au14` | 4 | Keep the four remaining AU14 behavior/prototype tests; other AU14 files declare CMU namespaces and are counted in `cmu`. |
| Manual entity/localization/freeze checks | 9 | Keep outside normal CI as established in `CI.md`; they remain available in the manual suite. |

Missing direct `Assert` calls were not treated as proof of uselessness: NUnit
`ExpectedResult`, helper assertions, pool/lifecycle validation, sandbox failure,
and intentional exception regressions provide valid failure signals. Ignored
tests already do no test-body work, so deleting them is not a runtime saving.

## Verification and remaining evidence

The cleanup was applied to `AU-14/master` at `47cdbb0adda`, preserving its newer
tests, action versions, prototype-loader workaround, and NUnit selection limit.
The publication tree contains 4,249 test methods. A fresh static partition check
found all 21 groups populated, no overlapping selectors, no unintended omissions,
and 13 intentional policy/bulk-sweep exclusions. C# syntax and actionlint checks
passed on the merged publication files.

The runtime results below are from the original local working tree, before this
publication merge; they do not claim a full test run against the newer master.

The remaining Warlock and trauma unit tests passed: 50 cases, zero failures or
skips, built from the current sources. Roslyn syntax checks passed for the five
test files edited in the second pruning pass, and `git diff --check` passed.

Workflow syntax and static filter partitioning passed: all 21 groups are
populated, no method belongs to multiple groups, and only the 13 intentional
policy/bulk-sweep methods sit outside the normal selectors.

The focused integration run built successfully and reported 34 passed cases,
one skipped, and zero failures in 1 minute 59 seconds. It selected four methods
from each newly parallel fixture, the two shortened cache/scheduling regressions,
and the retained construction, flammable, and explosion regressions. TRX case
intervals show four overlapping cases within both the camera and wound fixtures,
and three within surgery. This verifies concurrent execution for those samples;
it is not a before/after speed comparison or a full-suite stability result.

The skipped case was
`ExplosionResistanceHardpointMergeRegressionTest.ArmorInstallSwapAndRemovalDirtyTheEffectiveResistance`,
reported as "Test was dirty-disposed." Its underlying cause remains unresolved.
An isolated retry could not build because another active test host locked the
shared output DLLs (`MSB3027`/`MSB3021`); it did not execute. The armor regression
is retained, but its behavior was not verified by this audit.

Use the per-group TRX results to identify the next expensive cases. In
particular, camera tests often start standalone servers, preset tests request
dirty pairs, and full prototype/map sweeps have different startup costs.
Changing their reuse or coverage needs runtime evidence. The integration pool
also has its own 45-minute shutdown and 46-minute hard stop, independent of the
workflow timeout; raising a workflow timeout alone will not extend that limit.

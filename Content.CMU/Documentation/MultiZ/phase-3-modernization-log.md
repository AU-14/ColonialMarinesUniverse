# Phase 3: Modernization Log

Status: implementation complete on 2026-07-23. Phase 4 live profiling and multiplayer
validation remain required.

This log records changes made only after their motivating finding was entered in
`audit-log.md`. The implementation preserves the Phase 1 gameplay contract except where Phase 2
proved a correctness defect or where an unsupported legacy behavior is now made explicit.

## Lifecycle and topology ownership

- `CMUZNetworkLifecycleSystem` is the single construction boundary for round loading, mapping
  loading, and `znetwork-combine`.
- Every declared level is loaded and validated before topology is attached. Component overrides,
  topology attachment, and auxiliary `MapInit` run inside a compensating transaction.
- The caller retains ownership of a round's base level. The lifecycle owns and rolls back its
  auxiliary levels; mapping owns every level it loads.
- Caller-owned component state is snapshotted and restored if a pre-commit step throws.
- The public topology event is explicitly post-commit and best effort. A subscriber exception is
  logged while the committed network remains intact because earlier subscriber side effects cannot
  be rolled back safely.
- Map removal repairs both topology indexes and neighboring links and deletes an empty network.
- Low-level topology mutation remains internal to the lifecycle boundary.
- The decision is recorded in
  `docs/adr/0024-transactional-z-network-construction.md`.

Fault-injection integration coverage now exercises returned load failure, auxiliary-map
initialization failure, and a post-commit observer exception. All cases execute rather than being
silently skipped by an already-initialized test map.

## Gameplay and vertical-interaction correctness

- Cross-Z shooting validates that an adjacent map exists before enabling look-up or shoot-down
  and clears stale modes. Source obstruction uses the selected ammo/hitscan or projectile-prototype
  collision mask; generic guns defer the check until ammo selection while xeno abilities provide
  their projectile/line policy before remapping.
- Projectile visual offsets use the actual source and destination maps. Predicted and replicated
  projectile visuals transfer ownership without overwriting unrelated sprite changes.
- `SetLookUp` and `SetShootDown` are the authoritative mutation paths. Enabling validates the
  runtime CVar and adjacent topology, disabling remains unconditional, and the paths keep
  replicated state, actions, and mutually exclusive modes synchronized.
- Shared `CanUseZPhysics` and `ShouldWakeZPhysics` policy now gives client, server, and thrown-item
  integration the same eligibility and support result.
- `ThrownItemSystem` suppresses its ordinary landing event only when enabled Multi-Z physics
  actually accepts the fall. Disabling Multi-Z therefore preserves the ordinary landing path.
- Only committed Z transfers consume the per-tick transition count; failed transfers no longer
  starve later entities.
- The unused `CMUZLevelMoveEvent` and its dispatches were removed after repository-wide
  confirmation that there was no consumer.
- Pure movement, view, and shooting-policy tests now use compile-time-checked internal seams
  through the shared assembly's test-only friend access instead of string-based reflection.

The remaining vertical-support design gap is recorded under MZ-056: general effect projection is
still tile-only while authoritative support also understands high ground. The throw adapter also
preserves the current null activator because the ordinary landing path does not expose an
authoritative thrower at that boundary.

## View, rendering, and presentation ownership

- Viewport eye and clear-color mutation is restored transactionally with `try/finally`, and
  non-Multi-Z fallback frames establish an explicit fresh-clear policy.
- Focused unit coverage verifies render-pass eligibility and input projection; the graphics-backed
  restoration and fallback branches still require harness or live validation.
- Runtime render behavior consumes an exact viewport/eye/map/network visibility snapshot instead
  of the process-global debug statistics object. Telemetry remains global only for the debug
  command.
- Lower-level visibility grace is keyed to the complete view identity, preventing state from
  leaking across eye, map, or network changes.
- Projected lighting consumes the main viewport's published lower-depth snapshot.
- Parallax suppression now follows the viewport's Multi-Z composition state rather than map
  topology alone.
- Weather uses the active Z pass's signed visual offset and ordinary eyes receive no artificial
  half-tile shift.
- Cross-Z speech bubbles project from the exact eye and no longer hit the obsolete same-map
  rejection.
- Visible-entity indicators retain per-viewport world-pass results for the later screen pass.
- Client Z visuals capture and restore the properties they actually own. `SnapCardinals`, falling
  shutdown, component shutdown, runtime disable, and predicted-to-replicated projectile handoff
  preserve external sprite state.
- Steady-state Z-physics presentation updates enumerate an active-only client marker rather than
  every mob, item, structure, and vehicle carrying the shared capability. Predicted direct
  `SetZLocalPosition` changes activate the same marker immediately.
- Client sprite ownership and server fall-activation tile caches were moved off the ubiquitous
  shared component into process-local, active/lazy components.
- The two opening caches previously owned by each process were reduced to the shared system's one
  process-wide cache.
- The unsafe dynamic-culling system was removed because persistent `Sprite.Visible` restoration
  could expose an entity hidden later by another system. Any replacement must filter inside the
  viewport render pass from its per-depth visibility snapshot.

Per-secondary-viewport lighting and receiving-map projected-light shadows remain explicit
visual-validation items under MZ-044 and MZ-061. A render-local dynamic-culling replacement is
deferred until Phase 4 evidence shows that it is worthwhile.

## PVS, networking, and runtime configuration

- Runtime enablement reconciles every attached session, including players attached while Multi-Z
  was disabled. Disabling clears view and shooting modes and removes projected audio/visual state.
- Transform changes mark viewers dirty and rebuild probes once from the settled transform instead
  of rebuilding synchronously from both map and parent events.
- Probe subscriptions remain available while the feature is disabled so re-enable reconciliation
  has complete ownership information.
- Cross-Z effect recipients use the maintained probe/subscriber index rather than another scan of
  all attached sessions.
- Remote-view ownership is recorded when the subscription is added. Removing a subscription still
  reaches the original viewer after the camera moves to a non-Z map; viewer and eye shutdown paths
  clean the same index.
- Duplicate map/parent transform notifications are coalesced through the dirty-viewer set.
  Topology publication still refreshes matching viewers synchronously.
- Immutable `CMUVehicleZTraversalComponent` prototype configuration is no longer auto-networked.
  Component presence remains replicated.
- Render, PVS, audio, and simulation traversal limits now have separate names and CVars:
  `MaxRenderDepth`, `MaxPvsDepth`, `MaxAudioDepth`, and `MaxZLevelTraversalDepth`.
- CVar callbacks publish only thread-safe scalar state or reconciliation flags. ECS mutation,
  cache clearing, probe rebuilds, visual restoration, shooting-mode clearing, and audio-source
  refresh run from the client/server update thread. Long-duration probe and transition settings
  use atomic tick storage to avoid torn `TimeSpan` reads.

Vehicle prediction at unsupported edges and immediate target-level PVS readiness remain latency
tests under MZ-038 and MZ-063. Replicated topology and broad `CMUZPhysics` population require Phase
4 byte/count evidence before further state removal.

### MZ-017 measured PVS follow-up

The Phase 4 16/32/64-viewer baseline identified server LOS result materialization, rather than
opening sorting, as the measured per-viewer allocation driver. The server stair-preview and
visible-opening checks now call Robust's allocation-free occluder first-hit API directly. The
stair predicate still ignores the viewer and stair entities and preserves the previous
endpoint-touching rule.

The change adds no cap, cache, dirty policy, or new scheduling behavior. Across every before/after
pair, visible-opening candidates, LOS checks, stair tiles, viewers, and probe eyes were identical.
Allocation per 4-Hz rebuild fell by 71.2-71.7% across the three viewer counts. Exact metrics and
artifact hashes are recorded in `phase-4-validation-log.md` and
`evidence/mz-017-pvs-scaling.json`.

Targeted validation passed: the Release `Content.Server` and `Content.Benchmarks` builds completed
with zero warnings and errors, and both `CMUZLevelViewerLifecycleTest` integration cases passed in
DebugOpt.

### MZ-001, MZ-045, and MZ-046 measured client-rendering follow-up

A live Release client capture on the five-map Bush scenario extended the existing render telemetry
with an opt-in 3,600-frame distribution sampler and explicit projected-ray, stair-candidate,
stair-LOS, tile, composite, and blur-pass counts. The sampler preallocates normal per-frame storage
and creates percentile scratch arrays only after a requested capture finishes.

The projected-light candidate pass performed 104 source-to-opening physics rays each frame. Its
existing physics query materialized a result list even though the caller only needed a Boolean
first hit. Replacing that predicate with the general occluder API was rejected: it disagreed with
12 of 104 existing physics decisions on every comparator frame. The accepted CMU-owned predicate
instead traverses the current frame's physics broadphases and preserves the exact existing maximum
distance, ignored source entity, opaque collision-layer, and hard-fixture checks. It matched all
374,400 old/new decisions in a 3,600-frame comparator run before becoming authoritative.

The same allocation-free occluder first-hit API now replaces the result-materializing Examine
query for current-view opening checks and stair-preview sprite/tile visibility. Endpoint touching
remains ignored, matching the previous Examine behavior. No visibility cap, cache, scheduling
policy, or prediction behavior was added.

In the paired stair capture, work remained exactly 104 projected rays, 9 projected candidates,
5 applied lights, 349 stair sprite candidates/checks, 349 stair LOS checks, one lower pass, and
one stair composite per frame. Projected-light p50/p95 fell from 0.2661/0.3986 ms to
0.2131/0.3307 ms; stair-cull p50/p95 fell from 0.4922/0.7479 ms to 0.4277/0.6569 ms. Full metrics,
artifact hashes, and the rejected comparator are recorded in `phase-4-validation-log.md` and
`evidence/mz-001-045-046-client-rendering.json`.

Blur was measured but not rewritten. In the one-lower-pass, 1280x720 scene, enabling it added
4.26% to frame p50, 5.66% to render p50, and 10.15% to lower-pass p50 relative to blur disabled.
The final capture recorded one blur pass per frame. Consolidating sequential multi-depth passes
would change accumulated blur strength, and the available capture did not isolate GPU
copy/shader timestamps, so a behavior-preserving production change is not justified.

Targeted validation passed: the Release `Content.Client` build completed with zero warnings and
errors, and the DebugOpt blur/stair filter passed 14/14 tests. The sampled stair scene did not
exercise nonempty FOV-mask tiles; a tile-heavy stair scene and isolated GPU timings remain open.

### MZ-014 and MZ-040 measured topology-burst follow-up

The Phase 4 evidence runner now brackets the five-map lifecycle load and teardown with profiler
captures. The baseline separated the required initial work from redundant event fan-out. Initial
roof construction visited and wrote 20,095 tiles across all five maps in 1.1536 ms with 322,640
scoped bytes; it remains a required full rebuild. The topology-triggered power path, however,
enumerated 47 APCs, 1,174 receivers, and 23 reactor groups in 0.1284 ms even though all 1,221
APC/receiver area updates were already pending from `MapInit`.

`CMUZLevelNetworkUpdatedEvent` now identifies a rebuilt network versus a removed map/depth. Map
removal resolves its network through `CMUZLevelMapComponent.NetworkUid`, retaining the old network
scan only as stale-index recovery. Roof removal work seeds state from surviving higher maps and
writes only levels below the removed depth; removing the lowest remaining level performs no roof
work. Initial network construction retains the unchanged full top-down build. RMC power now queues
only the affected Z-network's reactor power group and leaves the existing `MapInit` area-update set
untouched.

In bottom-up Bush teardown, all five ownership lookups were direct and fallback scans remained
zero. Four broad roof rebuilds and 38,298 roof writes fell to zero. Topology-removal p50/max/sum
fell from 0.2816/0.8716/1.7023 ms to 0.0279/0.1358/0.2145 ms
(-90.09%/-84.42%/-87.40%). The after load still performed exactly five-map/20,095-tile initial
roof work and exactly 1,221 pending power area updates; the redundant global power requeue was
absent. Single-run whole-load and wrapper timing differences are not treated as stable
improvements.

Targeted validation passed: the Release `Content.Benchmarks` build completed with zero warnings
and errors, both paired raw captures passed every hard five-map/headless gate, and the DebugOpt
Bush lifecycle/marker filter passed 2/2. The lifecycle test verifies that deleting a middle level
clears its roof contribution on the lower level, so the optimization is not based only on
bottom-up teardown.

The checked evidence is `evidence/mz-014-040-topology-bursts.json`. Bush contained no
reactor-powered-light entities, the required initial build remains synchronous at approximately
323 KB, and large middle/upper removals plus bulk mapping edits remain explicit risks. Shared RMC
power domains are unchanged.

### MZ-008, MZ-037, MZ-052, and MZ-053 measured replication follow-up

The Phase 4 runner now captures the generated component state for every topology and Z-physics
instance after the five-map Bush load. The baseline established 106 bytes of duplicated topology
state (49 on the network plus 57 across five maps) and 84,140 bytes across 6,010 serialized
`CMUZPhysics` states (14 bytes each). Bush had no active falling markers or vehicle traversal
components, so those absent populations were not treated as optimization evidence.

`CMUZLevelsNetworkComponent.ZLevels` is now the sole replicated topology. Its reverse lookup and
the four map-local owner/depth/neighbour fields are derived after client state application and map
startup. `CMUZPhysicsComponent.Bounciness` remains prototype data after repository-wide review
found no override or runtime assignment. The transient `CMUZFallingComponent` is now a server-only
active-set marker; one replicated `CMUZPhysicsComponent.Falling` boolean supplies the client visual
and predicted vehicle-drift signal. Every authoritative wake/sleep/stop path updates that signal.
Vehicle traversal remains a networked marker whose immutable prototype fields have no generated
state.

The isolated five-map state capture reduced topology from 106 to 28 bytes (-73.58%) and Z physics
from 84,140 to 66,110 bytes (-21.43%, 14 to 11 bytes per instance). A synthetic representative
vehicle traversal component produced a null state and zero state-payload bytes. A connected
five-map test verifies initial and reconnect topology exactly, and a second connected test verifies
falling true/false replication, absence of the server marker on the client, and vehicle defaults.

The real late-join milestone pair observed a 929,316-byte baseline full state and a 929,192-byte
after full state (-124 bytes, -0.013%), but both reached the server `InGame` transition at exactly
226,278 sent transport bytes. Twenty-second steady windows varied more than the change and are not
claimed as an improvement. With 100 ms fake lag on both endpoints, the client still completed the
full state, entered `InGame`, and remained connected; no vehicle edge transition occurred, so
prediction quality under latency remains open.

The checked evidence is `evidence/mz-008-037-052-053-replication.json`. It distinguishes isolated
component serialization from actual full-state and transport measurements and records the absent
Bush falling/vehicle populations as deferred risks.

### MZ-057 and MZ-062 measured gameplay-burst follow-up

The Phase 4 runner now forces Bush area/roof ordnance permission only inside the capture, selects a
stable coordinate at map 4 `(-18.5, 34.5)`, and repeatedly resolves the same live column. Each call
visited all five depths and maps and returned the same three blocking surfaces. The baseline
allocated exactly 168 bytes per call for the result/list while p50/p95 was only
0.0020/0.0021 ms, identifying allocation rather than CPU as the continuous-firing bottleneck.

`CMUTopDownOrdnanceSystem` now accepts a caller-owned resettable result. Only the orbital cannon's
per-tick firing update uses it; launch validation and the low-frequency mortar, rangefinder, and
area-info callers retain the convenient allocating API. The firing loop still performs the full
topology, tile, and area resolution every tick, so live retargeting behavior and invalidation
semantics are unchanged. The after capture retained 5/5/3 depth/map/surface counts and reduced
scoped allocation from 168 to zero. Its 0.0019/0.0023 ms p50/p95 does not establish a CPU
improvement.

The same runner raises a hard-landing event against a synthetic 24-occupant interior: 16 occupants
are in the tracked passenger set and 8 are discoverable only through the interior marker query.
Collision sound and occupant damage were both enabled, wheel/footprint damage disabled, and every
profiled landing found and applied damage to all 24 targets. The baseline performed two complete
interior scans per landing.

Landing sound and damage now share one event-local occupant snapshot. Discovery samples and total
candidate enumeration fell 128-to-64 and 3,072-to-1,536 over 64 landings, while target/applied
counts stayed 24 per landing. Hard-landing p50/p95 fell from 0.0228/0.0269 ms to
0.0127/0.0143 ms (-44.30%/-46.84%); the 100-landing unprofiled loop fell from 1.6422 to
1.3406 ms (-18.37%). Allocation changed only 19,624-to-19,584 bytes per landing because the
dominant 12,096-byte occupant-damage path is intentionally unchanged.

Mutable per-victim damage specifications were not pooled or shared. Damage events can independently
cancel or customize each target, so eliminating that measured allocation would need a stronger
`DamageSpecifier` ownership contract rather than a CMU-local cache. Checked evidence is
`evidence/mz-057-062-gameplay-bursts.json`.

## Cross-Z audio and acoustic policy

- One shared acoustic path builder defines each crossed boundary: downward traversal checks the
  current deck's floor, upward traversal checks the destination deck's floor, and every selected
  opening becomes the next search origin.
- Server speech and projected audio are separate adapters over that boundary model rather than
  duplicate topology traversal.
- Supported one-shot audio projections are owned by their source and are rebuilt for movement,
  topology, depth, enable, and audio-CVar changes.
- New audio sources are evaluated on the next server update, after the engine has finalized their
  recipient filter. Global, looped, stopped, and private/empty-filter sources are revalidated before
  any projection is created.
- Source or projection shutdown removes both sides of the ownership index and stops remaining
  projections.
- Looped sources are deliberately not projected; this prevents immortal detached loops until a
  synchronized cross-map looping-audio contract exists.
- Audio has its own traversal-depth CVar.

Depth attenuation remains a gameplay policy decision under MZ-055. Phase 3 preserves the legacy
2D attenuation on each receiving map.

## Allocation, query, and event-flow reductions

- Disabled falling diagnostics use a conditional interpolated-string handler. Formatted values,
  including tile-type conversion, are evaluated only when diagnostics are active.
- Vehicle footprint sampling generates each Cartesian sample once, reuses caller-owned lists, and
  no longer allocates axis lists or performs quadratic duplicate searches.
- A warmed 1,000-call footprint characterization reports zero managed allocation when the caller
  provides sufficient capacity.
- Roof tile propagation follows direct `MapBelow` links and reuses one system-owned Boolean list
  instead of a dictionary plus per-level tile lists.
- Viewport visibility state is owned by each control; only active viewport IDs are tracked during
  synchronous rendering. Runtime CVar flags use `Interlocked` integer access, keeping these paths
  compatible with the Robust content sandbox.
- RMC's unused reactor-light accumulation index was removed.
- Topology and opening lookup profiler counters distinguish direct/index hits, recovery scans,
  visited networks, recovered hits, misses, candidates, and writes without adding normal-gameplay
  counter cost while profiling is disabled.

The synchronous roof/topology pass, movement support probes, transition burst policy, projected
lighting, stair preview, blur, and transient projectile/follower visual sweeps remain
evidence-gated Phase 4 work.

## Static .NET performance scan

The final Phase 3 scan covered 124 unique production C# files and 30,727 nonblank lines: the CMU Multi-Z
modules, their direct modified host adapters, and the added CMU compatibility systems. Tests,
`RobustToolbox/`, `RSI.NET/`, and generated `BuildChecker/` trees were excluded.

- No Multi-Z string `IndexOf`/`Substring` hot path or repeated replacement chain was found.
  Reported `Contains` calls are typed collection or geometry membership checks; the string casing
  hits are pre-existing chat/mortar adapter code rather than new Multi-Z policy.
- Per-call collections remain in lifecycle/error paths, command completion, source-owned audio
  registration, and already-documented rendering/ordnance paths. No new unrecorded hot-path
  collection issue was identified.
- No current-culture comparer was introduced.
- The structural scan found 139 explicitly sealed production class declarations and no matching
  plain public/internal unsealed class declaration in the audited set.
- Existing projected-light, ordnance, support-query, and potential render-local culling findings
  remain in the audit log; Phase 3 does not claim that static shape proves their runtime cost.

## Validation evidence

- `Content.IntegrationTests` DebugOpt build: 0 warnings, 0 errors.
- Headless content sandbox verification: all client-loaded assemblies passed module type checks.
- Focused shared/client Multi-Z unit suite: 122 passed.
- `USSBushMultiZTest`: 5 passed across segmented runs, including five-level construction, source
  obstruction, rollback, post-commit observer retention, combination, topology repair, and
  teardown.
- `CMUZLevelAcousticPathTest`: 5 passed, including private-filter isolation.
- Remote-view/runtime viewer lifecycle tests: 2 passed.
- Disabled Multi-Z thrown-item landing fallback: 1 passed.
- Earlier Phase 1 full Bush loadability validation remains recorded in `phase-1-port-log.md`.

These are deterministic code and integration checks. They are not substitutes for the live
multiplayer, packet, CPU, GPU, and allocation captures required by Phase 4.

## Deferred to Phase 4 or explicit design

- Two-client latency, prediction, reconciliation, late join, reconnect, and remote-camera sessions.
- Populated-Bush server tick, client frame, allocation, PVS, and packet captures.
- Vehicle falling activation under latency (MZ-038).
- Secondary-camera lighting and receiving-map projected-light shadow validation (MZ-044/MZ-061).
- Profile whether a safe render-local dynamic-culling replacement is justified (MZ-015).
- Immediate cross-Z interaction versus PVS readiness (MZ-063).
- Candidate/sample caps, caching, or tick budgeting only where profiles demonstrate material cost.
- Physical atmosphere coupling, AI cross-map navigation, audio depth attenuation, power-domain
  separation, persisted networks, and atomic mapping-save manifests require gameplay/data-format
  decisions rather than speculative Phase 3 behavior changes.

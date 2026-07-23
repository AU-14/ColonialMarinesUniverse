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

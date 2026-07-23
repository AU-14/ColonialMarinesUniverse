# Phase 1: Functional Port Log

## Scope

Restore the final pre-rebase Multi-Z behavior from `origin/Zlevels`, including the Multi-Z USS
Bush Redux, without redesigning the system during the compatibility port.

## Legacy subsystem inventory

- Shared Z-network state, map ordering, transforms, falling, movement, throwing, interaction,
  shooting, viewer state, opening caches, roofs, weather, ordnance, and vehicle traversal.
- Server activation, movement, view/PVS overrides, audio, chat, ladders, ghost movement, roof
  updates, weather commands, and mapping commands.
- Client viewport projection, visibility overlays, sprite culling, projected lighting, blur,
  stair previews, and rendering diagnostics.
- Content hooks for projectiles, guns, throwables, movers, melee, atmosphere, dropships, cameras,
  speech, weather, tactical maps, power/comms, evacuation, mortars, orbital cannon, vehicles, and
  xeno abilities.
- Prototypes, keybinds, locale, shaders, tile textures, ladders, vehicle viewer components, tests,
  and five USS Bush Redux map layers.

## Port rules

- New CMU-owned code and unique resources live under `Content.CMU/`.
- Existing canonical resource files are patched in place only where the legacy system requires
  an integration entry; no relative resource path may exist in both resource roots.
- Current upstream-derived files receive the smallest viable integration hook.
- Compatibility fixes required by current APIs are allowed; behavioral redesigns are deferred to
  the audit backlog.
- `RobustToolbox/` and `RSI.NET/` are not modified.

## Validation matrix

| Area | Phase 1 evidence required | Status |
| --- | --- | --- |
| Shared/client/server compilation | Targeted builds succeed with nullable/analyzer warnings treated normally | Automated gate complete: integration project build succeeds with 0 warnings and 0 errors |
| Prototype and map load | USS Bush Redux prototype resolves and all five layers initialize as one Z-network | Automated gate complete: depths -1, 0, 1, 2, and 3 load and link correctly |
| Movement and ladders | Traversal, falling, pulling, stairs, ghost movement, and deployable ladders work | Partial: focused movement/ground-snap/vehicle tests pass; live traversal matrix remains |
| Interaction and combat | Interaction, melee, throwing, projectile prediction, and cross-Z targeting agree | Partial: focused shared tests and compilation pass; live predicted combat matrix remains |
| Visibility and rendering | PVS, occlusion, sprites, lighting, weather, blur, cameras, and placement previews agree | Partial: focused viewport/blur/stair tests pass; GPU/client visual validation remains |
| Simulation | Atmos, power/comms, AI/pathfinding, dropships, vehicles, evacuation, and ordnance remain stable | Partial: map/prototype integration passes; live simulation validation remains |
| Multiplayer | Server authority, client prediction, reconciliation, and late-join state remain stable | Partial: connected client/server integration pool passes; two-client live session remains |
| Stability | Client and server soak test without recurring exceptions or entity leaks | Partial: clean five-map load and teardown passes; long-running soak remains |

## Automated evidence

- `Content.IntegrationTests` builds in `DebugOpt` with 0 warnings and 0 errors.
- At the Phase 1 handoff, all three then-present `USSBushMultiZTest` cases passed. They verified all five depths, map/network backlinks,
  marker migration, known unresolved marker count, concrete replacement entities, transactional
  auxiliary-load rollback, existing-map combination/rejection, stacked roof propagation and
  removal, map teardown, and deletion of empty networks.
- The generic `GameMapsLoadableTest` passes when filtered to `USSBushRedux`.
- At the Phase 1 handoff, the focused `CMU14.ZLevels` unit-test filter passed all 91 then-present tests, including the warmed
  allocation assertion for vehicle support sampling.
- The Bush load consumes all active legacy ship markers. It intentionally retains 146 markers
  for removed AU gameplay: 144 objective return points, one alliance console, and one
  withdrawal console.
- Full YAML-linter execution currently has 523 repository-baseline errors; none are scoped to
  `_CMU14`, `_AU14`, USS Bush, `CMUZ`, or Multi-Z content.

## Phase 1 fixes discovered during validation

- Deferred entity-storage open/close work now skips entities whose storage component was removed
  before the queue flushed.
- Map removal now removes both network indexes, clears adjacent map links, refreshes surviving
  viewers, and deletes a network after its final map is removed.
- Legacy Bush vendor, door, console, lift, destination, and command-tablet markers now resolve to
  current concrete prototypes at map initialization. Requisitions uses
  `ColMarTechCargoGuns`, avoiding the abstract `ColMarTechCargo` prototype.

## Running decisions

- 2026-07-23: Use the final `origin/Zlevels` tree as the feature-parity reference because it
  contains the cumulative fixes and validation tests added after the original Multi-Z merge.
- 2026-07-23: Port CMU-owned files into the current `Content.CMU/{Shared,Server,Client}` layout
  while retaining compatible namespaces to avoid migration-only churn.
- 2026-07-23: Preserve removed AU objective/alliance/withdrawal markers as a documented parity
  gap instead of silently mapping them to unrelated current gameplay.
- 2026-07-23: Treat automated Phase 1 gates as complete while keeping live multiplayer,
  rendering, simulation, and soak validation explicitly outstanding.

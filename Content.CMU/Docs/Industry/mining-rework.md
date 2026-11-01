# Mining rework: cavern exploration

The agreed direction keeps active exploration, safe mine construction, manual
extraction, and industrial production. Underground generation is the first
implementation pass. The other accepted mining changes below remain planned.

## What the underground should feel like

Dig through the shallow cover and emerge onto a ledge overlooking a chamber
several levels tall. Establish a return route, survey the exposed walls and
floor, then choose where to take equipment. Galleries connect neighbouring
chambers; natural drops make ropes, ladders, stairs and bridges useful. Deep
mining should involve choosing a route and building access to a resource.

The current implementation samples a shared three-dimensional volume rather
than generating unrelated cave patches on each map. Default chamber diameters
are roughly 55–80 tiles before wall variation. Broad connecting galleries cross
region boundaries. Six levels of possible cavern space sit above a solid
separator; the next group begins below it. Floor height follows the chamber
shape, producing shelves around a lower floor. Separators give shaft digging a
purpose and bound how much terrain must be prepared below a natural opening.

These are initial geometry values, not a final balance decision. There are no
new cave creatures, biomes, underground structures or traversal tools in this
pass. Existing depth-specific rock, ores, water and lava are retained. Natural
routes can bypass a digging-tool gate; tool requirements still apply when
cutting rock, but should not be treated as an absolute access lock.

## Loading and preservation

- The cavern shape is a function of a source-map seed and coordinates. Its
  continuation does not depend on which entrance or chunk players discover first.
- Entity creation still uses the existing eight-tile chunks. Only a local
  neighbourhood of living players requests chunks; observers do not explore.
- Prefetch requests cover three chunks in each horizontal direction and the
  current vertical cavern group. Requests run bottom-up so a natural opening
  never exposes a destination whose terrain has not been prepared.
- Starting a downward digging action queues its destination while the floor is
  still sealed. Terrain preparation runs during the digging delay. Cancellation
  stops the request; any completed chunks retain their state for a later visit.
- Background generation starts at most two chunks per update and checks a
  four-millisecond allowance between chunks. A single chunk is atomic and can
  exceed that allowance. Immediate digging/landing and near-boundary safety
  preparation are synchronous, so this is not a hard frame-time guarantee.
- Requests no longer near players are discarded. Already generated terrain is
  retained for the round: excavations, bridges, ore, equipment and floor changes
  are not regenerated on a later visit.
- This is lazy generation, not save-to-disk unloading of explored chunks.
  Server memory can still grow with the total explored area. Existing networking
  and visibility systems handle what clients receive; no engine code was changed.

`ZBuildableMap.caverns` configures `startDepth`, `regionSize` and `bandHeight`.
Setting `caverns: null` retains independent legacy layers. Mapper-authored station
levels use the existing localized generation path and are not hollowed out by
the new volume. These settings should be selected before a round generates its
underground; changing them mid-round is not a terrain migration.

## Structural and mining integration

Natural cave space is protected from cave-in initiation and propagation. It does
not act as a support pillar for newly excavated tunnels. This distinction fixes
the imported natural-cave marker being recorded but ignored by collapse checks.
The scanner samples the new cavern shape from the shared seed so natural rooms
are not painted as unsupported player excavation.

Ore deposits and pools are placed on actual cavern floors, never in open shafts.
Ore-bearing rock can remain in walls and pillars. Constructed stairs get a
landing at their lower end; an ordinary dug pit can open into a deeper drop.

## Accepted mining work still to implement

1. Remove repeatable refined-metal/byproduct loops. Use distinct concentrates or
   finite recovery stages so processing cannot indefinitely reuse the same input.
2. Give deposits a dominant ore, limited byproducts and useful survey readouts.
3. Set deposit lifetimes from round duration, colony demand and desired mine
   lifetime. Support a dependable main mine plus valuable exploration targets.
4. Give manual, steam and electric extraction distinct practical roles.
5. Give underground regions clear resource purposes and engineering challenges.
6. Improve support warnings and material transport around the expedition/outpost
   loop, using the existing structural scanner and construction systems.

## Validation targets

Focused coverage checks large connected geometry, negative region coordinates,
vertical openings with real ground below, lazy exploration, observer exclusion,
preserved edits, and natural-cavern collapse boundaries. Existing construction
and structural lifecycle regressions protect authored maps and stairs.

A representative multiplayer exploration session is still needed to judge
visual readability, transport difficulty, worst-case generation stalls and
memory growth over a long round. Unit/integration timings are diagnostic, not a
server capacity claim.

### Verified in this pass

The shared, server and client projects compiled through the repository test
wrapper. Two geometry tests and 30 cavern/construction/structural integration
tests passed. Commands from the worktree root:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.Tests -Filter 'FullyQualifiedName~CMUCavernFieldTest'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUCavernGenerationTest|FullyQualifiedName~StructuralLifecycleTest|FullyQualifiedName~TileApplierSystemTest'
```

The final exploration fixture materialized 39 of 343 nearby chunks after half a
second of simulated time. The landing request after background dig preparation
took 0.2 ms in that run. Earlier cold synchronous column requests took
approximately 250–275 ms; batching floor updates alone did not reduce that
measurement. Preparation now moves terrain/entity work into the existing digging
delay. The synchronous fallback is still used for unprepared direct placement
or boundary arrival and can still produce a burst. These single-run observations
do not establish a maximum frame time or player capacity.

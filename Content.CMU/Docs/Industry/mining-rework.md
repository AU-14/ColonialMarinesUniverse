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
than generating unrelated cave patches on each map. Chambers range from narrow
pockets to large halls, with independent scale and aspect ratio. Branching
passages cross region boundaries. Six levels of possible cavern space sit above a solid
separator; the next group begins below it. Floor height follows the chamber
shape, producing shelves around a lower floor. Separators give shaft digging a
purpose and bound how much terrain must be prepared below a natural opening.

These are initial geometry values, not a final balance decision. Eight geological
variants now change cavern shape and scenery. There are no new cave creatures,
underground structures or traversal tools in this pass. Existing depth-specific
rock and ores are retained; cavern liquids follow the local geology. Natural
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

`ZBuildableMap.caverns` configures `startDepth`, `regionSize`, `bandHeight` and
optional `variant` and `layout` overrides (for example `PillarCathedral` and
`Crescent`). Without overrides, each region mixes two to four chambers with their
own geology and structural recipes, independent of chunk boundaries.
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

### Verified in the first cavern pass

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

## Geological variants

The shape families are inspired by the Colony Industry
[ambient caverns design](https://github.com/TheHellFireo/CMU-Colony-Industry/blob/main/Content.CMU/Design/AmbientCaverns.md).
Their implementation here samples a continuous multi-level volume, rather than
creating the reference implementation's finite single-grid cave all at once.
The region seed varies orientation, bends, proportions and placement. Regional
galleries connect independently of those local shapes.

| Variant | Generated terrain and appearance | Expedition decisions |
| --- | --- | --- |
| Braided Galleries | Three winding galleries, cross-links, amber mineral growths | Mark junctions and establish a main haul route through the loops. |
| Flooded Fork | A trunk splits toward two broad basins; water and limestone outcrops | Choose dry ledges or accept slower wading; bridge gaps before moving machinery. |
| Pillar Cathedral | A broad chamber, several Z-levels of open height, rock columns and pale crystals | Descend from the rim, build bridges and survey exposed walls. |
| Lava Tubes | Winding conduits, widened pockets, side spurs and exposed lava | Keep equipment and people off the lava; establish a protected crossing. |
| Leaking Cavern | Offset drainage galleries, narrow fingers, small pools and outcrops | Follow branching shelves and plan passages around wet ground. |
| Biological Dead Zone | Lobed chambers and crooked links; dead roots and fungal caps | Explore connected pockets; dead growth is scenery, not a harvestable crop. |
| Thermal Springs | Overlapping basins with rock peninsulas and mineral water | Place staging areas on dry rock; wading slows movement. |
| Vent Labyrinth | Angular fault corridors, switchbacks and a tall chimney; dormant vents | Mark the route through repeated intersections and secure vertical access. |

Only natural floors receive liquid/scenery; open shafts never receive floating
props. Water slows movement and uses the existing water interactions. Lava uses
the existing ignition behavior. Mineral springs currently behave as tinted
water. Vents, roots, fungi and crystals are non-blocking scenery; ore is supplied
by the independent ore-vein/deposit system. Decorative props are sparse (at most
one candidate per four-by-four cell), with no per-prop update loop or point light.
Liquid patches follow continuous noise, leaving dry centres along regional
galleries. This does not guarantee a walkable route across every vertical drop.

The reference project's telegraphed gas/steam eruptions, treatment tools, staged
passage obstructions and buried discoveries are not included in this pass.
They can build on the new geology after traversal and resource balance are
reviewed. No new art was copied: local RSI assets retain their original metadata.

The interactive terrain atlas samples the compiled `CMUCavernField` at two seeds
across Z-2 through Z-10. Its top-down plans and cross-sections are actual sampler
output. Its close-up uses native asset frames with schematic wall edges; it is
not an in-game screenshot and does not reproduce lighting, smoothing or ore
rolls. The loading view illustrates queue order, not measured elapsed time.

### Verification of the first eight-family pass

The same focused commands above passed with nine geometry cases and 31
integration cases, and rebuilt the shared, server and client content projects.

- `CavernsSpanLevelsHaveWideChambersAndTerminateAboveBedrock` samples the cathedral
  volume to check chamber width, vertical continuity and the solid cover/separator.
- `NeighbouringRegionsConnectAcrossNegativeChunkCoordinates` traverses the
  generated geometry for each of the eight variants.
- `FloodedGeologyPlacesWaterOnGroundButNotInTheShaftAbove` uses real chunk
  generation to check a liquid entity on the floor and an empty shaft above it.
- Existing cavern tests cover falls, preserved edits, staged exploration,
  observer exclusion and collapse boundaries. Structural lifecycle and tile
  application checks remain green.

The atlas was checked in Edge at 780, 360 and 320 pixels, in light and dark
appearance, with all variants, all four views and the loading control. No script
errors or horizontal overflow were found. Full multiplayer visual playtesting
and long-round performance measurements remain outstanding.

## Eighty recipes and a branching underground

There are eight geological kernels and ten structural arrangements. Every pair
is a different recipe, giving eighty combinations before seeded variation.
These are procedural recipes, not eighty copied map rooms. Ordinary generation
mixes several recipes inside each region. The optional overrides are intended
for mapper control and comparing samples in the atlas.

| Arrangement | Structural change |
| --- | --- |
| Vault | One large, asymmetric volume retaining the geological family's shape. |
| Twin | Two offset chambers joined by a neck. |
| Crescent | A chamber cut around a solid rock mass, with an entrance on its inner side. |
| Ring | An annular gallery with a geological side chamber and radial entrance. |
| Clover | Three lobes around a branching junction. |
| Terrace | Successive chambers at different heights with connecting slopes. |
| Rift | A tall, narrow cleft and an off-centre side pocket. |
| Honeycomb | Six small chambers branching from a common vestibule. |
| Spiral | A winding gallery wrapping outward to a terminal chamber. |
| Broken gallery | Offset rooms linked by an angular route with a blind finger. |

Each region contains two to four chambers. Satellite chambers select a different
geological family from the primary chamber unless a mapper forces one family.
Their positions, elevations, scale, aspect ratio, full-angle rotation and shape
parameters vary independently. Occasional oversized primary chambers provide
large openings among smaller pockets. Scenery follows the nearest local chamber,
so water, dead growth, crystals and other environments can meet inside one region.

The previous mandatory east/north grid has been replaced with connected mazes
over groups of sixteen regions. Each group starts as a seeded spanning tree,
then adds sparse loops; independently seeded gateways join neighbouring groups.
That preserves a connected main network while allowing local terminal branches,
missing exits and routes that require a detour. Connectivity here means connected
air volume, not a promise of a flat, dry walking route: drops can require stairs
or bridges. Natural intersections between chambers can add further shortcuts.

Passages independently choose one of eight styles: broad gallery, narrow crawl,
meander, switchback, tall chasm, wide-to-narrow keyhole, split loop, or shaft with
an elevation change. Width, bend direction and bend size also vary. Finite side
branches terminate without a destination chamber; if a branch intersects another
natural void it becomes a shortcut, otherwise it ends in ordinary mineable rock.
The voxel checks verify that closed tips actually occur in the generated terrain.

Only small geometry descriptions are cached (up to 64 regional plans and sixteen
maze plans per sampler). They contain no world tiles or entities. Eviction and
opposite discovery orders produce the same geometry. Tile materialization still
uses the existing nearby, bottom-up chunk queue and preserves previously explored
areas. This changes newly generated worlds; it is not a migration of an active
round's existing caves.

### Diversity regression coverage

- `PassageShapesConnectTheirEndpointsThroughRealVoxels` searches actual carved
  cells from one chamber to the next for each passage style.
- `ShortBandsDoNotBuryShaftConnectionsInTheSeparator` checks the lower bend of a
  shaft with the shortest supported vertical band.
- `RegionalMazeHasBranchesAndConnectsAcrossNegativeMacroBoundaries` traverses the
  regional graph across negative coordinates and checks interior terminal nodes.
- `ClosedSideBranchesEndInRock` checks open branch tips against closed rock ahead.
- `MixedChambersAndTerrainSurviveDifferentDiscoveryOrderAndCacheEviction` checks
  mixed geology within a region and repeatable terrain/scenery after distant
  exploration and cache eviction.

The atlas exports the compiled sampler, compares all eighty geometry signatures
without counting scenery, and draws passage routes directly from regional plans.
Routes are projected across the vertical band; a line is not a claim that its
whole length lies on the selected floor. Dashed routes identify finite branches,
which may intersect other natural voids. The existing streaming illustration
shows queue order rather than measured elapsed time.

The final focused runs passed thirteen geometry cases and 31 gameplay integration
cases using the commands above. Shared, client and server projects compiled.
The export found eighty distinct geometric signatures with scenery excluded.
The atlas contains 198 sampled combinations across two seeds, including wider
192-by-192 views of normal mixed generation. All eighty recipe selections, route
overlays, depth views and narrow/light/dark layouts passed the browser check.

A local standalone sampler run measured a fresh eight-by-eight chunk at 0.72 ms
median and 1.45 ms at the 95th percentile over 100 samples after warm-up. This
includes constructing the sampler and selecting floor scenery, but excludes
world entity creation, physics, networking and client rendering. It is not a
server frame-time or player-capacity guarantee. A multiplayer exploration session
and long-round memory/performance measurements remain necessary.

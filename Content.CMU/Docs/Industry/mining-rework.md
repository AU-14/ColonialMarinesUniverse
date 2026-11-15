# Mining rework: cavern exploration

The agreed direction keeps active exploration, safe mine construction, manual
extraction, and industrial production. Underground generation, construction access
and field surveys are implemented here. The other accepted mining changes below remain planned.

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
variants now change cavern shape and scenery. There are no new cave creatures or
authored underground structures. Construction and survey tools are described below. Existing depth-specific
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
| Braided Galleries | Three winding galleries, cross-links, ochre crusts and shale | Mark junctions and establish a main haul route through the loops. |
| Flooded Fork | A trunk splits toward two broad basins; water and limestone outcrops | Choose dry ledges or accept slower wading; bridge gaps before moving machinery. |
| Pillar Cathedral | A broad chamber, several Z-levels of open height, rock columns and calcite fans | Descend from the rim, build bridges and survey exposed walls. |
| Lava Tubes | Winding conduits, widened pockets, side spurs and exposed lava | Keep equipment and people off the lava; establish a protected crossing. |
| Leaking Cavern | Offset drainage galleries, narrow fingers, small pools and outcrops | Follow branching shelves and plan passages around wet ground. |
| Biological Dead Zone | Lobed chambers and crooked links; dead roots and fungal caps | Explore connected pockets; dead growth is scenery, not a harvestable crop. |
| Thermal Springs | Overlapping basins with rock peninsulas and mineral water | Place staging areas on dry rock; wading slows movement. |
| Vent Labyrinth | Angular fault corridors, switchbacks and a tall chimney; dormant vents | Mark the route through repeated intersections and secure vertical access. |

Only natural floors receive liquid/scenery; open shafts never receive floating
props. Water slows movement and uses the existing water interactions. Lava uses
the existing ignition behavior. Mineral springs currently behave as tinted
water. Vents, roots, fungi and mineral crusts are non-blocking scenery; ore is supplied
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

Each region contains two to four chambers. With the default balanced recipe,
satellite chambers select a different geological family from the primary chamber
unless a mapper forces one family. Custom biome palettes can repeat a family.
Their positions, elevations, scale, aspect ratio, full-angle rotation and shape
parameters vary independently. Occasional oversized primary chambers provide
large openings among smaller pockets. Scenery follows the nearest local chamber,
so water, dead growth, mineral crusts and other environments can meet inside one region.

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

## Cavern tools and field surveys

### Before an Insurgency round

Use the admin console in the lobby. The overrides affect Insurgency, reset with
the round, and cannot change once terrain has been generated. They never reroll
an active cave or erase player construction. Caverns still begin at **Z-3**, below
the two authored underground levels.

```text
cmu_caverns status
cmu_caverns seed 12345
cmu_caverns preset volcanic
cmu_caverns biome ThermalSprings 2
cmu_caverns biome LavaTubes 0
cmu_caverns preview -5
cmu_caverns size 120
cmu_caverns height 7
cmu_caverns variant mixed
cmu_caverns layout mixed
cmu_caverns reset
```

`seed random` selects a fresh seed when each world first generates. Status also
prints the resolved seeds of generated worlds and the pending biome weights.
`preset balanced` keeps all eight biomes; `dry`, `flooded`, `volcanic`, and
`biological` select themed mixtures. The biological preset uses the existing
dead-root/fungal geology; it does not add living vegetation or creatures.
`biome <name> <weight>` adjusts a biome's relative frequency, with zero disabling
it. Missing entries in a preset are disabled. For example, a weight of 4 has four
times the selection probability of a weight of 1; these are chamber probabilities,
not exact percentages of floor area. Main and satellite chambers both respect
the palette. At least one biome must stay enabled.

The numeric seed determines placement, chamber geometry and connections. The
palette determines which biomes occur and how common they are. Reproducing a world
requires the same seed **and** size, height, palette and layout settings. Weighted
satellite chambers can repeat their region's main biome; layouts and dimensions
still vary independently. `variant` remains a single-biome debugging override;
choosing a preset or editing a biome clears that override.

With a numeric seed selected, `preview [depth]` prints primary biomes for 25
regions around the origin, plus counts including their satellite chambers.
It samples the same generation plans without creating any world tiles, so
previewing does not lock the settings. It describes the cavern band, not a
walkable slice or a guarantee that all chambers are accessible from one entrance.

Region size accepts 48–160 tiles;
band height accepts 4–9 levels and includes its solid bottom separator. Keep
geology and layout mixed for the full variety. The command's help lists all
eight geology names and ten layouts for deliberate overrides.

### Building routes

The construction menu's **Z-Level (Experimental)** page includes up stairs,
down stairs, deployable ladders, and survey maps. Each stair uses the existing
50 steel + 50 plasteel graph. A portable ladder costs 10 steel + 5 plasteel.

Up stairs prepare the upper cave chunk, excavate a small landing, and leave a
shaft over the stair. Down stairs prepare the lower landing and provide a return
stair. Preparation includes landing tiles across chunk edges and preserves
player-built structures. Support beams retain their existing collapse behavior.
Ladders need solid ground below, an open shaft above, and an adjacent supporting
wall or beam. They do not drill through ceilings. Generated cave maps have
inherent planetary gravity, so machinery does not need a gravity generator.

New floor scenery uses several combinations per geological family: shale,
limestone knobs, calcite fans, sediment, volcanic clinker, ochre and iron crusts,
dead roots and collapsed fungi. Crystal formations are no longer selected by
the generator. Decorative props remain non-blocking, with at most one per 4×4
geological cell; pools retain their existing movement and heat effects.

### Explore, write, share

1. A living player underground automatically records a local 4×4 footprint.
   Rock and closed doors hide cells behind them. Only already materialized terrain
   is recorded; surveying never generates new chunks. Stationary explorers refresh
   their immediate surroundings every two seconds to notice excavation or building.
2. Make a **cave survey map** from one sheet of paper. Use its **Transcribe survey**
   verb to copy your observations into it. Rewriting adds newer observations while
   preserving other areas already on the paper.
3. Use the map in hand, or its **Read survey** verb. Its knowledge joins your own
   and opens the 3D survey. The tactical-map action also opens remembered cave
   knowledge; **Colony map** returns to the ordinary tactical view. Characters
   without a tactical-map action receive a survey action.
4. Page north/south/east/west through 64×64 sections, or deeper/shallower through
   groups of eight floors. Orbit, zoom, isolate floors, and center on your location
   using the reconstruction controls. **Refresh** loads the latest recordings.

Newest means the time a person actually observed each cell. Writing, copying,
or reading a map does not make its old information newer. Observed empty shafts
can replace old walls. Disjoint areas combine, overlapping cells retain their
latest observation, and different worlds and depths never overwrite each other.
A paper map stays frozen until someone transcribes into it again; past readers
do not receive remote updates. Personal knowledge belongs to the character.
Maps and knowledge last only the current round.

The automatic colony reconstruction excludes generated cave levels. There is
no unexplored cave atlas behind the personal survey. Surveys include coarse
terrain and traversal structures, not live people, inventories or ore contents.

### Resource limits

Recording processes at most eight players per server tick, from a queue refreshed
at most four times per second. Each observation reads sixteen cells and performs
short visibility checks within that footprint. Stored 4×4 blocks are immutable;
physical maps share those blocks until later observations replace them. Movement
does not broadcast survey geometry.

Each character or map can retain four worlds and up to 8,192 blocks per world
(up to 131,072 known cells, fewer when blocks are only partly observed). At the
limit, existing blocks can still be updated; new blocks are refused with a
visible warning. Copy requests are throttled. The viewer requests one bounded
section and sends at most eight 16×16 chunks per server tick across viewers,
with at most four per viewer per pass. These are workload and memory limits,
not a measured multiplayer capacity guarantee.

A local data-model benchmark with 2,145 populated blocks and 10,000 warmed-up
4×4 observations measured 3.20 microseconds median and 4.10 microseconds at the
95th percentile, allocating 1,296 bytes per observation. Copying the block index
for a frozen map averaged 0.025 ms; enumerating a 64×64×8 section with 4,096 known
cells averaged 0.227 ms. These measurements exclude terrain lookup, visibility,
ECS events, serialization, rendering, and long-round garbage collection effects.

### Verification (2 November 2026)

The repository test wrapper built Content.Shared, Content.Server and Content.Client
successfully. The geometry/survey-data run passed all 16 tests. The combined
gameplay run passed seven tests and hit a test-pool cleanup assertion in the
remaining gravity/falling test. That test passed when rerun alone, giving eight
passing gameplay checks across the runs. The pool cleanup issue remains separate
from the implemented feature; no engine or fixture workaround was added.

Commands executed from the industry-import checkout:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.Tests -Filter 'FullyQualifiedName~CMUCaveSurveyDataTest|FullyQualifiedName~CMUCavernFieldTest'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUCaveToolsTest|FullyQualifiedName~CMUCavernGenerationTest|FullyQualifiedName~GeneratedCavesDoNotExpandOrDisableTheAutomaticColonyAtlas'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~OpeningHasGeneratedGroundBelowAndRevisitingRetainsChanges'
```

Coverage includes frozen map copies, observation ordering, negative-coordinate
clipping, wall occlusion, distinct worlds, access restrictions, networked survey
geometry, owner-position tracking, client/shared sandbox compatibility, colony
atlas exclusion, gravity and falling, gradual exploration, scenery placement,
and cave stairs/ladders across a chunk boundary. Native sprite states and license
metadata were checked; `git diff --check` passed. The new pre-round console controls
and visual appearance have not been exercised in the user's live game. No
multiplayer load-capacity claim is made from the small data-model benchmark.

The later biome-seed update passed all 14 `CMUCavernFieldTest` cases and three
focused integration checks, with no failures or skips. The same test wrapper
rebuilt the shared, server and client projects successfully:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.Tests -Filter 'FullyQualifiedName~CMUCavernFieldTest'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUCavernBiomeConfigTest|FullyQualifiedName~FloodedGeologyPlacesWaterOnGroundButNotInTheShaftAbove|FullyQualifiedName~PersonalSurveyStreamsOnlyObservedCellsAndTracksItsOwner'
```

| Requirement | Evidence |
| --- | --- |
| Weights affect all chamber biomes; disabled entries stay absent; discovery order and cache eviction preserve the seed result | `BiomePaletteControlsEveryChamberAndIsStableAcrossDiscoveryOrder` |
| Lobby previews leave settings editable; world creation freezes the chosen seed and palette | `PreviewLeavesPaletteEditableButGeneratedWorldLocksItsRecipe` |
| Weighted geology produces liquid on the actual cave floor, not in the shaft above | `FloodedGeologyPlacesWaterOnGroundButNotInTheShaftAbove` |
| Client/server surveying and client/shared sandbox loading remain valid | `PersonalSurveyStreamsOnlyObservedCellsAndTracksItsOwner` |

The lobby controls have now been exercised in an automated real-lobby fixture.
The user's live game was not restarted or repositioned during this update.

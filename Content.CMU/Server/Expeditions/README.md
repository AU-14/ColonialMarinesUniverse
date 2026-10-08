# Govfor expedition terrain

This is CMU's own finite map generator. It does not call `BiomeSystem`, dungeon generation, or
salvage generation. Existing engine map APIs materialize its output, and existing licensed art
supplies the tile and object palettes.

## Try a map

For named locations with recovery briefings, readable evidence and histories, see [SUNDIAL recovery
sectors](STORIES.md). Use `cmu-expedition scenarios` to list them and
`cmu-expedition scenario CMUBlackwaterReach 42` to generate one. Terrain-only generation below
remains available for testing arbitrary combinations.

In a development server's admin console (Mapping permission):

```text
cmu-expedition generate Woodland 42 RiverValley CrashRecovery
cmu-expedition status <map ID printed by generate>
cmu-expedition open <map ID>
cmu-expedition-visit <map ID>
```

`generate` and `scenario` build a map in batches, then automatically open its Govfor dropship
beacon and announce the recovery operation. `status` reports readiness; `open` remains an
idempotent manual option for maps created through the API. No destination exposes an unfinished
map. Remove unused maps after evacuating them; their upper levels are removed with the surface.

Each expedition has gravity, breathable oxygen/nitrogen at 20 °C, and two linked upper levels.
The LZ remains open sky; cliff masses continue onto the first upper level with solid summit
tiles on the second. All levels share a seeded day/night phase. Set a particular hour with
`cmu-expedition time <map> hour <0..23.99>`; the normal 45-minute light cycle then continues.
This changes lighting, not the biome's temperature or weather.

A seated, grounded fighter pilot can right-click their seat and choose **Launch to <sector>**.
Native takeoff clearance is required. The fighter receives the expedition's chart and airspace;
its original physical launch site stays the return destination.

`cmu-expedition-visit` requires Admin permission and moves you to the ready map's LZ. From the
server console, append a connected username. If that player has no body, it creates an admin
observer and enters the gameplay view, including on `game.dummyticker=true` preview servers.

Available settings:

| Setting | Choices |
| --- | --- |
| Biome | `Woodland`, `Swamp`, `Tundra`, `Beach`, `Mountain`, `SwampJungle`, `BurnedWoodland` |
| Landform | `RiverValley`, `LakeCountry`, `Ridgeline`, `Wetlands`, `Coast`, `Archipelago`, `Caldera`, `Fjord`, `Delta`, `Highlands` |
| Story layout | `CrashRecovery`, `SurveyCamp`, `BrokenConvoy`, `LostRelay` |
| Seed | Any signed 32-bit integer |

Omitting the landform uses `Coast` for beach, `Highlands` for mountain and `Wetlands` for swamp
jungle; other biomes choose one from the seed. Omitting the story uses `CrashRecovery`.
These are 280 biome/landform/story combinations before seed variation. Story choices alter the
central recovery site; they are not implemented mission scripts yet. Secondary sites are
natural landmarks: groves, deadfall, rocky outcrops and hollows, not a ring of settlements.

## Generation contract

- Default dimensions are 140×140 tiles. Profiles may choose 128–196 tiles per side.
- A 27×27 clear LZ provides room for the current 11×21 tactical dropship footprints in either orientation.
  Validate each intended aircraft's real footprint, orientation, ramp and landing offset in game;
  this is not an assertion that every carrier or large craft fits.
- Generator v7 uses gradient noise at several scales and bends the terrain sampling coordinates.
  Rotated drainage systems, tributaries, overlapping lake basins, irregular islands, broken crater
  rims, inlets, branching deltas and rock spines provide different large shapes.
  Beaches follow water margins. Mountain highlands have solid cliff masses and passable valleys;
  swamp jungle uses denser, larger trees; burned woodland uses sparse stumps and scorched soil.
- The generator selects the LZ by checking candidate footprints against terrain. Its clear 27×27
  footprint retains natural ground inside a ragged clearing instead of painting a dirt square.
  Sites must share a reachable region before routes are constructed. Up to twelve deterministic
  terrain attempts are allowed; an infeasible seed is rejected before map allocation.
- Five to nine sites use terrain-based placement, separation, varied dimensions and orientations.
  Ship structures stay at the recovery site; small abandoned camps and cargo spills also appear in the wilderness.
  Cargo wrecks use the original MULE-17 equipment lander layout: a broad cargo keel, separate two-seat
  cockpit, outboard engines, personnel exits and an aft loading ramp. It assembles existing licensed
  Fallujah structural artwork into a new airframe. The three gunship derivatives remain available.
  MULE damage shears an entire nacelle, breaches one cargo wall and scorches contiguous deck sections;
  approach trails use its openings instead of deleting scattered hull tiles. Equipment pallets, power
  cabinets and damaged flight instruments dress the interior; heavy parts trail behind the ramp.
  Long skids, broad burnouts, breakup, shore impacts and cliff strikes affect the surroundings. Mountain sites prefer rock faces; coastal sites
  prefer a nearby bank. Original water, bridges and cliff tiles remain intact.
- Forest-floor dressing is independent of solid tree and boulder cover. Grasses, ferns, shrubs,
  shoreline plants, pebbles, branches and deadwood form patches between trees. Paths and the LZ use
  low, walk-through detail. Cliffs alone use rock walls; loose cover uses individual gray boulders.
  Palm, conifer, snow-tree, swamp and jungle palettes provide different environments. Woodland has
  thirteen tree variants, including broadleaf, large-canopy, conifer and dead trees. Flower patches
  cluster on dry vegetated ground, while driftwood appears along beaches. Expanded grass, shrub,
  fern, pebble and stump palettes vary the smaller details without adding more solid obstacles.
- Leaf litter, moss, fungi, dry grass and ash form local patches with less repeated ground cover.
  Seven to ten physical wilderness features are attempted: windthrow, rockfall, abandoned camps, cargo spills,
  bog remains and burn scars. Wildfire pockets use native RMC tile fire and begin only when the LZ opens.
  Initial pockets avoid the landing area and access routes; normal extinguishing, expiry and fire spread apply.
- Routes follow a variable branching graph with optional shortcuts. Water and cliffs block ordinary
  route search. Separate bridge links require two dry 3×3 banks, a straight three-tile-wide crossing
  and a maximum bank-to-bank span of twelve tiles. Wide water forces a detour. Neither clearings
  nor site floors fill water or cut cliffs. Bridge decking uses weathered timber.
  The recovery site has at least two graph
  connections, although physical routes may converge at chokepoints. Paths reserve three tiles,
  with narrow, intermittent worn ground along the center. Vegetation reaches the path edges.
- Invisible collision bounds the map without surrounding every biome with a rectangular stone wall.
  Exposed water uses the matching RMC desert-water set: full shallow/deep water, straight edges,
  outer corners and inner corners. Eight neighbouring terrain cells determine the shape and rotation.
  Narrow channels with opposing banks remain shallow. Native animation, depth, speed modifiers,
  and RMC submersion/wake behavior are retained. Banks use original terrain, so bridges do not
  create false shoreline stripes across the river.
  Bridges have no water entity underneath, so dry crossings do not slow or submerge players.
  Tundra channels are liquid water bordered by snow; freezing and cold exposure are not implemented.
- The same seed, profile, choices and generator version reproduce the layout. Random tile and
  entity variant selection also uses that seed. Existing game systems may still randomize their
  own cosmetic details; this is not a complete deterministic simulation replay.
- Only one generation job runs at a time, with at most three expedition maps present. The server
  sets up to 1,024 tiles or spawns up to 48 objects per update. Layout planning and final map
  initialization still have one-time costs and need profiling on a populated server.
- Failed builds remove their new map. Deleting a map during generation frees the build slot on
  the next update. Existing maps are never overwritten. Maps and their runtime metadata are
  round-local; save/load of in-progress expeditions is not supported.

## Integration boundary

`CMUExpeditionGenerator.Generate` creates a pure layout. `CMUExpeditionSystem.TryGenerate` validates
the profile and starts materialization. `CMUExpeditionReadyEvent` tells future mission-selection
code that it may call `OpenLandingZone`. `CMUExpeditionMapComponent.Plan` retains the seed,
biome, landform, story, terrain attempt, recovery center, site kinds, route graph, bridge spans,
original terrain, water depth, solid cover and walk-through detail placement.
Treat the retained plan as read-only. The LZ cannot be designated as the colony's primary LZ.

The experimental miner is an inert recovery-site prop. Mission completion, lifting/loading it,
colony mining and the planet-selection console remain later phases.
AI cover and route decisions must account for the *current* world, including destroyed objects,
instead of treating this initial generation plan as an always-correct navigation map.

`cmu-expedition-ai <map ID> [count: 1-6]` (Admin) adds a new squad near the objective and prints its squad ID.
Armed scavengers detect visible GOVFOR enemies and use a real loaded MAR-40 with firearm training.
They shoulder the rifle, lead using its projectile speed, aim for 0.3 seconds and fire at most three
rounds. Every trigger attempt checks the full scatter corridor and nearby allies. Decisions run every
0.15 seconds. Paired shelter and peek positions produce physical step-out attacks and withdrawals;
cover recovery lasts 0.8 seconds unless incoming fire or treatment requires longer. Squad attack slots
stagger peeks and position reservations reduce crowding. Cover searches inspect at most 256 nearby
cells and reject grazing angles, water, cliffs, fire and recently failed destinations.

Visible hostile fire passing near a guard or a fresh hit causes a suppression response. Wounded guards
seek shelter and use their finite three-dose dressing pack through interruptible native medical actions.
Briefly lost enemies are watched for 1.5 seconds; last-seen memory expires after six seconds. Incapacitation,
player control and disabling NPCs stop movement, fire and treatment. Reloading, casualty rescue, grenades
and coordinated flanking use bounded plans and native actions. See [AI design and research](AI-DESIGN.md) for sources,
behavior rules and limitations. Use `cmu-expedition-ai-status <map ID>` to inspect state, ammunition,
health, suppression, selected shelter/peek positions, the last firing check and cover-search cost.

## Verification and preview

```text
dotnet test Content.Tests/Content.Tests.csproj --filter FullyQualifiedName~CMUExpeditionGeneratorTest
dotnet test Content.Tests/Content.Tests.csproj --filter FullyQualifiedName~CMUExpeditionShorelineTest
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --filter FullyQualifiedName~CMUExpeditionMapTest
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --filter FullyQualifiedName~CMUExpeditionAgentTest
dotnet run --project Content.CMU/Tools/ExpeditionPreview -- expedition-preview.svg 42
dotnet run --project Content.CMU/Tools/ExpeditionPreview -- river-seeds.svg 42 RiverValley
```

The preview uses the actual generator source and exports twelve schematics with different seeds.
The default gallery covers all ten landforms and seven biomes. Supplying a landform as the third
argument holds woodland and crash recovery constant, showing twelve seeds of that one terrain
type. This exposes repeated silhouettes and layouts instead of hiding them behind palette changes.
These are schematics, not screenshots of in-game art. Generator v7 changes crash layouts from older versions.

| Requirement | Automated evidence |
| --- | --- |
| Clear LZ, bounded map, distant objective, reachable sites | `LandingAndAllSitesRemainReachable`: 3,360 layouts across all 280 combinations, including minimum/default/maximum sizes and signed seed extremes; independent dry-path flood fill |
| No dirt roads across water, unsupported bridges, cliff cuts or floating buildings | The same matrix independently checks original terrain conservation, every bridge's banks/span/deck, and colliders |
| Replay and seed variety | `SeedReplaysExactlyAndOtherSeedsChangeGeography` |
| Actual biome/landform/story differences | `BiomesLandformsAndStoriesChangePhysicalLayout` |
| Varied site placement and branching networks | `SeedsVarySiteCountsNetworkTopologyAndLandingPositions` compares degree distributions independently of site numbering |
| Shapes change within one terrain type | `WaterShapesDifferBeyondRotationOrMirroring` compares water silhouettes under all eight square symmetries |
| Bad input rejected | `InvalidSizesAreRejectedBeforeAllocation`, `UndefinedVariantsAreRejected` |
| Materialization and explicit LZ publication | `ProfilesBuildBeforeTheirGovforLandingZoneOpens` |
| Failure/cancellation recovery | `InvalidProfilesAndDeletedBuildsDoNotLeaveTheGeneratorBusy` |
| Dense backwoods, natural landmarks, human props concentrated at the objective, low path vegetation | `BackwoodsStayVegetatedWithoutBecomingASettlement` |
| Real RMC water on every exposed wet cell, shallow/deep behavior, nonblocking ground detail | `ProfilesBuildBeforeTheirGovforLandingZoneOpens` inspects spawned components |
| Edges and both corner shapes face their banks; rotations, channels and bridges remain coherent | `BanksSelectTheMatchingRmcEdgeAndBothCornerShapes`, `EveryNeighbourPatternRotatesConsistently`, `NarrowChannelsBridgesAndMapEdgesDoNotGetFalseBanks` |
| Generated river uses the new shapes, with flower patches off paths and water | `GeneratedRiverUsesCornersAndScatteredFlowerPatches` |
| MULE carrier and three gunship variants, wide scorch footprints, actual nearby cliffs, physical wilderness remains and safe initial fire placement | `CrashFamiliesAndWildfireScarsStayPhysicalDryAndAccessible` |
| Recognizable MULE hull, continuous cargo aisle, equipment pallets, usable exits, terrain preservation and extraction access across seeds/orientations | `MuleKeepsACargoAisleAndRecognizableFuselage` |
| Original hull pieces and orientation, no premature fire, no duplicate fire on reopening | `ProfilesBuildBeforeTheirGovforLandingZoneOpens` |
| Human AI sight, faction filtering, physical pursuit, finite ammunition, last-seen expiry, injury retreat into reachable cover and incapacitation shutdown | `InfantryUsesSightRealAmmunitionAndMovementThenStopsWhenIncapacitated` |
| Rifle handling and holding fire for teammates | `InfantryReadiesRifleAndHoldsFireForTeammates` |
| Physical short-burst peeks, near-miss suppression and return to shelter | `InfantryPeeksFiresShortBurstsAndPhysicallyReturnsToShelter` |
| Nearby muzzle clearance around walls and actual projectile hits | `InfantryStepsClearOfGrazingWallAndShootsWithoutRemovingIt` |
| Sheltered medical actions, damage interruption and exhausted supplies | `WoundedInfantryTreatsInShelterInterruptsOnDamageAndExhaustsDressings` |
| Staggered squad exposure, continued attacks by both soldiers and failure memory | `SquadStaggersPeeksAndBothGuardsKeepAttacking` |
| Automatic LZ publication, physical guard orders/construction and fighter departure | `AutomaticLandingZoneAndGuardOrderBuildPhysicalCover` |
| Maximum two opening grenades and a shared cooldown | `SquadLimitsOpeningGrenadesAndDoesNotChainThrows` |
| Multi-z links, open landing airspace and synchronized day/night phase | `ProfilesBuildBeforeTheirGovforLandingZoneOpens` |

Generator v7 was checked with 83 generator cases, including all 840 crash-recovery layouts,
and three map integration cases before the upper-level addition. The complete 3,360-layout
matrix is available above; the later broad rerun was stopped after 134 passing cases and
must not be treated as a completed pass.

```text
dotnet test Content.Tests/Content.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~CMUExpeditionGeneratorTest&(FullyQualifiedName!~LandingAndAllSitesRemainReachable|Name~CrashRecovery)'
dotnet test Content.Tests/Content.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~CMUTacticalPlannerTest
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~CMUExpedition
```

The preview tool can export a complete machine-readable plan:
`dotnet run --project Content.CMU/Tools/ExpeditionPreview -- --plan mountain.json Mountain 42 Highlands CrashRecovery`.
Mountain / Highlands / CrashRecovery, seed 42, previews the MULE with a detached engine and a
nose strike against the rock face. The wreck is a set piece, not a flyable aircraft.

The current engine fixtures exercise real rifle fire, medical supplies, magazines, grenade
throws, casualty pulling, squad radio, guard construction, fighter departure and moving
connected player bodies in trees, the MULE, mountain corners and swamp banks. On 2026-10-08,
the latest results for all 24 expedition integration cases passed across the focused runs,
along with four planner unit tests. The final affected cover/terrain rerun passed all five
cases without skips. Six-guard cover searches measured 3.65/3.98 ms median and 5.91/6.91 ms
95th percentile for one/two squads in the local debug simulation, not a production guarantee.

Before connecting a player-facing console, fly an actual Govfor dropship into each biome and
back, walk both approaches while dragging equipment, check tree visibility and collisions,
and measure generation tick time with players online. Watch repeated peeks into held angles,
squad crowding and long movement interruptions. The six-guard fixture reports candidate-search
costs. Human multiplayer playtesting is still required for pacing, combat balance, aircraft
footprints and visual quality.

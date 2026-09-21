# UD6 Mohawk import and audit

Source: [CM-SS13 PR 12866](https://github.com/cmss13-devs/cmss13/pull/12866),
`PoltavskaPraca/cmss13-praca`, commit `0d711eddc539ea7ac67cf6b5869a08fa33dc9391`.
The audited PR was a draft with 99 changed files and 63 commits. The import is
pinned to this revision, not to the changing PR branch.

## What the source contains

The Mohawk is a dropship, not a replacement for the Almayer. There are two
authored 17 by 24 maps, Omaha and Midway. Each DMM contains one authored cabin
level. Deployer objects generate the lower exterior, servicing points, landing
gear and boarding ramp. The artwork additionally supplies a tile-addressed top
view. CMU represents the underside, cabin and upper hull with three linked grids.
The lower and upper levels are exterior geometry, not two additional passenger
cabins.

| Feature | Omaha | Midway |
| --- | --- | --- |
| Authored DMM cells | 408 | 408 |
| Seats | 64 (52 passenger, 10 pilot-type, 2 command) | 19 (18 passenger, 1 gunner) |
| External weapon mounts | 4 | 4 |
| Electronic mounts | 2 | 2 |
| Fuel/engine servicing points | 2 | 2 |
| Crew weapon mounts | 0 | 3 |
| Fixed chin weapon | None | Twin M90 |
| Cameras | 5 | 5 |
| Intercoms | 6 | 5 |
| Lights | 9 | 7 |
| Wall medical cabinets | 3 | 1 |
| Weapon vendor | 1 | 0 |
| Cockpit hatches | 3 | 1 |
| Side hatches | 2 | 2 |
| Deployable cockpit ladder | 1 | 1 |
| Landing gear assemblies | 4 | 4 |
| Lower clearance/crush markers | 16 | 16 |
| Rear ramp | 3 tiles wide, 5 stages | 3 tiles wide, 5 stages |

Both layouts include navigation, equipment/fire control, camera controls,
overwatch and a tactical map table. Omaha's source also has a CPR dummy and weapons
rack; the CMU layout omits the training dummy and provides two internal utility
mounts in the rear passenger-bay corners for medevac and other utility modules.
Midway has a separate gunnery station. Source pixel offsets are meaningful: many
passenger seats are placed twice per tile. The importer retains those offsets.
Seat and passenger artwork use the source offsets; the physical buckle point
stays on the seat tile so the south-facing rows cannot buckle people into walls.

The PR also changes CM-SS13's radio/camera networks, dropship names, attachment
registrations, shuttle movement hooks, transit borders, damage and door controls,
and adds experimental Almayer/LV624/runtime landing pads. CMU uses its own radio,
camera, CAS, shuttle and Z-level systems; the BYOND map-management code cannot be
copied into the C# game.

## CMU implementation

- `MultiDeckDropshipComponent` declares secondary grid files by relative depth.
  Only the cabin owns `Dropship` and `Shuttle`; followers point back to that
  controller through `DropshipDeckComponent`.
- `MultiDeckDropshipSystem` loads the declared decks, attaches maps to the
  existing Z network, synchronizes world position/rotation, transfers the decks
  during FTL, and deletes followers with their owning ship. Existing terrain maps
  are reused; missing levels are empty maps rather than duplicated colonies.
  During flight, all three maps share the cabin's moving-space parallax. Landing
  preserves the destination maps' own backgrounds.
- Ground landing markers are resolved one level below the cabin. Departure
  searches use that ground coordinate as well. Landing checks cover the cabin
  and underside, including the reserved envelopes of inbound ships. Roof artwork
  above the cabin does not reject a pad. Adjacent envelopes may touch at an edge
  but cannot occupy the same area. Tactical landing uses the same clearance rule.
- Midway replaces the dynamic gunship for USCM and HazOps. Their other transports
  remain Alamo and Osprey respectively. Omaha and both Navy paint variants remain
  available for mapping and admin spawning. Round setup selects a home pad only
  when the cabin and underside fit; if no pad
  fits, it tries another compatible design without consuming a dropship slot.
  The existing carrier and colony maps are not resized by this port.
- Deck grids disable structural splitting: underside mounts and landing gear
  are separated by empty space but must remain attached to one moving ship.
  Fixed artwork and clearance markers disable grid traversal, so an origin over
  an empty cell cannot detach the underside on initialization or departure.
  The lower servicing grid is marked as an exterior deck: loose ground occupants
  remain at the departure site rather than riding its invisible anchoring tiles.
  Z movement accepts occupants directly parented to these linked grids, and
  rotated ramps use their grid-local height profile.
- Equipment lives on the lower servicing deck and is registered with the cabin
  controller. This replaces BYOND's proxy objects with normal CMU power-loader
  interactions and avoids separate equipment inventories for each deck.
  Weapon insertion and map container fills refresh the mount's visible weapon
  and ammunition layers, including Midway's preinstalled M90.
  Mohawk mounts substitute complete cannon and launcher artwork for the normal
  hull-edge attachment frames, including when ammunition changes. Engine upgrades
  use the source's compact Mohawk fuel/cooling faces and half-tile mounting
  offsets to sit on the black servicing plates. Empty engine points stay hidden
  and retain a clickable plate-sized area. Other dropship mounts keep their
  existing attachment artwork.
- A hijacked Mohawk loses its upper and lower grids and crashes its cabin onto
  the impact marker's actual level. Loose people underneath stay at departure;
  ramp occupants are brought inside before the lower deck is removed. The wreck's
  ramp and cockpit boarding hatch remain disabled.
- `MohawkSystem` implements timed hatch/ladder and ramp deployment, forced
  retraction before FTL, ramp support using the existing Z physics, and landing
  gear clearance damage. Lowering the ramp applies 40 blunt damage, a five-second
  knockdown and a throw to people underneath each moving segment. Raising carries
  occupants into the cabin, including occupants still parented to an overlapping
  landing pad. Departure interrupts deployment and retracts both boarding devices.
  Console door commands also reach the ramp.
  Queens can sabotage the ramp and side controls; engineers and pilots can
  restore their shared control connection with a multitool.
- Landing gear uses the source's 64-by-64 collision footprint, with matching
  lower-grid tiles and the correct front/rear facing. The 96-by-96 gear artwork
  retains its bottom-left anchor without displacing the collider.
- Dense hull facades have invisible anchoring tiles, including source pieces
  placed over empty turfs. Their collision bodies stay fixed to the ship instead
  of drifting into the cabin when it rotates.
- Medical wall dispensers use the existing NanoMed cabinet and its inventory,
  with the source wall offsets. The tactical table has a solid one-by-two-tile
  collision footprint. Cameras, lights, intercoms and consoles anchor to the
  cabin. Camera facing and all mounting offsets are preserved in replicated
  prototypes. Lights use the original 32-pixel fixtures without the station
  wall-offset system overriding their placement. Intercoms use onboard power
  and start with their Marine Common speakers enabled.
- Object names describe their function: bulkheads, outer hull plating, landing
  gear, hatches, walkway panels, cabinets and consoles no longer inherit a ship
  name or a generic placeholder.
- `MohawkSeatSystem` presents the source passenger offsets on connected clients
  and restores their original offsets when unbuckling. Seats remain anchored,
  follow ship rotation, and use the imported buckled sprite where one exists.
- Existing CMU components provide seats, access, cameras, intercoms, vending,
  overwatch, tactical maps, CAS and hijacking. The Midway M90 uses CMU's CAS weapon
  backend, with a fixed weapon mount and its own 400-round PGU-420 ammunition.
  Source firing values are 40 rounds per burst, 145 damage, 30 penetration,
  2-second travel and a 1-second weapon delay. The M90 requires the dedicated
  gunnery console and gunner seat, and cannot be fired through fire missions.
  Gunnery uses CMU's existing CAS targeting and camera UI.
- All Omaha, Midway and Navy DMI states are imported, including animations and
  source dimensions. Navy paint versions of both existing layouts are also
  available as `omaha_navy.yml` and `midway_navy.yml`, with corresponding
  deployment maps for mapping and admin use. Navy has no separate authored DMM. Its
  available artwork replaces matching states without changing the layout or
  equipment. Missing Navy assets retain their originals, including the upper
  hull, certain consoles/modules, and passenger seats with restraint artwork.
  `map_audit.json` lists every fallback. The source provides only one tiled
  upper-hull sheet.
- Positional sounds are downmixed to mono while preserving their sample rate
  and duration. Hatches retain standard dropship airlock behavior: xeno prying,
  welding, maintenance wires, occlusion and airtight state changes. Imported
  hatch art uses the normal door animations, with RMC weld and panel overlays.
- Mohawk takeoff and landing use the original shuttle engine sounds for both
  layouts and paints, including the landing-pad arrival cue.
- Empty engine servicing mounts have transparent base layers and explicit click
  bounds; installed fuel and cooling upgrades remain visible. Weapon points
  use complete underside artwork instead of cropped hull-edge mounting frames.
  The Midway M90 retains its source sprite origin and horizontal placement offset.
- Deployed ramp stairs exclude vehicles from climbing support on both server
  and client. Pedestrians retain the standard multi-Z stair profile.
- Every cabin includes a named warp point (`Omaha cabin`, `Midway cabin`, and
  the corresponding Navy names). The point stays attached through flight.
- Side hatch direction and control groups follow their side of the cabin. This
  corrects the draft maps' missing starboard facing and Omaha's duplicate port
  door ID. Directional buttons retain the source's map-relative pixel offsets,
  draw above their wall mounts, and anchor to the deck, including an explicit
  supporting tile for the external ramp control. The belly artwork keeps its
  source transparency and draws below the deployed ramp instead of covering it.
- Lowered ramp artwork is assigned by entity prototype, so it reaches connected
  clients. The nine distinct `ramp-4-low` through `ramp-12-low` states form the
  entire walkable surface. There is no invisible ramp row past the bottom edge.
  Cabin ramp edging stays attached when the opening's floor tiles are removed,
  and travels with the ship through takeoff and landing.
  Tiles 4–9 remain at ground level; tiles 10–12 use the existing multi-Z stair
  component and its standard height profile. The three cabin-end pieces use
  `3,16` bulkheads with full-tile collision and wall support while deployed.
  The raised `ramp-1` through `ramp-3` sprites stay on the cabin floor and are
  not drawn below the lowered edge, where they would duplicate its lip.
  The entire lowered assembly is one tile aft of the raised floor. Its collision
  pickup area ends at the visible edge. Raised floor tiles
  retain their original cabin positions and are restored on retraction.
- Cabin grids carry saved breathable atmosphere, including their round-setup
  wrappers, so loading over a vacuum map does not create an empty cabin gas mix.

`map_audit.json` accounts for the input cells and conversion dispositions.
The converter fails on unknown functional atoms rather than silently discarding
them. `source_manifest.json` records the source revision and asset hashes.

## Source limitations

The pinned draft explicitly leaves landing-gear xeno crawling, further carrier
and ground-map work, ceiling-light art and revised landing-shadow art unfinished.
Those are not completed upstream features. The source also uses a simplified
generated exterior rather than moving three authored DMMs.

## Rebuild the resources

Use Python with Pillow, PyYAML and SoundFile. Supply the pinned source files in a separate
directory, preserving their repository paths:

```text
python Tools/mohawk/import_mohawk.py --source <source-directory> --fetch --art --maps
python Tools/mohawk/validate_mohawk.py
```

Generated assets belong under `Content.CMU/Resources`. Do not edit generated
entity/tile prototypes or map files by hand; change the importer and regenerate.
The handwritten mechanics and equipment prototypes are in `mohawk.yml`.
The `*_deployment.yml` files are map wrappers for round setup; the cabin
`omaha.yml` and `midway.yml` files are grids for the map loader. Each cabin loads
its lower and upper grid from `MultiDeckDropship.deckPaths`.

## Validation

Static validation covers map format, tile payload dimensions and IDs, unique
entity/component IDs, prototype parents, sprite paths/states and RSI frame
geometry. `MohawkDropshipTest` exercises loading both variants, linked Z maps,
equipment ownership, movement/rotation, timed deployment, ascending and descending
the ramp, shot openings, launch retraction, actual FTL arrival, landing clearance,
inbound reservations, missing decks and cleanup. Boarding is tested at both zero
and 90 degrees on both variants, in small steps from an overlapping landing
surface and back down. The flight cases depart at 90 degrees and land at 180
degrees, checking that all five controls keep their original deck attachments.
`MohawkControlsTest` checks side hatch wiring, facing, anchors and button operation;
`MohawkPresentationTest` checks door animation and rotated button sprite offsets.
`MohawkRampPresentationTest` loads all four layout/paint combinations on the
server and checks all twelve lowered pieces, their states, positions and
visibility on a connected client, including
retraction and reopening after rotation. Static validation rejects map-only
sprite overrides, which the server discards instead of replicating to clients.
`MohawkOccupantsTest` checks every mapped seat with human passengers, physical
wall clearance while buckled and after unbuckling, networked seat artwork through
rotation, both kinds of ramp occupant parenting, takeoff during retraction,
lowering with people above and below the ramp, and collision across all four gear
footprints. `MohawkEquipmentTest` checks onboard power, cabinet artwork, camera
and light mounting through rotation, solid table fixtures, and medevac-compatible
utility slots in both paint schemes. The flight regression also checks that collidable hull pieces, seats
and landing gear retain their anchors and local positions after actual FTL.
`MohawkUndercarriageTest` checks every lower-deck part through actual FTL on the
server and connected client, including the belly artwork whose origin is over
empty space. People deliberately parented to the servicing and external-control
tiles must remain on the original ground map. Ramp regressions check the solid
bulkhead, full-tile alignment, normal stair profile, and bystanders just beyond
the visible foot of the ramp.
The undercarriage flight cases also verify parallax on every flight map,
replicated cabin warp points, the original shuttle takeoff/landing cues, ramp
edging attachment after opening and flight, and matching tile definitions on all
three decks of the connected client.
`MohawkHijackTest` checks cabin-only impacts, deletion on server and client,
preserved passengers/bystanders and disabled boarding. `MohawkWeaponVisualsTest`
checks installed weapons, ammunition and removal on connected clients in all
four layouts/paint schemes. It measures the selected frames to reject blank or
cropped weapon fragments, checks rocket ammunition thresholds, and verifies that
standard mounts retain their original frames and directional offsets.
`MohawkAttachmentsTest` installs real equipment through power-loader attachment
completion, checks fuel/cooling effects on flight timing, fires a GAU and the
Midway M90, and hoists a stretcher patient into the moving cabin with medevac.
It also checks the compact engine faces, their placement on the black plates,
and the servicing click targets before and after installation.
`MohawkRoundSetupTest` starts the actual USCM and HazOps platoon rule on USS Bush,
checks the replacement Midway and its faction controls, verifies empty equipment
and ammunition slots and fabricator availability, then checks all three decks
after arrival.

Run the regression fixtures from the repository root:

```powershell
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~Mohawk|FullyQualifiedName~CMUZAudioLifecycleTest|FullyQualifiedName~SupportActivationTest|FullyQualifiedName~ZLevelNetworkTest|FullyQualifiedName~DynamicGunshipMapTest'
```

Use separate build artifacts if a running local server has the normal output
assemblies locked. The integration output directory must be two levels below the
repository root, such as `bin/cmu-mohawk-tests`, for resource discovery.

The integration project builds Content.Shared, Content.Server and Content.Client.
Resource validation covers 12 grids, 4 deployment maps, 2,670 entities, 1,654 RSI
states and 7 mono positional sounds. The regression fixtures above cover the
server and connected client, including full flights, equipment use, all 83 seats,
all four layout/paint combinations and the two faction round-setup paths.

The cabin and installed-equipment screenshots in `media/` were captured from a
fresh client/server. An earlier report of scrambled cabin tiles did not recur
there; its original cause remains unconfirmed. The regression suite compares
replicated tile definitions on all three decks through flight.

## Spawn a fresh ship

Restart the server and client with the updated content build. Choose an unused
map number and enter each command separately:

```text
addmap 100
loadgrid 100 /Maps/CMU14/ShuttlesDropships/Mohawk/omaha.yml
tp 0 0 100
```

Replace `omaha.yml` with `midway.yml`, `omaha_navy.yml`, or `midway_navy.yml`
for the other layout/paint combinations. The grid loader creates the linked
lower and upper decks automatically. The deployment files are for round setup.
Existing spawned ships retain their old layout, entities and prototypes; load a
fresh ship to inspect these changes.

## Equip the Midway chin gun

The fixed M90 starts installed with an empty ammunition slot. Print a PGU-420
90mm crate from the dropship parts fabricator's Ammo menu for 275 points and
load it into the underside chin mount with a powerloader. Each crate holds 400
rounds: ten 40-round bursts. Standard GAU ammunition does not fit. Buckle into
the gunner seat and use the dedicated gunnery console with normal CAS targeting.

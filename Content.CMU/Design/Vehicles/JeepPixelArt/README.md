# Jeep pixel art

`jeep_pixel_art.py` draws every jeep sprite from one voxel model and writes them to
`Content.CMU/Resources/Textures/CMU14/Structures/vehicles/jeep/`. Previews go to `bin/jeep-preview/`.

```
python Content.CMU/Design/Vehicles/JeepPixelArt/jeep_pixel_art.py
```

## Projection

Screen x is the world x and screen y is `-depth * DEPTH_SCALE - z * HEIGHT_SCALE`, per view:

- The front and back views (S, N) are seen from high up: depth at 1.05 and height at 0.5 facing
  south, 0.45 facing north (`DEPTH_SCALE`, `HEIGHT_SCALE`). Only a strip of the grille and tailgate
  shows, so a jeep turned between headings lies flat on the ground instead of tipping like a box.
  Three parts are drawn differently there so they still read:
  - Each wheel, the spare too, keeps its round, upright shape from the steeper 1:1 scales
    (`WHEEL_SCALE`), moved to where the flatter projection puts its centre. The near axle's tyres
    stand in front of the body (`NEAR_WHEEL_PRIORITY`), full width beside the headlights or tail
    lights, and the lamps at that end stay in front of the tyres (`LAMP_MATERIALS`).
  - The windshield stands taller above its hinge than the body (`WINDSHIELD_HEIGHT`: S 0.8, N 0.6),
    so its frame, divider and glass read.
  - The closed hood's shoulders are drawn level with its top (`HOOD_SHOULDERS`), a square panel as in
    the side view.
  Two heights share a screen row at these scales; ties go to the higher voxel (`HEIGHT_TIE`), so
  lower details like the grille slats never show on the edge above them.
- Side views (E, W) squash the jeep's width to 0.65 and stretch its length by 1.25
  (`LENGTH_SCALE`), so the side view is as long as the front view is tall. The road wheels stay
  round on the stretched axles.
- In the front and back views the far axle's wheels are drawn shifted towards the far end
  (`FAR_WHEEL_SHIFT`: S -13, N -14 rows) and tucked behind the body. The near tyres are lifted 5
  rows in both (`NEAR_WHEEL_LIFT`).
- Seat backs stop at the riders' necks (`SEAT_BACK_TOP`), and lower facing south so the rear
  riders' heads clear the front seat backs. Facing north and south they join the overlay where
  they are in front of a rider. The transport's rear bench
  stands 3 voxels higher than the front seats (`REAR_BENCH_LIFT`), so the rear riders' heads clear
  the front seat backs.
- In game, seated riders are cut off at their hip row (`clips` on each seat, from `RIDER_HIP_ROW`),
  so standing mob sprites read as seated and their legs never show through the seats in front.

Robust y-sorts sprites by the bottom of their whole frame. Overlay-entity states (`*overlay*`,
lights, signals, smoke, outlines) are stored slid down per direction (`overlay_slide_px` in
`seat_offsets.json`) and drawn back up by the overlay's `directionOffsets`, so the overlay sorts at
the jeep's near edge: people in front draw over it, riders under it.

## RSIs

| RSI | Version |
| --- | --- |
| `jeep.rsi` | 2 seats, cargo bed with a crate slot |
| `jeep_gunner.rsi` | 2 seats + gunner on the pedestal turret |
| `jeep_transport.rsi` | 4 seats |
| `jeep_medical.rsi` | 2 seats, the passenger's turned to face a surgical bed across the back; red crosses on the hood and the passenger's door |
| `jeep_wreck.rsi` | one burnt-out wreck per version, named like its RSI |
| `hardpoints.rsi` | item icons for parts with no game item yet: `tires`, `mgturret`, `windshield`, `wirecutter`, `searchlight`, `engine`, `headlight`, `turn_signal`, `taillight`, `hood`, `door`, `tailgate` |
| `cargo_straps.rsi` | `straps`, drawn over a standard crate once it is wrenched into the slot |
| `jeep_beams.rsi` | `beam_low`, `beam_high`: the headlight haze, 320 px, one direction, the cone pointing down from the middle of the top edge |
| `light_masks/` | `low_beam.png`, `high_beam.png`: the beams' light cones, pointing up (the game turns them to the jeep's front) |
| `actions.rsi` | hand-drawn `hazards_off` / `hazards_on`; generated `headlights_off` / `_low` / `_high` |
| `vehicle_spray_painter.rsi` | the vehicle spray painter's `icon` and the white `paint` in its cup, tinted in game with the picked colour |
| `blood.rsi` | `splat_1`-`5` and `drip_1`-`3`: white blood splashes, tinted in game with the bleeder's blood colour |

All jeep states are 96 px with 4 directions. The paint is bare olive drab: players decorate jeeps
with crayons. Only the medical jeep is marked, with outlined red crosses on white (`cross_field`).

## Layer stack

The jeep is open, so riders are drawn between the vehicle and an overlay entity:

1. Vehicle entity: `jeep_base`, `damaged_frame`, `engine_*`, the hood state, the extinguisher
   (under the doors: the passenger's swings open in front of it), the doors and the tailgate, the
   fuel door, the other hardpoints (`BASE_KIT_ORDER`: the tail lights over the spare beside them),
   then `wheels_0` / `wheels_1` (layer `rmc-wheels`).
2. Crate (when loaded) and riders, far to near. Facing north the crate goes after the riders.
3. Overlay entity: `jeep_overlay`, `damaged_overlay`, the doors' and tailgate's `*_overlay`
   states, `hood_closed_overlay`, the hardpoint
   `*_overlay_0` / `_1` states, then the raised hood (`hood_opening` / `hood_open` /
   `hood_closing` `_overlay`) and the fuel door `*_overlay` states, `wheels_overlay_0` / `_1`, then
   the unshaded lit lamps (see Lights), `engine_smoke_*`, `engine_fire`, and last the `*_outline`
   hover highlights.
4. Turret visual: `mgturret_0` / `mgturret_1`, like `humveeturret_0`. Facing north the gunner
   stands between the camera and the gun, so in that direction the turret goes under the riders.

## Turret

The gunner swings the gun through a forward arc of about 90 degrees (45 either side of the
jeep's nose).
- The turret states have 8 directions, so the gun reads at the ends of that arc too.
- `mgturret_raised_0` / `_1` show the barrel lifted for when the gunner aims up. This is only a
  look: the jeep has no anti-air, so what the gun can hit follows the normal gun rules.
- RMC turrets rotate freely and `GunFireArc` only blocks the shot, so the jeep's turret also needs
  its rotation clamped to the arc.

`jeep_turret_arc.png` shows each facing with the gun forward, at both ends of the arc, and aimed
up.

The gun draws the nearest of its 8 direction frames and turns that frame by the leftover angle,
at most 22.5 degrees. Inside the arc it therefore uses three frames (front-left, front,
front-right) and turns smoothly between them. RMC's exact cardinal system forces every turret
visual onto a cardinal frame and only turns four-way states back, which would draw this gun as
its cardinal frame turned by the jeep's whole heading (right only facing south). So
`CMUVehicleOverlayVisualSystem` picks the gun's frame from its aim on the jeep's drawn frame and
turns it back by that frame's angle, every frame after RMC's system.
- `jeep_turret_sweep_ingame.gif` mimics that.
- `jeep_turret_sweep_smooth.gif` re-renders the gun at every angle, for comparison.

Each overlay state only holds the directions where that piece is in front of the riders. It mirrors
its base state and toggles with it.

## Hardpoints

Every piece hung on the jeep is a hardpoint holding a real item. The body keeps only the brackets,
so an empty slot still reads.

| Slot | States | Holds |
| --- | --- | --- |
| wheels | `wheels_0` intact (animated while moving), `wheels_1` destroyed; hidden when removed (brake drums show) | jeep wheel |
| windshield | `windshield_up_*`, `windshield_down_*`; the damaged glass has bullet holes | jeep windshield |
| turret (gunner) | `mgturret_0` / `_1` | heavy machine gun |
| spare | `spare_*`; drawn as its tread face from the side; removed leaves the carrier. On the cargo and gun jeeps it hangs on the tailgate and folds under it | the same wheel-set item the wheel slot takes, so it replaces destroyed wheels |
| jerry can | `jerrycan_*`; the damaged can is holed and drips fuel. On the tailgate like the spare | `RMCFuelCan` |
| shovel | `shovel_*`, on the driver's door, swinging with it | `CMEntrenchingTool` |
| axe | `axe_*`, on the driver's door above the shovel | `RMCFireAxe` |
| hood | `hood_*` | jeep hood |
| doors | `door_driver_*`, `door_passenger_*` | jeep door (fits either side) |
| tailgate (cargo, gunner) | `tailgate_*`; blank states on the transport | jeep tailgate |
| front | `wirecutter_*` | wire cutter |
| cowl | `searchlight_*` | searchlight |
| engine | `engine_*`, in the bay under the hood | jeep engine |
| headlights | `headlight_left_*`, `headlight_right_*`, big round lamps beside the grille; removed leaves the brackets | jeep headlight (fits either side) |
| turn signals | `turn_signal_left_*`, `turn_signal_right_*`, amber lenses on the front fenders; removed leaves the housing | jeep turn signal |
| tail lights | `taillight_left_*`, `taillight_right_*`, the rear clusters (tail and brake light over the rear turn signal); removed leaves the bracket | jeep tail light |
| extinguisher | `extinguisher_*`, strapped to the outside of the cowl ahead of the passenger's door; removed leaves the shelf and strap | `CMFireExtinguisherPortable` (any extinguisher fits) |

Left is the driver's side. The lamps are only a few pixels, so their `click_*` masks take in the
ring of jeep around them.

Following the humvee, `_0` is intact and `_1` is damaged (`damagedVehicleState`). `PRIMARY`,
`ATTACH`, `WHEEL` are empty placeholders for hidden hardpoint layers.

## Hood and engine

- **Hood states:** `hood_closed` and `hood_open` (each with an `_overlay` twin). The open lid swings
  to 80 degrees, short of upright, so it stays ahead of the raked windshield (both stay easy to
  click) and the bay stays in view from the front.
- **Animation:** `hood_opening` and `hood_closing` are 7 frames at 0.07 s. Play one, then switch to
  `hood_open` or `hood_closed`.
- **Draw order with the windshield:** facing south, a shut hood is drawn under the windshield,
  which can lie folded on it. A raising, raised or closing lid is drawn over it, in front of the
  upright windshield. The two never clash because the hood only opens with the windshield up. In
  practice that is two overlay layers, `hood` below the hardpoints for the shut hood and
  `hood_raised` above them for the moving and open lid.
- **Engine hardpoint:** sits in the bay under the lid, so it only shows with the hood open. To fix a
  damaged engine, open the hood and replace or repair the engine hardpoint.
- **Smoke:** `engine_smoke_0` (light, damaged) and `engine_smoke_1` (heavy, nearly broken) are
  8-frame loops leaking from the hood edges and grille. They show whether the hood is open or
  shut.

`jeep_hood_swing.gif`, `jeep_hood_open.png` and `jeep_engine_smoke.gif` show them.

## Windshield

- **Animation:** `windshield_folding_0/1` and `windshield_raising_0/1` (each with an `_overlay`
  twin) swing the frame between up and folded onto the hood, 5 frames at 0.07 s. Play one, then set
  the windshield hardpoint to `windshield_down_*` or `windshield_up_*`.
- **Damage:** the `_1` versions keep the bullet holes through the motion.

`jeep_windshield_fold.gif` shows it.

## Doors and tailgate

- **Side doors:** the openings run from the front seats to the cowl, above the step, on all four
  jeeps. Hinged at their front edge, they swing out to 70 degrees: `door_driver_*` and
  `door_passenger_*`, each `closed`, `open`, `opening`, `closing` (5 frames at 0.07 s) with
  `_overlay` twins. The driver's door is on the left (-r) and carries the shovel and the axe.
- **Tailgate (cargo, gun and medical jeeps):** the rear wall between the corner posts, above the
  bed, folds down rearward until it lies level with the bed: `tailgate_*`, same states. The spare and the
  jerry can hang on it and fold away under it. The transport's bench backs onto a fixed wall.
- **Kit on a panel:** `shovel`, `axe`, `spare` and `jerrycan` have `<name>_open_<0/1>`,
  `<name>_opening_<0/1>` and `<name>_closing_<0/1>` (with `_overlay` twins) swinging with their
  panel, plus `<name>_open_outline` and `click_<name>_open`.
- **Shading:** mid-swing a panel is shaded as one flat surface, like the open hood; lying level, the
  tailgate is back on the voxel grid and shades like the body, with its hinge line across the bed.
- **Fuel door:** on the right rear quarter, under the rim above the wheel arch, behind the passenger
  door: a small hatch framed by a dark gap, with a hinge and a latch.

`jeep_doors.gif` swings everything; `jeep_doors_open.png` has it all open.

## Lights

The driver's "Headlights" action cycles off, low beam, high beam. RMC's `VehicleSpotlight` flag
follows it, so F (the "Flip object" / "Holster primary" binding) moves the same switch on. Each beam
is a child entity of the jeep with its own cone-masked light (`light_masks/`) and haze sprite
(`jeep_beams.rsi`), lit only through fitted, intact headlights. The haze's shader shows it only
where its own light lands above the map's ambient light, so walls cut it off and daylight hides it.
The beams start at the headlights the art draws in each direction
(`headlight_lamp_anchor_px` in `seat_offsets.json`); each headlight has its own low and high beam, its light kept on the lens the art draws. The overlay also has unshaded states for the
lamps themselves, one per lamp and side (`LIT_STATES`), each shown only while that lamp is fitted
and intact:
- `headlight_<side>_on`: the headlights.
- `taillight_<side>_on`: the tail (position) lights.
- `brake_<side>`: the tail lights burning brighter, for braking, with or without the lights on.
- `signal_front_<side>` (the fender lens) and `signal_rear_<side>` (the tail light's amber): the turn
  signals, blinking at 0.4 s. Left is the driver's side.
  - The driver's "Hazard lights" action blinks both, moving or not (`actions.rsi` icon).
  - While steering left or right, only that side blinks, unless the driver has switched the
    "Automatic turn signals" action off (`signals_auto_off` / `_on`).
  - Reset both animations together so they blink in step.

The driver also gets a "Horn" action, which plays the jeep's `VehicleSound` horn with its
cooldown; RMC's own horn key (use item in hand) works too.

Every lit lamp also casts a subtle wash of its colour over the nearby bodywork, stronger for the
brakes. The searchlight hardpoint already scales the spotlight. `jeep_lights.png` shows night with the
lights off, with hazards, and signalling left and right; `jeep_signals.gif` blinks them;
`jeep_lamps_missing.png` has three lamps taken off, by day and lit at night.

## Interaction

- **Empty hand:**
  - Clicking the hood opens or closes it. It can't open while the windshield is folded down on it.
  - Clicking the windshield folds it down or raises it, only while the hood is shut.
  - Clicking a door or the tailgate opens or closes it. A seat's door open (or off) lets its rider
    in and out at once; shut, climbing over takes 2 seconds. The gunner climbs in over the
    tailgate; the transport's rear seats have no door.
  - Crates load and unload, and a loose one slides off, only over a lowered tailgate. The spare and
    the jerry can can't be reached while it is down.
  - Clicking the fuel door opens or closes it.
  - Clicking a fitted item (shovel, axe, jerry can, spare, extinguisher, wheels and so on) takes it
    into the hand. A lamp only comes off with a screwdriver.
- **Holding an item:** an empty slot the item would go into shows its mask as a faint ghost of what
  goes there, with its `*_outline` on hover; clicking it fits the item there. That picks the side
  for a door or a lamp, which fit either. Clicking the jeep elsewhere fills the first empty slot.
- **Key:** any jeep key goes in the ignition by clicking the jeep with it; alt-click takes it out.
  Without it the engine won't start, and pulling it stops the engine. While someone drives, only
  those aboard can reach it.
- **Swapping moving parts:** the windshield, hood, doors and tailgate come off screwdriver (screws
  out), wrench (bolts out), then an empty hand lifts them off. They go back on the other way round:
  hung on with the part in hand, bolted with a wrench, screwed down with a screwdriver. A panel with
  kit hung on it comes off only once that kit is off.
- **Hover highlight:** `hood_outline` and `fuel_door_outline` highlight those click targets, and
  `seat_<name>_outline` highlights each seat (`driver`, `passenger`, `rear_left`, `rear_right`,
  `gunner`, as the version has them). Outlines are a white one-pixel rim, which the client tints
  like the standard hover outline: green in reach, red out of reach.
- **Click maps:** `click_maps/<rsi name>_hood_closed.png` and `_hood_open.png` give the part under
  each pixel, in the same layout as the crayon maps. Alpha marks the jeep and R holds the part id:

  | id | part | id | part | id | part |
  | --- | --- | --- | --- | --- | --- |
  | 0 | body | 5 | windshield | 10 | wire cutter |
  | 1 | hood | 6 | spare | 11 | searchlight |
  | 2 | fuel door | 7 | jerry can | 12 | wheels |
  | 3 | engine | 8 | shovel | 18–20 | driver's door, passenger's door, tailgate |
  | 28 | extinguisher | 9 | axe | 13–17 | seats: driver, passenger, rear left, rear right, gunner |
  | 27 | surgical bed | 21–26 | headlights, turn signals, tail lights (left, right each) | | |

  The maps are drawn with every slot filled, so they also find an empty slot.

## Sounds

In `Content.CMU/Resources/Audio/CMU14/Jeep/`, cut from the team's Pixabay recordings (see
`attributions.yml`):
- `jeep_engine_start`: plays when someone takes the wheel and the jeep can run (key, fuel, engine).
- `jeep_engine_idle` and `jeep_engine_drive`: seamless loops, the second the idle sped up. Each
  client plays both while the engine runs and fades from the idle at rest to the driving loop at
  top speed.
- `jeep_engine_rough` and `jeep_engine_rough_drive`: the same pair, used while the engine is
  below its smoke threshold.
- `jeep_engine_rev`: plays when the jeep pulls away, at most every 3 seconds.
- `jeep_engine_sputter`: the engine stalls under the driver (tank dry, engine dead). A driver
  getting out just switches it off.
- `jeep_hood_open`, `jeep_hood_close` and `jeep_windshield`: with their swings.

The jeep has no RMC running sound; the horn and collision sounds are RMC's.

## Motion

While driving, everything but the wheels should bob on its springs: a damped spring kicked by
random bumps, rounded to whole pixels. Apply it in code as a layer and entity offset on the
body, the overlay, the riders and the turret, not as sprite states. `jeep_moving.gif` shows the
curve; `suspension_bob()` in the script is the reference.

## Dirt, blood and cleaning

- **Dirt** builds up with distance driven: fastest on bare (diggable) ground, slower on paved ground
  outdoors, hardly at all indoors. The paint shader shows it as dust in blotches over every pixel,
  growing with the dirt, and as mud caking the low, mud-splashed bodywork first. It is sent in
  tenths.
- **Blood** goes on like crayon drawings, from `blood.rsi`, tinted with the bleeder's blood: a splat on
  a rider's seat or the floor in front of it when they take a few points of brute damage, drips
  while they bleed, and splats on the bumper, grille or hood when the jeep runs someone down.
- **Cleaning:** each unit of space cleaner (or soap or water) sprayed or splashed on the jeep takes off
  a tenth of its dirt, blood and crayon, oldest first. Scrubbing it down with soap, or a mop wet with
  any of them, takes it all off in a few seconds.

## Medical jeep

A surgical bed fills the back (`BED`, `surgical_bed()`): a white mattress on a steel frame across
the rear compartment, over the wheel arches, behind a tailgate. The passenger seat is the cargo
jeep's turned end for end (`REVERSED_SEATS`), its back against the dash, so the medic faces the bed.
- In game the bed is a seat that lays its patient down (`Strap` `position: Down`) and counts as an
  operating table (`CMOperatingTable`), so surgery can be done on the move. Clicking the jeep never
  puts anyone on it (`autoSeat: false`); a patient is laid on it, by dragging them onto it or by its
  verb, over the tailgate.
- The patient is always drawn lying across the bed, head by the medic (`LYING_SEATS`, the seat's
  `lyingAngles`): turned a quarter turn facing south and north, upright on screen facing east and
  west (head down facing east, up facing west), the near wall over their lower half.
- The rear-facing medic is drawn with their back to the camera facing south (`Reversed`).

## Burning wreck

When damage takes the hull to zero, the engine catches fire (`CMUJeepComponent.Burning`): flames
lick out from under the hood and through the grille (`engine_fire`, 8 frames at 0.08 s, over the
heavy smoke), a flickering orange glow lights it, the fire crackles, and sparks fly from the engine
bay with now and then a bang. After six seconds it goes up and leaves a wreck from
`jeep_wreck.rsi`, which the fire left under it doesn't burn away. `jeep_engine_fire.gif` shows it.

## Kit crate

A jeep can come as a kit crate (cargo, medical, transport or gun jeep). Opened, the bare chassis
takes the crate's place and its contents are laid out on both sides:
- wheel sets;
- windshield, hood, two doors, and the tailgate on cargo, medical and gun jeeps;
- two headlights, two turn signals and two tail lights;
- the gun mount for the gun jeep;
- two fuel cans, the shovel, the fire axe and the fire extinguisher;
- a wrench, a screwdriver, the ignition key and the assembly manual.

The chassis has its engine but an empty tank, and no lamps or key. The crate parachutes in like any
crate from a squad's supply drop pad. As `CMUDropshipAttachmentAmmoLaunchableJeep*`, it is a round
for a dropship's LAG-14 launcher, dropped by parachute at a laser or flare.

The platoons order the kits from the ASRS under Vehicles (`jeep_requisitions.yml`): cargo 12,000,
medical 15,000, transport 17,500, gun jeep 20,000, against an account that opens a round at 20,000.

## Crayons

`crayon_maps/<rsi name>.png` records which point of the body every pixel shows, in the RSI layout
(S, N, E, W in a 2x2 grid of 96 px tiles):

| Channel | Meaning |
| --- | --- |
| A | 0 off the body, else 255 (under riders) or 128 (over riders) minus the panel: 1 hood lid, 2 fuel door, 3 driver's door, 4 passenger's door, 5 tailgate, 0 other bodywork |
| R | `f + 128`, voxel position along the jeep (forward is +f) |
| G | `r + 128`, voxel position across the jeep (its right is +r) |
| B | `2 * z` on a top face, `2 * z + 1` on the face turned to the camera (S: +f, N: -f, E: +r, W: -r) |

A drawing is stored on the body, not the screen:
1. Look up the clicked pixel to get its anchor point and face.
2. Store the decal's right and down axes in jeep space, taken from the direction it was drawn in.
3. To draw a direction, paint the decal only into pixels on the same face within a couple of voxels
   of the anchor's plane, blended with the shading underneath.

That keeps it on the same spot as the jeep turns. It rotates with the jeep on top faces, and side
panels show it only when they face the camera. Pixels off the body or hidden behind something
never take paint, so nothing floats off the edge.

Wheels and hardpoints aren't in the map: drawings stay on the bodywork. Use the alpha to split the
painted pixels between the vehicle entity and the overlay entity. `crayon()` and `crayon_demo()` in
the script are the reference; `jeep_crayons.png` shows the result.

## In game

Prototypes are in `Content.CMU/Resources/Prototypes/CMU14/Vehicles/Jeep/jeep.yml`, strings in
`Content.CMU/Resources/Locale/en-US/CMU14/vehicle/jeep.ftl`. Code:

| File | Does |
| --- | --- |
| `Shared/Vehicle/Jeep/CMUVehicleOverlay*` | spawns the overlay entity drawn over the riders |
| `Shared/Vehicle/Jeep/CMUVehicleSeat*` | one strap entity per seat (the driver's seat drives), exit points, climbing in and out over shut doors, riders' gun scatter by firearms skill and the vehicle's speed (with no camera kick); seated riders keep their body but lose hard contacts and are held still around each physics step, so bumps don't unbuckle them while fire, smoke, bullets, knockbacks and drags still reach them |
| `Shared/Vehicle/Jeep/CMUVehicleEngineSound*`, `Server/.../CMUVehicleEngineSoundSystem.cs`, `Client/Vehicle/CMUVehicleEngineLoopSystem.cs` | engine start, rev and stall from the server, running loops faded by speed on each client |
| `Shared/Vehicle/Jeep/CMUJeep*` | hood, windshield, door, tailgate and fuel door toggles, taking moving parts off and on in steps, clickable parts, item hooks, engine/windshield/lamp wear and repair, each lamp in its own slot (`CMUJeepLamps`), the headlight switch and its beams, jerry can leaks, stray shots passing through, the burning wreck |
| `Shared/Vehicle/Jeep/CMUVehicleIgnition*` | the ignition key slot: no key, no running |
| `Shared/Vehicle/Jeep/CMUVehicleFuel*` | fuel use and refuelling from a fuel can at the fuel door; the do-after bar starts at the tank level |
| `Shared/Vehicle/Jeep/CMUVehicleCargo*` | loading, wrenching down and unloading a crate on the cargo bed, over the tailgate |
| `Shared/Vehicle/Jeep/CMUVehiclePaint*`, `CMUVehicleSprayPainter*`, `paint.swsl` | the vehicle spray painter (an RGB colour window in hand, 30 charges, 5 a respray) and respraying the jeep with it; the art marks its paint pixels (alpha 254, 253 under mud) |
| `Shared/Vehicle/Jeep/CMUVehicleCrayon*`, `Server/.../CMUVehicleCrayonSystem.cs` | crayon drawings (up to 50) and blood splashes anchored to the body via `crayon_maps/` |
| `Shared/Vehicle/Jeep/CMUVehicleGrime*`, `Server/.../CMUVehicleGrimeBuildUpSystem.cs` | dirt from driving, blood from wounded riders and anyone run down, cleaning |
| `Shared/Vehicle/Jeep/CMUVehicleKitCrate*`, `Server/.../CMUVehicleKitCrateSystem.cs`, `jeep_kit.yml` | kit crates opening into a chassis with its parts, launch-bay rounds, the manual |
| `Client/Vehicle/CMU*` | overlay frame and lights, part masks and hover outlines, swing animations, damage looks, suspension bob, crate placement, crayon painting, paint, headlight haze |

Clickable parts are child entities whose sprite is the `click_*` mask: the part's own pixels at
about 12% alpha, just over the engine's click threshold and invisible over the part itself. Clicks
and hover pick the part, and the hover draws the matching `*_outline` state on the overlay. Where
masks overlap, the part drawn on top in that direction takes the click (its render order follows
the layer stack); the seats are under every other part.

CMU14-marked edits outside the jeep files: RMC's grid mover ignores entities riding the vehicle,
RMC's weapon seats fall back to the open vehicle a CMU seat belongs to, turrets take an optional
`cmuMaxYawDegrees` arc, `SharedDoAfterSystem.CMUSetProgress` starts a do-after part-way, and RMC's
fuel can carries a `CMUJerryCan` tag and a `fuel` solution.

Test: `Test/run_jeep_test.ps1` starts a local server on Stable Garrison (`StableGarrisonRedux`, no
lobby, about a minute to load) and a client that joins it. Build the server and client first.
`Test/server.toml` turns on the jeep dev settings in `Server/Vehicle/Jeep/CMUJeepDevCVars.cs`, all
off by default:
- `cmu.jeep.dev_job`: joining players spawn as this job if the station offers it. Garrison has no
  Passenger slot, which a new test account asks for, so it is `AU14JobCivilianColonist`.
- `cmu.jeep.dev_kit`: `CMUJeepDevKitSystem` parks the four jeeps on the nearest open ground around
  each player (under open sky where there is room, facing south, three tiles apart), each with its
  key on the ground by the driver's door, with a welder,
  wrench, screwdriver, crayons, a vehicle spray painter, a fuel can, cupola ammo, a crate and a fuel tank, and moves the
  player next to them. The server log line `Jeep dev kit for ...` gives the entities and where.
- `cmu.jeep.dev_daylight`: the round starts at midday instead of dawn.

The admin command `jeepkit [username]` parks another set next to a player on any server.

## Offsets

`seat_offsets.json` has pixel offsets (x right, y up, from the vehicle origin) per direction for:
- each rider seat,
- the turret pivot (`pixelOffsetSouth` etc.) and its muzzle (`mgturret_muzzle_px`: along the barrel
  and up the screen, the `GunMuzzleOffset` of the gun),
- the crate sprite in the cargo slot,
- the headlights, where the beams start.

Regenerate it together with the sprites.

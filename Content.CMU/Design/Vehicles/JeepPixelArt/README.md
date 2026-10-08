# Jeep pixel art

`jeep_pixel_art.py` draws every jeep sprite from one voxel model and writes them to
`Content.CMU/Resources/Textures/CMU14/Structures/vehicles/jeep/`. Previews go to `bin/jeep-preview/`.

```
python Content.CMU/Design/Vehicles/JeepPixelArt/jeep_pixel_art.py
```

## RSIs

| RSI | Version |
| --- | --- |
| `jeep.rsi` | 2 seats, cargo bed with a crate slot |
| `jeep_gunner.rsi` | 2 seats + gunner on the pedestal turret |
| `jeep_transport.rsi` | 4 seats |
| `hardpoints.rsi` | item icons for parts with no game item yet: `tires`, `mgturret`, `windshield`, `wirecutter`, `searchlight`, `engine`, `headlights` |
| `cargo_straps.rsi` | `straps`, drawn over a standard crate once it is wrenched into the slot |

All jeep states are 96 px with 4 directions. The paint is bare olive drab, no markings: players
decorate jeeps with crayons.

## Layer stack

The jeep is open, so riders are drawn between the vehicle and an overlay entity:

1. Vehicle entity: `jeep_base`, `damaged_frame`, `engine_*`, the hood state, the other
   hardpoints, then `wheels_0` / `wheels_1` (layer `rmc-wheels`).
2. Crate (when loaded) and riders, far to near. Facing north the crate goes after the riders.
3. Overlay entity: `jeep_overlay`, `damaged_overlay`, the hardpoint `*_overlay_0` / `_1` states,
   the hood `*_overlay` state, `wheels_overlay_0` / `_1`, then the unshaded `lights_on`,
   `headlights_on` and `engine_smoke_*`.
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

Robust turns a rotating directional sprite smoothly on its own: it draws the nearest of the 8
direction frames and rotates that frame by the leftover angle, at most 22.5 degrees. Inside the
arc the gun therefore uses three frames (front-left, front, front-right) and turns smoothly between
them. The cost is one transform per frame on the turret visual, which RMC already updates for every
turret.
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
| spare | `spare_*`; drawn as its tread face from the side; removed leaves the carrier | the same wheel-set item the wheel slot takes, so it replaces destroyed wheels |
| jerry can | `jerrycan_*`; the damaged can is holed and drips fuel | `RMCFuelCan` |
| shovel | `shovel_*` | `CMEntrenchingTool` |
| axe | `axe_*` | fire axe |
| front | `wirecutter_*` | wire cutter |
| cowl | `searchlight_*` | searchlight |
| engine | `engine_*`, in the bay under the hood | jeep engine |
| headlights | `headlights_*`, big round lamps beside the grille; removed leaves the brackets | headlights |

Following the humvee, `_0` is intact and `_1` is damaged (`damagedVehicleState`). `PRIMARY`,
`ATTACH`, `WHEEL` are empty placeholders for hidden hardpoint layers.

## Hood and engine

- **Hood states:** `hood_closed` and `hood_open` (each with an `_overlay` twin). The open lid swings
  back past upright to lean on the windshield, so the bay stays in view from the front.
- **Animation:** `hood_opening` and `hood_closing` are 7 frames at 0.07 s. Play one, then switch to
  `hood_open` or `hood_closed`.
- **Engine hardpoint:** sits in the bay under the lid, so it only shows with the hood open. To fix a
  damaged engine, open the hood and replace or repair the engine hardpoint.
- **Smoke:** `engine_smoke_0` (light, damaged) and `engine_smoke_1` (heavy, nearly broken) are
  8-frame loops leaking from the hood edges and grille. They show whether the hood is open or
  shut.

`jeep_hood_swing.gif`, `jeep_hood_open.png` and `jeep_engine_smoke.gif` show them.

## Lights

The driver toggles RMC's `VehicleSpotlight` with F, the "Flip object" / "Holster primary" binding.
That only switches a point light, so the jeep adds two unshaded overlay states to show it:
- `lights_on`: tail lights and fender marker lamps.
- `headlights_on`: the headlights; show it only while the headlights hardpoint is fitted and
  intact.

The searchlight hardpoint already scales the spotlight. `jeep_lights.png` shows night with the
lights off and on.

## Motion

While driving, everything but the wheels should bob on its springs: a damped spring kicked by
random bumps, rounded to whole pixels. Apply it in code as a layer and entity offset on the
body, the overlay, the riders and the turret, not as sprite states. `jeep_moving.gif` shows the
curve; `suspension_bob()` in the script is the reference.

## Crayons

`crayon_maps/<rsi name>.png` records which point of the body every pixel shows, in the RSI layout
(S, N, E, W in a 2x2 grid of 96 px tiles):

| Channel | Meaning |
| --- | --- |
| A | 0 off the body, 255 drawn under riders, 128 drawn over riders |
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

## Offsets

`seat_offsets.json` has pixel offsets (x right, y up, from the vehicle origin) per direction for:
- each rider seat,
- the turret pivot (`pixelOffsetSouth` etc.),
- the crate sprite in the cargo slot.

Regenerate it together with the sprites.

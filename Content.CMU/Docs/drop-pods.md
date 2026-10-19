# CMU drop pods

Drop pods fire troops from a ship onto the planet below. A drop pod console
shows the planet's tactical map. An operator clicks an insertion point, ticks
the drop units to fire, and launches them. Every unit lands at a random point
within its `scatterRadius` (60 tiles by default) of the insertion point.

All values below are datafields on the prototypes in
`Resources/Prototypes/CMU14/Entities/Structures/Machines/drop_pod.yml`;
change them there or on a child prototype.

## Linking

A console fires every drop unit on its own map or z-network whose `group`
matches the console's `group` (default `default`). Use different groups to give
several consoles their own pods.

## Single-trooper pod (`CMUDropPod`)

- Holds one occupant matching `occupantWhitelist` (humanoids, not xenos), who
  enters by dragging themselves onto it or with the verb. It has two weapon
  racks (`ItemSlots`), which lock during flight.
- Flight plays each entry in `phases` in order: its parallax, message, sound,
  and camera shake. Atmospheric phases roll `atmosphereBreakupChance`. A pod
  that breaks up lands up to `breakupExtraScatter` tiles further away, then
  shatters on impact.
- Pods land on the z-level at `targetDepth` (0 by default). If an indestructible
  `landingBlockers` match (walls by default) is in the way, the pod lands on top
  of it, on the level above. With `respectParadropAreas`, pods do not land in
  areas that forbid paradrops.
- On landing the pod rolls `impactBreakupChance` to shatter: the occupant takes
  `breakupDamage` and is thrown out, and the weapons drop. Otherwise it rolls
  `injuryChance`. An injured occupant who stays inside is healed by
  `treatmentDamage` every `treatmentInterval`, `treatmentTicks` times. Moving
  climbs out. Uninjured occupants are let out after `openDelay`.
- A landed pod is spent and cannot be reused.

## Drop units built from walls and doors (`CMUCustomDropUnitCore`)

Like an elevator car, a drop unit is the room its core stands in. Mappers can
place one, and players can build one mid-round from the construction menu.

1. Build or map a room on the ship with walls (`wallWhitelist`, the `Wall` tag
   by default) and at least one door (`doorWhitelist`, any `Door`).
2. Place a `CMUCustomDropUnitCore` inside the room. In-round it is built from
   steel and plasteel and taken down with a screwdriver and crowbar.
3. Fire it from a linked console.

The room is found by flooding out from the core. Walls and doors stop the flood
and become part of the unit, including corner pieces. The launch is refused if
the floor has a hole, the room is not sealed or has more than `maxTiles` tiles
(walls included), there is no door while `requireDoor` is set, or another grid
is inside.

During the flight the unit stays docked. Its doors close and stay bolted while
everyone inside sees the unit's `phases` messages, sounds, and shakes. When the
last phase ends, the room's floor, walls, doors, and everything inside are cut
out of the ship and set down on the planet. Its old footprint is left as
`leftBehindTile` (`Plating`; set it to `null` to leave the tiles empty). Every tile of
the unit must fit on the landing level; mobs standing there take
`crushDamage`. Afterwards the doors are unbolted.

Custom units cannot heal their occupants or store weapons. They roll
`injuryChance` for each rider, and `atmosphereBreakupChance` and
`impactBreakupChance` to break apart. A broken unit's walls and doors become
`breakupDebris`, and riders take `breakupDamage`. If the unit can no longer
launch on arrival, for example because a wall was removed, the drop is
aborted and the unit is ready again.

Ship walls that form part of a unit leave with it, so build the unit inside a
room rather than against the hull.

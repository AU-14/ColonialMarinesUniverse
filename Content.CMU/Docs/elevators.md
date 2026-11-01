# CMU elevators

CMU elevators move a mapper-built platform between already linked z-levels.
Players pick any floor from the control, or call the elevator from a button on
their floor. A lift's footprint is determined by its rail outline, so it can be
rectangular or irregular; it does not need a fixed platform prototype.

## Build an elevator on a map

1. Make sure the map has the desired neighboring z-levels linked and aligned.
   The elevator does not create levels. A trip can go to any level in the
   z-network. The destination must have a grid with clear space at the same map
   coordinates, and every level in between must be open there too (no tiles or
   colliding non-mob entities in the shaft).
2. Place `CMUElevatorRail` entities to fence in the platform. Rails are
   directional railings: each rail fences the side of its tile that it faces.
   Put the rails on the platform's edge tiles, facing outward, so the platform
   is fenced on at least three sides. One side of the outline's bounding box
   may stay open (for example a railing lift that players walk onto); two or
   more open sides are rejected. Tiles beyond the open side are not part of the
   platform. A corner tile needs two rails, one for
   each outer side, and a single-tile lift needs four rails on that tile,
   one facing each direction. Every rail must stand on a platform tile. The
   rails must be on one grid and one z-level. Older layouts that use a solid
   ring of rail tiles around the platform still work.

   `CMUElevatorWall` (connecting elevator walls) and `CMUElevatorDoor` (UPP grey
   airlock) can stand in for rails. They fill their whole tile, so place them
   edge-to-edge around the platform like a wall ring, mixing them with rails as
   needed. Walls and doors are part of the platform and travel with it.
3. Place the platform floor inside the outline. The enclosed tiles, including
   rail, wall, and door tiles, are treated as the footprint. The system moves floor tiles and
   entities on those tiles, including people riding the platform. Do not put
   another grid on the platform; grid entities cannot be carried.
4. Place a `CMUElevatorControl` on an enclosed platform tile and give the
   control and every rail, wall, and door in this lift the same non-empty
   `elevatorId`. The control has no collision, so it can share a tile with a
   railing and players can walk over it.
5. Optionally, place a `CMUElevatorCallButton` on each floor the elevator
   serves, outside the shaft, and give it the same `elevatorId`. Pressing it
   calls the elevator to that floor.

For example, a mapper can add components to prototype instances like this:

```yaml
- type: entity
  id: CMUExampleLiftControl
  parent: CMUElevatorControl
  components:
  - type: CMUElevator
    elevatorId: "cargo-lift-a"
    disableSkillLevel: 3

- type: entity
  id: CMUExampleLiftRail
  parent: CMUElevatorRail
  components:
  - type: CMUElevatorRail
    elevatorId: "cargo-lift-a"

- type: entity
  id: CMUExampleLiftCallButton
  parent: CMUElevatorCallButton
  components:
  - type: CMUElevatorCallButton
    elevatorId: "cargo-lift-a"
```

Create map instances of the rail prototype to draw the outline and one control
instance inside it. Use a distinct ID for each elevator. IDs match exactly;
rails with a different or empty ID are not part of the lift. The default
`disableSkillLevel` is 3.

The control uses the repository's elevator-control artwork, and the rails use
the existing raised railing artwork.

## Use and operation

- Activate the control and pick a destination floor. Floors are numbered from
  the lowest level in the z-network (Floor 1) upward; the current floor is not
  listed.
- Press a call button to bring the elevator to that button's floor. Calls are
  refused while the elevator is disabled, already moving, or already on that
  floor.
- Trips take a short travel cycle. The control's `CMUElevator` `travelTime`
  (default 4 seconds; `0` moves instantly) sets how long the car is in
  transit. During the cycle the lift's `CMUElevatorDoor`s close and are
  bolted; they are unbolted again once the car is waiting on a floor. New
  requests are refused while the car is moving. The trip is checked when it
  is requested and again on arrival.
- Railing lifts can use `CMUElevatorRetractableRail` as their gate. It fences
  the side it faces like `CMUElevatorRail`, travels with the car, is lowered
  while the car waits on a floor, and is raised for the trip.
- `CMUElevatorLandingDoor` (or any elevator piece with `landing: true`) is a
  door placed on a floor, outside the shaft, where the car stops. Landing
  doors are not part of the outline and do not travel. They stay closed and
  bolted unless the car is waiting on their floor, where they are unbolted.
  Keep them out of the shaft footprint, or they block the car arriving.
- The trip is rejected if the rail outline or control placement is invalid,
  the destination is not a linked z-level, destination tiles are not empty, a
  colliding non-mob entity blocks the destination footprint, a level between
  the source and destination has tiles or colliding non-mob entities in the
  shaft, or another grid is on the platform. Ghosts (including admin ghosts)
  and entities without collision are ignored and are never carried.
- Any mob or player-controlled entity standing in the footprint on the
  destination level, or on any level the platform passes through, does not
  stop the elevator; it is crushed and killed when the elevator arrives. Keep
  the shaft clear; this is a lethal hazard, not a safety interlock.
- Platform tiles are transferred to the corresponding coordinates on the
  destination level. The floor the elevator is first used from is its home
  floor: when the car leaves it, the vacated footprint is left as
  `homeFloorTile` (default `Plating`), and the car may land on or pass through
  that plating later. On other floors the vacated footprint is left empty.
  Keep the destination footprint empty on every other level the elevator
  serves.
- The platform footprint's bounding rectangle is limited to 1,024 tiles.
- The control's `CMUElevatorWeightLimit` component sets the maximum number of
  entities the platform can carry. Its default `maxEntities` is 250. Every
  entity on the platform counts, including the contents of bags, crates, and
  riders' inventories (clothing, held items, organs). The elevator control and
  its rails, walls, and doors do not count toward this limit. If a move is
  attempted while the load exceeds the limit, the elevator breaks, stays in
  place, and must be re-enabled by an engineer. It will not gib
  destination-level mobs when a trip is refused for excess load.

## Disable or re-enable

An engineer with the configured engineering skill level can select the
elevator's **Disable elevator** or **Enable elevator** alternative verb and
confirm the prompt. The skill and minimum are configurable on the
`CMUElevator` component with `disableSkill` and `disableSkillLevel`; the default
is `RMCSkillEngineer` level 3. The requirement is checked both when showing
the verb and when applying the confirmation. Disabling prevents travel but
does not move or dismantle the lift.

## Prototype references

- `CMUElevatorControl`, `CMUElevatorRail`, `CMUElevatorWall`,
  `CMUElevatorDoor`, `CMUElevatorLandingDoor`, `CMUElevatorRetractableRail`,
  and `CMUElevatorCallButton` are defined in
  `Resources/Prototypes/CMU14/Entities/Structures/elevators.yml`.
- The component defaults and operation are implemented in
  `Shared/Elevators/CMUElevatorComponents.cs` and
  `Server/Elevators/CMUElevatorSystem.cs`.

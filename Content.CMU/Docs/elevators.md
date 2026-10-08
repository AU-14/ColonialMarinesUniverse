# CMU elevators

CMU elevators move a mapper-built platform between adjacent, already linked
z-levels. A lift's footprint is determined by its rail outline, so it can be
rectangular or irregular; it does not need a fixed platform prototype.

## Build an elevator on a map

1. Make sure the map has the desired neighboring z-levels linked and aligned.
   The elevator does not create levels. Each trip moves exactly one level up or
   down, and each destination must have clear space at the same map coordinates.
2. Place `CMUElevatorRail` entities in a continuous, closed outline around the
   platform. The outline must contain at least four rail tiles. The rails must
   be on one grid, on one z-level, and connected edge-to-edge. Diagonally
   touching rails do not connect.
3. Place the platform floor inside the outline. The enclosed tiles, including
   rail tiles, are treated as the footprint. The system moves floor tiles and
   entities on those tiles, including people riding the platform. Do not put
   another grid on the platform; grid entities cannot be carried.
4. Place a `CMUElevatorControl` on an enclosed platform tile and give the
   control and every rail in this lift the same non-empty `elevatorId`.

For example, a mapper can add components to prototype instances like this:

```yaml
- type: entity
  id: CMUExampleLiftControl
  parent: CMUElevatorControl
  components:
  - type: CMUElevator
    elevatorId: "cargo-lift-a"
    travelDirection: 1
    disableSkillLevel: 3

- type: entity
  id: CMUExampleLiftRail
  parent: CMUElevatorRail
  components:
  - type: CMUElevatorRail
    elevatorId: "cargo-lift-a"
```

Create map instances of the rail prototype to draw the outline and one control
instance inside it. Use a distinct ID for each elevator. IDs match exactly;
rails with a different or empty ID are not part of the lift. The defaults are
`travelDirection: 1` (up) and `disableSkillLevel: 3`.

The control uses the repository's elevator-control artwork, and the rails use
the existing raised railing artwork.

## Use and operation

- Activate the control and confirm the prompt to move one z-level in the
  indicated direction. After each successful trip, the next direction reverses.
- The trip is rejected if the rail outline or control placement is invalid,
  there is no linked z-level in that direction, destination tiles are not empty,
  a non-mob entity blocks the destination footprint, or another grid is on the
  platform.
- Any mob standing under the arriving footprint on the destination level is
  gibbed when the elevator arrives. Keep the shaft clear while the platform is
  moving; this is a lethal hazard, not a safety interlock.
- Platform tiles are transferred to the corresponding coordinates on the
  destination level. The source footprint is cleared. Keep the destination
  footprint empty on every level the elevator serves.
- The platform footprint's bounding rectangle is limited to 1,024 tiles.
- The control's `CMUElevatorWeightLimit` component sets the maximum number of
  entities the platform can carry. Its default `maxEntities` is 20. The elevator
  control and its rails do not count toward this limit. If a move is attempted
  while the load exceeds the limit, the elevator breaks, stays in place, and
  must be re-enabled by an engineer. It will not gib destination-level mobs
  when a trip is refused for excess load.

## Disable or re-enable

An engineer with the configured engineering skill level can select the
elevator's **Disable elevator** or **Enable elevator** alternative verb and
confirm the prompt. The skill and minimum are configurable on the
`CMUElevator` component with `disableSkill` and `disableSkillLevel`; the default
is `RMCSkillEngineer` level 3. The requirement is checked both when showing
the verb and when applying the confirmation. Disabling prevents travel but
does not move or dismantle the lift.

## Prototype references

- `CMUElevatorControl` and `CMUElevatorRail` are defined in
  `Resources/Prototypes/CMU14/Entities/Structures/elevators.yml`.
- The component defaults and operation are implemented in
  `Shared/Elevators/CMUElevatorComponents.cs` and
  `Server/Elevators/CMUElevatorSystem.cs`.

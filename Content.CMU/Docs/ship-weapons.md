# Ship orbital support weapons

Spawn `CMUShipWeaponsTerminal`, `CMUShipWeaponsTerminalGovfor`, or
`CMUShipWeaponsTerminalOpfor` in a ship's command area. This feature does not
change existing maps. The generic terminal accepts RMC, GOVFOR, or OPFOR command
access; the faction variants accept only their respective command access.

Command selects exactly two different weapons and confirms the permanent
selection. Selection is available until confirmed, not restricted to a timed
round-start window. Each weapon has three uses for the entire operation.
Depletion, closing the UI, and replacing or adding terminals cannot reset the
loadout.

Loadouts, ammunition, and the 60-second support-battery cooldown belong to the
ship entity: its Z-level network for multi-deck ships, otherwise its grid.
Separate instances of the same ship template are independent, even when they
belong to the same faction. Multiple non-Z ships on the same map remain
independent. All decks of a multi-deck ship must belong to its own Z-level
network.

## Calling strikes

The existing overwatch orbital bombardment tab has a weapon selector once the
ship has armed its loadout. Enter the usual ground coordinates, select an armed
weapon, and use the existing fire confirmation. Walking Barrage also offers
north, east, south, and west directions.

The standard orbital cannon remains a separate option with its existing
warheads, loading, fuel, and cooldown. Support weapons do not require a physical
cannon or consume its ammunition.

Accepted support strikes consume one use immediately and arrive after 24
seconds. Nearby ground troops receive launch and final-approach warnings;
the selected squad receives a warning too. Invalid, protected, or underground
targets, insufficient access, depleted weapons, and cooldown failures do not
consume ammunition. Each shell rechecks the current impact column, so later
protection changes can intercept an already launched strike without refunding
its ammunition.

## Weapon behavior

| Weapon | Effect |
| --- | --- |
| Gatling Barrage | 36 rapid small impacts scattered within 5 tiles. |
| Walking Barrage | 7 HE shells progress along a 16-tile line centered on the target in the selected direction. |
| 120mm HE Barrage | 12 medium HE impacts with 6-tile scatter. |
| 380mm HE Barrage | 6 slower, larger HE impacts with 6-tile scatter. |
| Gas Strike | A 5-tile-radius toxic cloud inflicts poison damage for 30 seconds; filters and sealed internals provide their normal protection. |
| EMS Strike | A 5-tile-radius field applies nonlethal stuns and severe movement slowdown for 20 seconds, respecting normal status immunities. |
| Smoke Strike | A non-damaging, opaque 6-tile-radius screen lasts 30 seconds. |
| Airburst Strike | 3 anti-personnel bursts with 3-tile scatter, without terrain breaking. |
| Precision Strike | One concentrated HE impact at the exact coordinates, without scatter. |

Definitions are in
[ship_weapons.yml](../Resources/Prototypes/CMU14/Entities/Structures/Machines/ship_weapons.yml).
The full pattern plus its individual blast radii is at most 12 tiles from the
requested target, smaller than the standard explosive OB's configured radius.
Smoke and gas do not spread beyond their initial footprint. Individual cloud
tiles respect orbital protection and require non-space ground.

## Regression coverage

[CMUShipWeaponsTest](../IntegrationTests/ShipWeapons/CMUShipWeaponsTest.cs)
checks ship-instance and deck isolation, command access, malformed selections,
permanent locking after terminal replacement, shared ammunition and cooldown,
rejected targeting, three-use exhaustion, standard-cannon selection, all nine
footprints, walking direction, and bounded support clouds.

Run:

```powershell
dotnet test Content.IntegrationTests\Content.IntegrationTests.csproj --filter FullyQualifiedName~CMUShipWeaponsTest
```

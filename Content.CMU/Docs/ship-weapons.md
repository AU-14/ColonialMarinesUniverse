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
warheads, loading, fuel, and cooldown. Support weapons never consume the
standard cannon's ammunition.

## Physical batteries and ammunition

`cmu.ship_weapons.require_guns` defaults to `false`. In that mode, arming and
firing retain gun-free behavior. Setting it to `true` requires an anchored
compatible battery on the same ship to arm a weapon, and a matching loaded
mission round to fire it. A battery need not be loaded during arming.
Changing the CVar or removing a battery never unlocks a confirmed loadout.

These spawnable batteries inherit the `RMCAntiAirPropFlipped` sprite:

| Battery prototype | Supported missions |
| --- | --- |
| `CMUShipWeaponGunHeavy` | 120mm HE, 380mm HE, Airburst |
| `CMUShipWeaponGunSupport` | Gas, EMS, Smoke |
| `CMUShipWeaponGunRapid` | Gatling, Walking, Precision |
| `CMUShipWeaponGunRailcannon` | Railcannon |

Map any number of batteries; no existing maps are changed. The component's
`weapons` list can customize a battery's supported missions. Batteries and
ammunition cannot be borrowed across separate ship instances, including
identical GOVFOR and OPFOR ships. Batteries on another deck of the same
Z-level network do count.

Players load matching `CMUShipWeaponRound...` items by interacting with a
battery while holding a round. Each battery holds one round; alt-click or
the eject verb unloads it. Incompatible ammunition is rejected. One round
supplies the **entire fire mission**, including every shot in a barrage.
It is consumed only when a launch is accepted, alongside one of the weapon's
three uses. Unloaded batteries or batteries loaded with another supported
weapon's ammunition cannot fire the selected mission.

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
| Gas Strike | A 30-tile-radius toxic cloud inflicts poison damage for 30 seconds, but never damages xenos; filters and sealed internals provide their normal protection. |
| EMS Strike | A 30-tile-radius field lasts 15 seconds and forcibly stuns everyone, including xenos and ordinarily stun-immune targets. Initial and late entrants remain stunned until 25 seconds after the field ends; leaving early does not shorten the stun. |
| Smoke Strike | A non-damaging, opaque 30-tile-radius screen lasts 30 seconds. |
| Airburst Strike | 3 anti-personnel bursts with 3-tile scatter, without terrain breaking. |
| Precision Strike | One concentrated HE impact at the exact coordinates, without scatter. |
| Railcannon Strike | Devastating area trauma (base 2,000 piercing and 2,000 structural, bypassing resistances) confined to a 5x5 square of tiles centered on the target tile. Uses the existing whole-body blast-injury handling without a radial explosion or damage outside the square. |

Definitions are in
[ship_weapons.yml](../Resources/Prototypes/CMU14/Entities/Structures/Machines/ship_weapons.yml).
Damaging barrage patterns plus their individual blast radii remain at most 12
tiles from the requested target, smaller than the standard explosive OB's
configured radius. The three support clouds intentionally use the expanded
30-tile radius. Railcannon affects entities whose origin lies in the exact
5x5 tile square; every target also respects orbital protection.
Smoke and gas do not spread beyond their initial footprint. Individual cloud
tiles respect orbital protection and require non-space ground.

## Regression coverage

[CMUShipWeaponsTest](../IntegrationTests/ShipWeapons/CMUShipWeaponsTest.cs)
checks ship-instance and deck isolation, command access, malformed selections,
permanent locking after terminal replacement, shared ammunition and cooldown,
rejected targeting, three-use exhaustion, standard-cannon selection, ten
profiles, walking direction, bounded support clouds, battery compatibility,
manual loading, CVar modes, Railcannon boundaries, xeno gas immunity, and EMS
entry and recovery timing.

Run:

```powershell
dotnet test Content.IntegrationTests\Content.IntegrationTests.csproj --filter FullyQualifiedName~CMUShipWeaponsTest
```

# Ship orbital support weapons

Spawn `CMUShipWeaponsTerminal`, `CMUShipWeaponsTerminalGovfor`, or
`CMUShipWeaponsTerminalOpfor` in a ship's command area. This feature does not
change existing maps. The generic terminal accepts RMC, GOVFOR, or OPFOR command
access; the faction variants accept only their respective command access.

The terminal component's YAML `maxSelections` field sets the maximum number of
different weapons command can select (default: `3`). `uses` sets the number of
fire missions shared by every selected weapon (default: `2`):

```yaml
- type: CMUShipWeaponsTerminal
  maxSelections: 3
  uses: 2
```

The UI displays this terminal's limits, and the server enforces them. Set
positive integers; at least one weapon must be selected. Terminals on the same
ship should use the same values. The first confirmed loadout locks the ship and
copies that terminal's `uses` into ship state; later edits or other terminals
cannot change or reset the pool.

Command selects up to the displayed limit and confirms the permanent
selection. Selection is available until confirmed, not restricted to a timed
round-start window. All selected weapons draw from one shared pool of fire
missions for the entire operation (for example, two Gatling Barrages, or one
Gatling Barrage and one Gas Strike, exhaust the default pool).
Depletion, closing the UI, and replacing or adding terminals cannot reset the
loadout. Confirming fewer than the limit also locks the empty slots permanently.
Inspecting any selection terminal offers the CMU **Ship Orbital Support Weapons**
guide, also available from the CMU guidebook root.

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
It is consumed only when a launch is accepted, alongside one of the ship's
shared fire missions. Unloaded batteries or batteries loaded with another supported
weapon's ammunition cannot fire the selected mission.

Accepted support strikes consume one shared mission immediately and arrive after 24
seconds. Nearby ground troops receive a launch warning, one directional warning
three seconds later pointing from each recipient toward the target (or warning
that their position is targeted), and a final-approach warning;
the selected squad receives a warning too. Invalid, protected, or underground
targets, insufficient access, depleted weapons, and cooldown failures do not
consume ammunition. Each shell rechecks the current impact column, so later
protection changes can intercept an already launched strike without refunding
its ammunition.

## Weapon behavior

| Weapon | Effect |
| --- | --- |
| Gatling Barrage | 120 impacts, 0.15 seconds apart, with 16-tile scatter and a 1.5-tile blast radius. |
| Walking Barrage | 9 HE shells, one second apart, progress along a 16-tile line centered on the target in the selected direction. Each has a 6-tile blast radius. |
| 120mm HE Barrage | 12 HE impacts, 0.75 seconds apart, with 16-tile scatter and a 12-tile blast radius. |
| 380mm HE Barrage | 6 heavy impacts, 1.8 seconds apart, with 6-tile scatter and a 16-tile blast radius. |
| Gas Strike | A 30-tile-radius toxic cloud inflicts poison damage for 30 seconds on everyone, including xenos; filters and sealed internals provide their normal protection. |
| EMS Strike | A 30-tile-radius field lasts 15 seconds and forcibly stuns everyone, including xenos and ordinarily stun-immune targets. Initial and late entrants remain stunned until 25 seconds after the field ends; leaving early does not shorten the stun. |
| Smoke Strike | A non-damaging, opaque 30-tile-radius screen lasts 30 seconds. |
| Airburst Strike | 6 anti-personnel bursts, 0.8 seconds apart, with 8-tile scatter and a 12-tile blast radius, without terrain breaking. |
| Precision Strike | One 120mm-strength HE impact at the exact coordinates, with a 12-tile blast radius and no scatter. |
| Railcannon Strike | Devastating area trauma (base 2,000 piercing and 2,000 structural, bypassing resistances) in a 5x5 square on every Z-level of the target's network. Uses the existing whole-body blast-injury handling without a radial explosion or damage outside the square. |

Definitions are in
[ship_weapons.yml](../Resources/Prototypes/CMU14/Entities/Structures/Machines/ship_weapons.yml).
The expanded damaging patterns intentionally no longer have the original
12-tile footprint cap. Scatter plus blast reaches 28 tiles for 120mm, 22 for
380mm, 20 for Airburst, and 17.5 for Gatling. Walking reaches 14 tiles along its
axis; Precision has a 12-tile radius. The three support clouds use the expanded
30-tile radius. Railcannon affects entities whose origin lies in the exact
5x5 tile square on each live network map; every target also respects orbital
protection. This includes levels above and below the resolved impact, but never
other Z-networks. Non-Z maps receive only their own square.
Smoke and gas do not spread beyond their initial footprint. Individual cloud
tiles respect orbital protection and require non-space ground.

## Regression coverage

[CMUShipWeaponsTest](../IntegrationTests/ShipWeapons/CMUShipWeaponsTest.cs)
checks ship-instance and deck isolation, command access, malformed selections,
permanent locking after terminal replacement, shared ammunition and cooldown,
rejected targeting, shared-pool exhaustion across weapons, standard-cannon selection, ten
profiles, walking direction, bounded support clouds, battery compatibility,
manual loading, CVar modes, Railcannon boundaries, xeno gas damage, and EMS
entry and recovery timing. It also verifies one-to-three permanent selections,
YAML-configured selection limits and shared-use pools,
empty/oversized loadout rejection, the terminal's guidebook link and document
parsing, directional-warning timing, and Railcannon damage across separate
Z-level maps with network isolation and protected-area checks.

Run:

```powershell
dotnet test Content.IntegrationTests\Content.IntegrationTests.csproj --filter FullyQualifiedName~CMUShipWeaponsTest
```

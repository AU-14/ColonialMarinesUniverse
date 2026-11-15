# Power guide

How power works on CMU maps: the two systems, which one a map runs, and how to
build for each. Atmospherics has its own guide: [ATMOS.md](../ATMOS/ATMOS.md).

Terms: **tile power** — a map running the wizden cable network. **Area power** — the
RMC system (reactor + APCs, no cables), the default. "The grid" when referenced
below means the cable network, **not** the map grid entity.

## The two systems never mix on one map

Which system drives a device is decided at runtime by the map's power flag
(`CMUMapUsesTilePower` on the map entity), never per room and never per device.
Lights, doors, screens, and vendors **carry both power components at the prototype
level and are dual-mode**; devices need no map edits when a map converts (only RMC
*furniture* does.)

## Area power (the default, **pre-rework**)

A fusion or geothermal reactor feeds a **map-wide power group** — every APC on the
map shares its output, no cables involved. One `CMApc` powers all receivers in its
area per channel (Equipment / Lighting / Environment); devices carry
`RMCPowerReceiver` and need no wiring. Blackout happens only when the reactor dies.
MultiZ by nature, one single geothermal reactor can power the entire ZNetwork.
The SMES and stray cables on current maps remain decorative.

Recipe for new maps:

- Paint areas first: place `RMCArea*` area entities (parent `RMCAreaBase`; examples
  in `Resources/Prototypes/_RMC14/Entities/Areas/`; new area protos go in the CMU
  zone), then run `areas save` (host flag) to bake them into the grid. `areas load`
  re-materializes them for editing; `areas cleanup` strips ones left on space tiles.
  Needs a user-friendly-oriented rework pass. Point, click (overlay/display), save.
- One wall-mounted `CMApc` **per area**. Every variant spawns with a charged
  `RMCPowerCellHigh` (`CMApcNoPower` is the deliberate empty one). The Battery
  numbers on `CMApc*Capacity` are never read by area power — the cell in the APC slot
  is the only battery that matters. Do not size rooms around them for legacy power.
- One generator per power group. Planets use the CMU dual-mode generators
  (`CMUGeneratorFusion`, `CMUGeneratorThermo`); both roll damage at map init
  (50 % weld / 35 % wire / 15 % wrench) and need repairs before they feed anything —
  that is the planet round arc: colonists or responders secure and repair generation
  or the lights die. Upstream `RMCGeneratorFusion` is the roundstart-working variant
  and stays ship-side.
- Generation splits evenly across every APC in the group, drawing or not. Many small
  rooms means many APCs and slower charging each, so size generation to the APC count.
- Multi-z ships: the power group is the z-network owner, else the map. Reactors only
  feed z-levels networked together; an APC on a level outside the network needs its
  own reactor on that map.
- Cell charge stages the channels: above 33 % everything runs, below 16 % equipment
  is gone, below 1 % only environment remains, then all off. That is the "consoles
  on, lights out" blackout shape. With the group dead, a charged APC cell drains in
  minutes to tens of minutes at authored loads — the repair clock is short, and
  portable generators are the stopgap.
- Bare wizden devices (ordinary `ApcPowerReceiver`) are auto-adopted into area power
  on the Environment channel, with their `powerLoad` as the draw. New custom devices
  need no dual components.

Diagnostics:

- A device on a tile with no painted area never registers and stays dark, silently.
- The APC renames itself "\<Area\> APC" at map init; an APC still named "area power
  controller" means its area link failed.
- Every APC red / not charging with all areas dark means no working reactor in the
  group (portable generators only feed their own area).

**Distress Signal planets keep this system.** The maps under `Maps/Planets/` run RMC
area power unchanged: its simplicity is their round arc — reactor up or blackout,
nothing to maintain. Do not add wizden grid pieces to DS planets, it isn't intended.

## Tile power (the opt-in, pre-rework)

The chain, with the actual **prototype IDs**:

```
generation  ->  CableHV  ->  SMES  ->  SubstationBasic (HV->MV)  ->  CableMV  ->  APC  ->    devices
(pacman/AME/    (spine)      (buffer)  (per deck / z-level)          (per deck)   (MV tap,   (wireless, <=3 tiles (C#, fixed),
 TEG/solar)                                                                        battery)   or via LV cable runs)
```

Mapper rules for tile-power maps:

- One substation per deck, fed by an HV spine through maintenance. HV never enters
  habitable rooms (except for warships.)
- MV cable runs from the substation along corridors; one APC per room or section,
  mounted and loaded with a cell (the APC is the LV source and the blackout buffer).
- Devices link to an APC wirelessly within **3 tiles** (provider/receiver range).
  Anything farther needs an LV cable run (`CableApcExtension`) to carry the provider.
- Size SMES banks for the intended outage gameplay: with generation down, SMES and
  APC cells keep lights and vendors alive. That buffer time is the engineers' loop —
  refuel or repair generation, recharge storage, **run a temporary generator on
  extension cables**. Base APCs have a sealed internal battery: no cell-swap loop; the
  loop is cables, generation, and APC rebuild (crowbar → frame + electronics.)
- Generation per map type: ship-side → reactor + portable generators + Solar.
- Cut cables must be repairable: keep cable runs reachable from walkable tiles, not
  under walls.

## Generators

- **"Dragon" DR-30** (`CMUGeneratorPortableDragon`) — the vendable portable for
  tile-power maps; runs on phoron sheets (`CMSheetPhoron`, ASRS phoron crate), with a
  matching fuel canister (`CMUCanisterPhoron`). Its fuel bay takes only the sheets
  that fit.
- **"Orochi" OR-30** (`CMUGeneratorPortableOrochi`) — the area-power backup portable;
  it feeds its own area only.
- **`CMUGeneratorFusion` / `CMUGeneratorThermo`** — the dual-mode stationary pair:
  the map's power flag picks their live channel at runtime. On a tile-power map,
  anchor them on an HV cable tile and the output joins the run; the supplier only
  comes online with the reactor repaired to working order — a broken generator can
  never feed the grid.

## Cross-deck power

`CMUPowerRiserUp/Down` (PR2) carries power across a deck gap on tile-power maps.
HV only, 30 kW: anchor both halves on HV cable tiles, same tile, one deck apart.
Screwdriver toggles role (source/sink); crowbar flips direction. The pair cannot
self-oscillate by design.

For lighting a remote deck, chain it:
> power riser → substation (HV→MV) → APC → LV -> lamps (or electronics).

The **ATS-1 automatic transfer switch** (`CMUAutoTransferSwitch`) is the emergency
backup for tile-power maps: port side wired from a cable terminal, output side into
the emergency HV run. It charges from the mains, and after they die it cranks for
roughly eight seconds before taking the load.

## Converting a map to tile power

- Scope: whole maps outside the DS planet family, never rooms. Conversion is one
  component on the map entity (`- type: CMUMapUsesTilePower`) plus the furniture
  edits below — devices are never edited.
- On a converted map remove all of: `RMCGeneratorFusion`, `CMApc*`, stray RMC SMES
  and cables; lay the real grid per the chain above.
- Never add the tile-power flag without doing the full conversion in the same
  change: a tile-power map with no grid means every `needsPower` device is
  permanently dark.

## Traps

- `RMCCableBlue/Red/Yellow` are decorative only now: pure visuals, no power, hidden
  from spawn menus. For thin-looking runs that actually carry power use
  `CMUCableThinLV/MV/HV`; for plain device runs base `CableApcExtension`.
- Runtime spawns inherit the map's system: a wizden APC spawned mid-round on an
  area-power map arrives with its cable net inert, and RMC furniture sits inert on
  tile-power maps. There is no per-grid opt-out; the flag is per map.
- On an area-power map, an unpowered console wants a `CMApc` in its mapped area, not
  cable runs — the grid cannot help it.

## Admin overrides (VV)

Both systems gate every consumer through the same component (`ApcPowerReceiver`), so
admin overrides are single fields on the device itself:

- **Always powered:** set `NeedsPower` to `false` on the device's `ApcPowerReceiver`.
  The power tick skips it and both server and client treat it as on, regardless of
  reactor, APC, or grid state. This is the same bypass some prototypes ship with:
  dropship/airlock doors, vendors, cameras, fax and requisition machines carry
  `needsPower: false`. Set it back to `true` to undo; the next power tick
  reconverges the device.
- **Force a device off:** set `PowerDisabled` to `true` on the same component. Works
  on both systems; it is the field a cut wire sets, so a repaired wire clears it.
- **Power a whole area** (area-power maps): VV the painted area entity (`RMCArea*`,
  component `Area`) and set `AlwaysPowered` to `true`. This short-circuits the APC
  check and powers every receiver in the area on all channels. Dropship interiors do
  exactly this while intact. `false` undoes it.

Notes:

- VV edits are live-only and server-side: gone on entity delete or round end. A
  permanent bypass belongs in the prototype (`needsPower: false`), not in VV.
- Consumers that read area state directly (`IsAreaPowered`) ignore the per-device
  bypass; for those, the area `AlwaysPowered` toggle is the override that works.
- The overrides silence power gating only. Fuel, cells, and repair state still gate
  behavior, and suppliers (reactors, portable generators) carry no
  `ApcPowerReceiver` at all.

## Pointers

- CMU generator and device protos: `Content.CMU/Resources/Prototypes/CMU14/Entities/Structures/Power/`
- Atmos side, including the cross-deck hardware family: [ATMOS.md](../ATMOS/ATMOS.md)

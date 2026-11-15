# Atmospherics guide

How air works on CMU maps, what mappers place, and the devices that bridge decks.
Power has its own guide: [POWER.md](../POWER/POWER.md).

## The atmosphere model

Every z-level of a CMU map is its own grid and runs per-tile atmos. Two components
carry the system:

- `MapAtmosphere` (map entity, **mandatory on every map file**): the map's declared
  world air. It feeds only tiles flagged as outside (`isSpace` tiles and empty grid
  positions); on planet maps (`space: False`) roofless tiles also relax toward it.
  Immutable: it never depletes, moves, or costs sim time.
- `GridAtmosphere` + `GasTileOverlay` (the grid): real per-tile simulation. Spacing,
  canisters, fires, and scrubbers live here.

Rooms vent into the world air one-way: the declared mixture is protected from sim
writes, and gas dumped into it is gone. **Gas never transfers between z-levels** —
each deck's air is fully its own; a deck-3 breach cannot touch deck 1.

### Where a tile gets its air

1. **Authored air** saved in the map file (`GridAtmosphere` `uniqueMixes`). Roundstarts
   exactly as saved.
2. **The declared world air** — outside tiles only. Interiors never read it.
3. **Vacuum** (0 moles, 2.7 K) — any interior tile with no authored air. There is no
   seeding: `MapAtmosphere` is not a fallback for rooms.

An unbaked interior is a vacuum deck. Baking is mandatory on every level.

### World air inheritance

Authoring `MapAtmosphere` on the base (lowest) deck only is enough: at load and on
every network change, the lowest deck that declares one fills every member that ships
none. A deck with its own declaration keeps its air — author the base once for the
easy scheme, author more decks only for deliberate variation.

### Planet air: sky relaxation and day-night

On planet maps, roofless tiles slowly relax toward the declared world air: pollution,
heat, and overpressure are carried off, deficits refilled. Coverage is the roof data
(the same source weather uses), so roofed rooms never normalize. Pace it live with
`cmu.atmos.outdoor_relax` and `cmu.atmos.outdoor_relax_rate`. Authored outdoor air
should match `MapAtmosphere`, **or the first minutes of the round will drift there**.

`CMUDayNightAtmosphere` goes once per z-network, on the map entity carrying the
canonical `MapAtmosphere` block. One clock fans temperature across the network while
each deck keeps its authored moles as its own baseline. Kill it live with
`cmu.atmos.day_night_temp`.

## Baking a map

On a local dev server, standing on the map (host admin flag):

```
addcomp <grid> GridAtmosphereComponent   # only if missing
addcomp <grid> GasTileOverlayComponent   # rebuild needs it; GridAtmos alone throws sadly
fixgridatmos <grid>                      # fills every tile with standard 101 kPa air (breathable)
```

- Run the fill on **every z-level**; each deck is a separate grid.
  `fixgridatmos <grid1> <grid2> ...` takes several at once. Skip a level and that
  deck ships vacuum.
- Save with `savemap <filename>` (console, standing on the grid) — it serializes live
  state, so the fill lands in the yaml as `uniqueMixes`. Multi-level maps:
  `znetwork-save <name>` saves every deck of the network in one pass, each under its
  original filename.
- `znetwork-bakesave <name>` is fill + save in one command; use it after tile edits.
  The **fill resets every tile to standard air** + `AtmosFixMarker`s, so hand-authored
  `uniqueMixes` (phoron rooms, off-standard mixes) are overwritten — finish those by
  hand and save with plain `znetwork-save`. A grid that cannot bake (missing
  components) aborts the run before its level is written; earlier decks stay saved,
  so fix the grid and re-run (idempotent).
- Saves land in the dev server's user data dir (`bin/Content.Server/data/`), never
  the repo tree. Copy the files back to the map's repo path; filenames already match,
  so the copy is flat. `ztp <up|down|offset>` hops between decks while working.

### AtmosFixMarker

Per-tile air override (spawn menu, "Atmos Fix ..."). During the fill, a tile carrying
a marker gets that marker's mix instead of standard air. Per-tile, not per-room: tile
a whole special room (freezer, nitrogen) before the fill, or place a few and let
the sim equalize after load. The fill does not delete markers — remove them before
saving or they ship as invisible anchored entities.

| Marker | Mix |
|---|---|
| `AtmosFixAirMarker` | O₂ 21 % / N₂ 79 % @ gas-miner pressure, 20 °C |
| `AtmosFixBlockerMarker` | hard vacuum, 20 °C |
| `AtmosFixOxygenMarker` | pure O₂ @ gas-miner pressure, 20 °C |
| `AtmosFixNitrogenMarker` | pure N₂ @ gas-miner pressure, 20 °C |
| `AtmosFixPlasmaMarker` | pure plasma @ gas-miner pressure, 20 °C |
| `AtmosFixInstantPlasmaFireMarker` | plasmafire, 5000 K — round starts on fire |
| `AtmosFixFreezerMarker` | air @ 235 K (walk-in freezer) |
| `AtmosFixVoxMarker` | pure N₂ @ 101 kPa, 20 °C (upstream) |

### Traps

- A `GridAtmosphere` with no baked air births every interior tile as vacuum, with or
  without `MapAtmosphere` on the map. The serializer drops airless tiles: a save
  without the fill ships an empty data block, which reads as vacuum decks every round.
- New rooms are covered by nothing: a room added after the bake ships vacuum until
  the fill runs again.
- Only `isSpace` tiles or empty grid positions count as outside. Ordinary floor
  outside the hull is a room; if terrain should breathe world air, that is a
  tile-definition decision, not a wall decision.
- `fixgridatmos` without `GasTileOverlayComponent` on the grid throws. Add both.
  Overlay is the visual effect you see per tile, like Green for Tritium.
- A missing `MapAtmosphere` on the map entity still means vacuum world air.
- Shuttles and dropships need their own `GridAtmosphere` + baked air: authored tile
  air rides with the grid, so the interior stays breathable in flight. Without it a
  shuttle is Space vacuum standalone.

## Map family declarations

| Map family | `space:` | declared mixture | reads as |
|---|---|---|---|
| Colony / planet / outpost | `False` | breathable or thin world air | the outside has the planet's atmosphere |
| Warship / shuttle in flight | `True` | zero-mole vacuum | the void is the void; hull breaches vent for real |

Planet world air, breathable (~101 kPa, inside plant tolerance 283-303 K / 81-121 kPa):

```yaml
- type: MapAtmosphere
  space: False
  mixture:
    volume: 2500
    immutable: True
    temperature: 293.2
    moles:
    - 21.824879   # Oxygen
    - 82.10312    # Nitrogen
    - 0
    - 0
    - 0
    - 0
    - 0
    - 0
    - 0
    - 0
    - 0
    - 0
```

Space vacuum 2.7 K:

```yaml
- type: MapAtmosphere
  space: True
  mixture:
    volume: 2500
    immutable: True
    temperature: 2.725
    moles:
    - 0 # Oxygen
    - 0 # Nitrogen
    - 0 # Carbon Dioxide
    - 0 # Plasma
    - 0 # Tritium
    - 0 # Water Vapor
    - 0 # Ammonia
    - 0 # Nitroux Oxide
    - 0 # Frezon
    - 0 # Phoron
    - 0 # unused
    - 0 # unused
```

Botany note: if the map has hydroponics, interior rooms must hold baked breathable
air (`fixgridatmos`) — with real sim there is no per-room override, a room's air is what its tiles
hold.

## Pipes and layers

Every CMU pipe and pipe device places in three layers: aim at a tile corner to pick
Primary/Secondary/Tertiary, screwdriver cycles a placed pipe's layer. Three lines can
cross one tile without connecting — **layers are independent by design**. Layered variants
are picked automatically on placement and are hidden from the spawn menu. The legacy
RMC pipe is hidden and stays loadable for upstream maps and hijack warship explosions.

## Keeping rooms alive

- **LS-1 life support vent** (`CMULifeSupportVent`) — a flush floor vent that keeps
  its compartment at a fixed oxygen-nitrogen mix and pressure, **breathing in fresh air
  and drawing out excess and waste gases**. The set-and-forget option for sealed rooms
  that must simply stay breathable. Fire integration with dropship shuttle consoles.
- **EX-1 exhaust vent** — one-directional waste dump: deletes inlet line gas into
  open sky, faster than a passive vent because the tile and LINDA never sit in the
  path. Runs only on a sky-exposed tile (under a roof it refuses to discharge).
  Screwdriver toggles it; the inlet floor idles below ~20 kPa so it **never strips a
  line to vacuum**. Emergency dump for overloaded waste lines, not distribution.

## Breathing maps

By default, humans on CMU maps do not breathe — atmospherics is a station problem,
not a personal one. A map that should be personally dangerous (a planet with hostile
air, like Bosenmori's nitrogen atmosphere) opts into **breathing**: while a human is
on that map, or anywhere else in its z-network, the map's air and their internals
matter. Arrivals get a respirator automatically; leaving the map takes it away again.
Ghost role call-ins on breathing maps come equipped with a gas mask and a full tank.
Barotrauma is part of the set — depressurized areas hurt, not just suffocate.

A debug console command toggles the whole life support set (respiration, barotrauma,
heat damage, crit asphyxiation) for testing: `help cmulifesupport` shell command.

## Borrowed atmosphere

Airless grids breathe what is under them. A dropship's lower deck or a lowered ramp
carries no air of its own: anything on it breathes the air of the grid it is over —
a ship's hangar, a colony's landing pad — and the map's atmosphere where there is
none. Land in a vented hangar and the deck stays breathable; land on open terrain and
it breathes the planet.

## Wide doors and airtight seals

Airtightness in the engine covers only a tile's anchored tile. A door that spans
several tiles leaks at the tiles its door entity does not cover. `CMUAirtightSeal`
is an invisible structural membrane that closes those gaps — place it on the tiles a
wide door or hatch leaves uncovered. `CMUAirtightSealSpace` is the same idea as an
invisible heavy hull block for space-facing edges.

## Stationary hardware

The full device line ships as CMU twins of the wizden hardware: upstream pump and
scrubber logic, cm-ss13 sprites, dual-mode power (auto-adopted on area-power maps,
wired provider or LV run on tile-power maps). Same three-layer placement as the
pipes; a device's fourway node connects to any adjacent run on its own layer. Placed
vents and scrubbers register to air alarms automatically.

Room hardware:

- **`CMVentPump` (AV-1 air vent)** — the floor grate that feeds or drains a room.
  Direction and target pressure are set from the air alarm. 200 W.
- **`CMVentScrubber` (AS-1 air scrubber)** — pulls room gas into the waste line.
  Alarm modes: scrub (filter), siphon (drain toward vacuum), wide (neighbors too).
  1000 W.
- **`CMUPassiveVent` (PV-0)** — unpowered grate that passes gas by pressure
  difference.
- **`CMUOutletInjector` (AI-1)** — one-way outlet that pushes pipe gas into the tile.

Flow control, same placement rules: `CMUGasPressurePump` (GP-1), `CMUGasVolumePump`
(GV-2), `CMUGasPassiveGate` (PG-1), `CMUGasValve` (MV-1), `CMUSignalControlledValve`
(V-2), `CMUPressureControlledValve` (PV-3), `CMUGasPressureRegulator` (PR-3),
`CMUGasPort` (CP-1, canister connection), `CMUGasDualPortVentPump` (DV-3),
`CMUHeatExchanger` / `CMUHeatExchangerBend` (HX-1 / HX-1B), `CMUGasFilter` /
`CMUGasFilterFlipped` (GF-4 / GF-4F), `CMUGasMixer` / `CMUGasMixerFlipped`
(GM-4 / GM-4F), `CMUGasPipeSensor` (PS-1), `CMUGasPipeManifold` (PM-1).

## Other placeable content

- **Phoron is a real gas.** Plasma's refined tier, made by players (hot N2 fixation),
  not by maps — but `uniqueMixes` can author it: mole lists are gas-enum ordered and
  phoron is the last (10th) entry (see **Map family declarations** above).
  Scrubbers filter it, it burns (way) hotter than plasma, it is toxic.
- **`CMUGasCrystallizer`** — gas-to-solid machine (phoron → fuel sheets,
  ammonia+CO2 → plastic). Standing machine, pipe connection south (rotatable), wants
  1500 W, works on both power families.
- **Gas miners work planetside** — they only reject space-flagged tiles, and CMU
  exteriors are unflagged. A sealed miner room is legitimate colony air
  infrastructure, it's magic.
- **Air alarms, fire alarms, firelocks, `AtmosAlertsComputer`**
- **Sealing against depressurization:** firelock entities at bulkhead doorways (the
  wizden ship pattern), **one fire/heat firelock per section boundary**, and air alarms
  with pressure thresholds actually set. The **unset alarm default is why vacuum
  shutdowns do not happen on unmapped decks**. One alerts console per engineering wing.
  Test by punching a hull hole in build mode: alarms trip, firelocks hold.
- **`CMUAnchorIndestructible`** — a 1x1 indestructible, unpowered station anchor for
  map-structure work.

## Cross-deck hardware

Paired riser/vent devices bridge decks. Common rules: the two halves sit on the same tile,
one deck apart; crowbar flips a half's direction (Up/Down); multitool opens the service
panel and pry deconstructs; direct-build from 5 steel.

- **`CMUPipeRiserUp/Down` (PR3)** — a fourway pipe bridge: both halves join their
  decks' pipe nets into one, gas flows freely across the deck gap, and vent-crawlers
  can pass through (todo: xeno larvae). Pipe layers are locked on risers. The link is passive and
  symmetric — a riser imposes no flow or pressure behavior of its own. One-way
  routing is the pump's job, exactly as on a single deck: a volumetric or pressure
  pump feeding a riser routes waste to the burn chamber or space and life support
  onward.
- **`CMUOpeningVentUp/Down` (OV1)** — passive pressure equalizer for stairwells and
  deck holes: no pump, no power, gas moves by pressure difference, capped slow per
  tick. Screwdriver opens/closes the grate; examine shows both decks' kPa.
  (todo: xeno larvae)
- **`CMUPowerRiserUp/Down` (PR2)** and the **ATS-1 transfer switch** — see
  [POWER.md](../POWER/POWER.md), "Cross-deck power".

> - Pipe and device protos: `Content.CMU/Resources/Prototypes/CMU14/Entities/Structures/Atmos/`

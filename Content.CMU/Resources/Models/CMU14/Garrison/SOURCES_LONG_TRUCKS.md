# Garrison five-axle long trucks

Three source-specific draft assemblies / 366 parts map three classic objects and six across the configured maps. The brown industrial, orange Donk and yellow mining bodies retain distinct source panels. Canonical definitions are `garrison_long_trucks.yml` and `garrison_long_truck_art.yml`. All three remain drafts.

## Geometry and original artwork

Each 122-part assembly contains ten cylindrical tires, five axles, separate rim rings and raised hubs, a ladder chassis, actual gaps below the wheel arches, a cab with seats/dashboard, sloped front glazing, mirrors and access steps. The closed cargo body has raised longitudinal roof ribs, reinforced end bands, original markings and red marker lights. Visible source wheel centers are at approximately 9.5, 32.5, 53, 92.5 and 113.5 source pixels.

Thirty original PNGs / 42 crop uses occupy atlas slots 427–456. Long panels are split into adjacent strips of at most 64 pixels; no resampling or palette changes are applied. Roof crops receive recorded quarter-turns. These splits preserve the source detail while retaining 64-pixel atlas cells. The complete 456-image atlas is now 4096 by 512 pixels / 8 MiB RGBA.

Initial comparison exposed reversed lettering on the opposite cargo side. That side now reverses both strip positions and U projection, so the full Donk logo and mining markings read normally from either side. All six complete side reconstructions are checked against their source pixels; the joins are continuous. The reverse-side decoration is an inference because the source supplies only one direction. Cab door images retain their front-to-back arrangement.

## Facing, pivot and map context

All three 128-by-64 source images face west, occupy columns 0–126 and contain one static frame. Canonical front -Y receives a -90-degree yaw correction. The horizontal render offset is 1.484375 tiles: source sprite offset 1.5, minus the two-tile half-frame width, plus occupied-frame center 63.5/32. This preserves source framing rather than centering the long body on the saved entity pivot.

The .125 map-Y render offset follows the midpoint of the source fixture's -.5 to +.75 Y range. Model width and height remain inferred: the outer wheel hubs span 1.214 tiles; the highest end-band roof is 1.399 tiles. Source simulation fixtures and all saved transforms are unchanged. No per-instance correction was required.

| Source prototype | Source state | Classic entity | Saved position | Parts |
| --- | --- | ---: | --- | ---: |
| RMCPropVehicleLongTruckBrown | longtruck_brown | 14363 | 165.14857, -147.41226 | 122 |
| RMCPropVehicleLongTruckDonk | longtruck_donk | 14364 | 165.06711, -153.48619 | 122 |
| RMCPropVehicleLongTruckMining | longtruck_mining | 14365 | 164.97336, -155.20494 | 122 |

All three saved placements have zero conservative part-bound intersections against neighboring exact or inherited modeled objects. Every unrelated scene record is unchanged. Browser inspection covers the brown truck next to the red empty-bed truck and crate, the Donk/mining pair, and their top-down spacing. The yard region exports 98 objects / 225 floors with no omitted entities; its center is 166.5, -151.5 and radius seven tiles.

## Evidence and unfinished work

- `Tools/three_d/generated/long-trucks-source-audit.json`: source states, fixtures, saved counts and attribution.
- `long-trucks-crop-audit.json`, `long-trucks-pixel-audit.json`: 42 byte-exact crop uses.
- `long-trucks-lettering-audit.json`: six complete readable side panels and continuous split joins.
- `long-trucks-placement-audit.json`: transforms, offsets, facings and neighboring-object intersections.
- `review/long-trucks-*`: source/four-view cards, an orbit comparison and actual-map captures.
- `garrison-long-trucks.glb` and its JSON report: assembled vehicle yard.

The 93 isolated native checks pass with all 660 canonical models, including packing and geometry budgets. All 660 individual GLBs, manifests, references and atlas outputs match deterministic regeneration and have zero Khronos errors or warnings. All 29 assembled exports also validate with zero errors or warnings. Further evidence is recorded in `Tools/three_d/STATUS.md`.

Physical depth/height, reverse-side decoration, hidden chassis and rear-door construction remain inferred. Rounded body transitions, detailed tires, suspension, interior equipment, material response, lights, damage and movement are unfinished. These are static scenery props. This pass changes assets only; the normal gameplay viewport remains unchanged and connected gameplay is not validated by these checks.

## Original source license

- Source: `/Textures/_RMC14/Structures/Vehicles/vehicles3.rsi`.
- License: CC-BY-SA-3.0.
- Original attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles

Retain this attribution and `Textures/CMU14/ThreeD/LongTrucks/ATTRIBUTION.md` with exported artwork. Canonical YAML includes the comparison-driven opposite-side correction; do not replay the one-time authoring script over it.

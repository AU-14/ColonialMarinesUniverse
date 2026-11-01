# Garrison HEMETT cargo truck sources

Three static editable drafts cover six classic-map objects and eleven objects across the configured maps. Canonical models and original-art declarations are `garrison_hemtt.yml` and `garrison_hemtt_art.yml`. The bare cargo deck, open cargo bed and canvas-covered bed retain distinct construction. They have 78, 79 and 116 parts respectively, all below the native 128-part asset-preview limit.

New geometric work is CC0-1.0 to the extent separately licensable. Original artwork and derivative source designs retain CC-BY-SA-3.0. Preserve this attribution with exported models.

## Source and reconstruction

RSI: `/Textures/CMU14/Structures/vehicles/militarycargotrucks.rsi`. States `miltruck_1`, `miltruck_2` and `miltruck_3` each contain four 96-by-96 static directional frames. All four directions were inspected. Original attribution:

> Taken from SectorPatrol at https://github.com/Neroid-Sector/SectorPatrol/blob/15c9c18fcd965391c0acfbbbf0496fb711765800/icons/obj/vehicles/miltruck.dmi

The models have four axles, eight capped-cylinder tires, recessed rims and hubs, chassis members, bumpers, lamps, cab bulkhead, mirrors, steps, vents and roof details. Open and bare decks expose their physical interior; the covered variant has raised canvas shoulders with seven connected straps. The continuous sloped windshield replaces an initial stepped reconstruction that appeared as horizontal bars.

Thirty-one source-crop uses share fifteen unique original PNGs in atlas slots 290–304. Crops retain exact RGBA pixels. Separate XZ/XY/YZ projections place original glazing, roof, side, vent and cargo-bed artwork on solids. These are original panel crops, not whole-vehicle billboards. The complete atlas contains 304 images in 4096 by 320 pixels / 5 MiB.

## Facing, pivot and clearance

The source uses directional `noRot` art and a screen offset of `0.5, 0.5`. An explicit `groundOffset: 0.5, 0` preserves its horizontal framing in map axes, independent of vehicle facing. The vertical screen offset contains projected height and is not copied into world Y. Native live placement, browser geometry and assembled GLB exports use the same authored horizontal correction; saved simulation transforms remain unchanged. The inherited `-0.5,-0.5,1.4,0.5` collider does not represent the directional three-tile visual footprint and is not used as the model's dimensions.

All six classic trucks retain saved positions and yaw, including the west-facing loading-bay truck and two north-facing trucks. All other scene records remain unchanged. The wheel assemblies were inset by .06 tile after context review found excessive protrusion into two adjacent strapped cases. The final conservative part-bound check finds no overlaps with nearby modeled objects other than shallow tire contacts with six ground catwalk sections. Those contacts reflect the .015 tire bottom versus .028–.035 grate tops; suspension/terrain contact is unfinished. There are no invented crate offsets. Existing support totals remain 803 placed and 74 without exact support.

## Sloped solid support

`WedgeY` is a closed triangular prism with normalized bounds +/- .5 and solid half-space `z <= y`. Its five flat faces use 18 vertices and eight triangles, with outward winding. CPU picking and the native shader clip the oriented box interval against the sloping plane. Browser rendering and portable GLB export use the same shape. Planar source UVs, mirrored U, cutouts and fractional source alpha are supported. Surface artwork is still rejected on ellipsoids and cylinders.

## Evidence and remaining limits

The source, crop, pixel and placement audits are `generated/hemtt-*-audit.json`. Source/four-view comparison cards and saved-map orbit/top captures are under `generated/review/hemtt-*`. Three regional GLBs cover the truck yard, open-bed parking space and west-facing loading bay. Export reports disclose omitted unmapped objects.

The affected client builds with zero errors. The isolated native harness passes 89 checks; Python passes 109 and Node passes 17. Actual native shader compilation and rendering pass 448 pixel checks in two configurations, including sloped silhouettes and textured faces. The browser wedge fixture passes 24 color and 24 picking samples; existing cylinder and alpha fixtures still pass. Normal connected tests remain unavailable due to unrelated tactical-map server compilation errors recorded in `Tools/three_d/STATUS.md`.

All three models remain drafts. Physical height, wheel track, hidden chassis construction and materials are inferred. Roof shoulders are stepped, wheel rims are simplified, and suspension, steering, damage, cargo changes and animation are unfinished. The source entities are static props; no drivability is added. Connected native scene presentation and performance remain unverified. The normal gameplay viewport is unchanged.

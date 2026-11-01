# Garrison heavy loaders and road-center dividers

Four source-specific loader drafts / 388 parts map five classic objects and nine across the configured maps. Canonical definitions are `garrison_loader_trucks.yml` and `garrison_loader_truck_art.yml`. The existing `CMU3DHybrisaRoadCenterBarrier` in `garrison_environment.yml` also has corrected geometry for 303 classic placements / 404 combined. All remain drafts.

## Source-specific construction

The trucks have four cylindrical tires, separate dark rims/hubs, two axles, a ladder chassis with real wheel gaps, inclined hood and windshield, cab roof hatch, door panels, mirrors and open two-rung access ladders. The first comparison exposed a solid front guard resembling a plow and stretched glass bands. The guard is now an open frame; the sloped windshield uses the original glass color sampled at normalized source pixel 17,31. Hidden glass construction remains inferred.

- The blue loader carries two separately modeled cases with original roof/side panels and raised gold retaining straps.
- The loaded white truck carries a green wrapped load with separate retaining bands.
- The east-facing white truck has a genuinely recessed empty bed, original ribbed floor, raised ribs and inner side walls.
- The security truck has readable source branding on both sides, a recessed roof deck and a separate framed, grid-faced equipment unit with two antenna masts and four green indicators. Its source has two one-second frames; exactly four visible pixels blink. This model represents the first frame only. The equipment's hidden faces and purpose are inferred.

Eighteen original PNGs / 39 crop uses occupy atlas slots 457–474. Crops retain exact RGBA values after the recorded horizontal normalization and roof quarter-turns. The complete atlas contains 474 images in 4096 by 512 pixels / 8 MiB RGBA. The rejected windshield crops are removed from the canonical surface definitions and image folder.

## Facing and placement

Source frames are 128 by 64 pixels. The three west-facing loaded variants occupy 82 columns; the flipped empty truck occupies 81. The security sheet is 256 by 128 because it contains two frames and additional transparent padding, not a larger vehicle.

Canonical front -Y receives -90 degrees for the loaded trucks and +90 for the east-facing empty truck. The padded-image correction is .78125 map-X for the loaded variants and .765625 for the empty variant: `1.5 - 128/64 + occupiedWidth/64`. No map-Y offset is applied; the source vehicle fixture is centered across that axis. All original saved positions and rotations remain unchanged.

Context review found protruding wheel hubs and mirrors contacting prison-wall trim. Moving wheel assemblies inward .04 tiles and reducing mirror reach .048 tiles preserves the one-tile source fixture width. The final truck width, including access ladders and hubs, is .972 tiles. This is an inferred physical depth, not a change to the simulation collider. The final audit finds no conservative part-bound intersections with neighboring exact or inherited modeled objects at any of the five placements.

| Prototype | State | Classic entities | Parts |
| --- | --- | --- | ---: |
| RMCPropVehicleLoaderTruckBlackFilled | armored_truck_wy_black_loaded | 14358, 14359 | 98 |
| RMCPropVehicleLoaderTruckBlueFilled | armored_truck_blue_loaded | 14360 | 100 |
| RMCPropVehicleLoaderTruckWhiteF | armored_truck_white_f | 14361 | 94 |
| RMCPropVehicleLoaderTruckWhiteFilled | armored_truck_white_loaded | 14362 | 96 |

## Road-center divider correction

The former eight-part divider was centered on its tile, .96 tiles tall and had two invented mounting legs. The source `centerroadbarrier1` has two parallel thin frames and a central stem; its East/West images distinguish the two frames. The new sixteen-part draft has real open slots, two rows of rails, one stem and a small mounting foot. The height is .626 tiles, inferred from the source silhouette; colors are sampled from the original South frame.

The entire divider now follows its inherited fixture at local Y -.45 through -.30, centered near -.375. Actual geometry stays within -.445 through -.303 and rotates with each saved yaw. The source's projected `0,-0.15` sprite offset is retained in the comparison metadata, rather than copied blindly into world depth.

All 303 classic placements are checked: 186 at yaw zero, 66 at -90 degrees and 51 at +90 degrees. With the narrowed loader geometry held constant, conservative neighboring contacts fall from two with the previous divider to zero with the corrected one. No new contacts are introduced, including along rotated runs. Neither source map transforms nor collision fixtures change. Acid, damage and other dynamic divider states are not modeled.

## Verification and artifacts

- `Tools/three_d/generated/loader-trucks-source-audit.json`, `loader-trucks-crop-audit.json`, `loader-trucks-pixel-audit.json`, `loader-trucks-source-detail-audit.json`: source dimensions/states, exact crops, glass-color samples and the four animated indicator pixels.
- `loader-trucks-placement-audit.json`: all five facings/offsets, unchanged saved transforms/unrelated scene records and no nearby modeled intersections.
- `center-road-source-audit.json`, `center-road-placement-audit.json`: original fixture/directions, all 303 placements and previous/current contacts.
- `review/loader-trucks-*`: all four source/four-view cards, the corrected divider card, orbit comparisons and browser map captures.
- Four assembled regions: `garrison-loaders-security-west`, `garrison-loaders-security-east`, `garrison-loaders-beds` and `garrison-loaders-prison`, each with GLB and JSON reports. They export 33/58/161/78 objects and 121/121/361/121 floors, respectively; 21/1/7/1 unresolved or disabled inherited objects are omitted explicitly.

All 93 isolated native checks pass with the final 664-model library, including packing and geometry budgets. All 664 individual GLBs, manifests, references and atlas outputs match deterministic regeneration. Khronos validation reports zero errors and warnings for all 664 individual assets and 33 assembled exports. All five truck map positions were visually reviewed. The normal server-dependent test project remains unavailable for the unrelated tactical-map build failures recorded in `Tools/three_d/STATUS.md`.

Physical dimensions, unseen cab/chassis structure, reverse-side paint and roof-equipment details are inferred. Curved panels, suspension, materials, glazing response, animation, lights, damage, cargo movement and functional loader mechanisms remain unfinished. This asset-only pass does not change renderer code or replace the standard gameplay viewport.

## Original source attribution

Both original RSIs use CC-BY-SA-3.0. Retain this attribution and the source RSI metadata with exported assets.

- Truck art: `/Textures/_RMC14/Structures/Vehicles/vehicles3.rsi`. Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles
- Road-divider art: `/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi`. Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5cf465e72efb6beccd2b78bf263072816a2a60ad/icons/obj/structures/barricades.dmi

Canonical YAML includes successive guard, windshield, wheel-track, mirror and divider refinements. Do not replay the one-time scratch authoring/correction scripts over it.

# Fleet interior review

This batch covers all 27 interior maps referenced by the drivable fleet, plus the procedural fighter cockpit. It adds 192 model definitions and 197 exact prototype bindings. Nine invisible triggers, collision helpers and spawn/effect markers deliberately keep their native behavior. Existing exact models are reused for the other furnishings.

- `overview.png`: every mapped cabin, with upper walls and roofs cut away for inspection.
- `highlights.png`: Blackfoot, command APC, medical Humvee and SPP tank interiors.
- `layout-01.png` through `layout-27.png`: complete assembly and two cutaway angles at the saved map transforms.
- `fighter-cockpit.png`: hull, canopy, tandem seats and radio at the procedural spawn transforms.
- `assets-01.png` through `assets-16.png`: source artwork, front, rear and underside views.
- `coverage.json`: maps, vehicle variants, exact bindings and per-model geometry counts.
- `layout-review.json`: assembly counts and checks for another fixture crossing a seated eye position.

Geometry follows the existing map layout; no interior map, collision shape or vehicle inventory was changed. Large chassis have hollow crew spaces and overhead roofs. Doors overlapping a driver's tile in the original top-down artwork are placed at the cabin edge in 3D. Wall-mounted fixtures without an ordinary tile wall use their mapped cabin anchors.

These remain draft interpretations of the sprite artwork, particularly hidden surfaces and depth. New fittings use static appearances; storage/stock animation and live viewport video are not reproduced by these new meshes. The screenshots are offline geometry reviews, not a live multiplayer walkthrough.

## Verification

`python Tools/three_d/author_vehicle_interiors.py` regenerates the assets and source comparison sheets. `python Tools/three_d/review_vehicle_interiors.py` assembles all maps and checks seated eye positions. The final layout review has no seated-eye intersections. Export validation and SHA-256 checks cover every delivered GLB and the complete library manifest.

The focused integration command builds the affected content projects and passes both checks:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMU3DVehicleInteriorTest|FullyQualifiedName~OptionalLibraryLoadsOnlyOnClientAndCanReloadAfterPrototypeReset'
```

Requirement coverage:

- `LoadedCabinFollowsVehicleAcrossMapsIncludingParentGridTransfer`: lazy cabin creation on an unsupported map, enabling after transfer onto a supported map, revoking on departure, reusing the same cabin and deleting it with its vehicle.
- `OptionalLibraryLoadsOnlyOnClientAndCanReloadAfterPrototypeReset`: the optional model library loads on demand on the client and survives prototype reset without loading on the server.

Source licenses and reproduction details are in `../../SOURCES_VEHICLE_INTERIORS.md`.

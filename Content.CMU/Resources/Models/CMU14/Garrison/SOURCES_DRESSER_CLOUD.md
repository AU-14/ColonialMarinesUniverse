# Wooden dresser draft

New source: `Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_dresser_cloud.yml`

Exact intended prototype: `Dresser` (six Redux records in the committed inventory).
Original reference: `Resources/Textures/Structures/Furniture/furniture.rsi/dresser.png`, state `dresser`, one direction, 32 × 32 pixels. Original definition: `Resources/Prototypes/Entities/Structures/Furniture/dresser.yml`.

The original pixels were inspected at nearest-neighbor scale. The authoring keeps the source's brown wood palette, two upper small drawers plus two wide lower drawers, six brass knobs, dark seams, narrow front stiles, four feet, and stepped overhanging top. These are individually editable physical volumes rather than one sprite-shaped extrusion. The width is the visible 27/32-tile sprite width. Cabinet depth, hidden rear, joinery and physical height are inferred. The 45-part draft exports to 540 triangles. The flat top is explicitly available as a support surface.

The source has one intact dresser state. Storage UI behavior is unchanged; no opening animation or damage debris is invented. This is staged asset work, not a runtime modification. Saved-map contact fitting, native admission/picking, live gameplay and full fidelity have not been verified. Status remains `draft`.

## Source attribution

Source RSI license: CC-BY-SA-3.0. Metadata states: “Taken from tgstation at commit https://github.com/tgstation/tgstation/commit/d5cb4288ec5f7cb9fb5b6f6e798f4c64cd82cd09, Taken from vgstation at commit https://github.com/vgstation-coders/vgstation13/commit/9d7ff729b6b89eee0b3d750327f9fbaff4aeb045”. Original `meta.json` is included with the art workspace. This source-guided derivative is distributed under CC-BY-SA-3.0; preserve the metadata and this attribution with the exported model.

## Evidence

- `Tools/three_d/generated/cloud-review/dresser-source-and-orbit.png`: inspected original/front/three-quarter/rear comparison at consistent horizontal tile scale
- `Tools/three_d/generated/cloud-review/dresser-proof.json`: part count and deterministic export digest
- Repository `build_models.load_models` accepted all 45 positive-volume, finite-bounds parts and exact metadata
- Generated via unchanged repository exporter; repeat run produces identical GLB bytes


## Cumulative atlas assignment
This cumulative snapshot uses centrally allocated, nonconflicting atlas slots. Any authoring-time numeric range in this historical family note is superseded by the canonical surface YAML and `Tools/three_d/generated/cloud-review/atlas-allocation-current.json`. Source IDs, original PNG pixels and modeled geometry are unchanged by atlas-index remapping.

# Static source-specific ladders

Five exact mappings use four original static states in `_RMC14/Structures/ladder.rsi`. They cover 40 saved placements across all ten configured Redux/classic map files: 37 Redux and three classic. All retain their saved positions, zero yaw and zero sprite offset. The source noRot flag is false, so supported entity rotations rotate both geometry and apertures. Static source-state, direction, frame and transformation gates precede replacement geometry and openings. No activation animation, return endpoint or cross-map view is invented.

| Model | Exact source prototype | State | Saved placements |
|---|---|---|---:|
| CMU3DLadderThroughDown | CMUZLevelLadderThroughDown | ladder11 | 1 |
| CMU3DLadderThroughDown3 | CMUZLevelLadderThroughDown3 | ladderdown | 15 |
| CMU3DLadderThroughUp1 | CMUZLevelLadderThroughUp1 | ladder10 | 5 |
| CMU3DLadderThroughUp3 | CMUZLevelLadderThroughUp3 | ladderup | 16 |
| CMU3DRMCLadder | RMCLadder | ladderdown | 3 |

Separate solid rails and rungs leave actual open spaces. Ladder11 keeps its longer visible upper section and existing downward controller. Ladder10 and ladderup have an inferred full-height extension to the existing 2.95 ceiling top. Written rail, rung, paint and marker crops preserve exact original RGBA pixels. Other faces use original palette colors. Alpha-29 source shading between rungs remains in the reference; it is not converted into a membrane between physical rungs.

The rail span is 18 source pixels / 0.5625 tile; the marked rim spans 28 pixels / 0.875 tile. Aperture depth, hidden faces, rail standoff, full-height continuation and recessed cap remain inferred. The floor opening is X [-0.28125, 0.28125], Y [-0.4375, -0.03125]. The ceiling opening is X [-0.28125, 0.28125], Y [-0.4375, 0.09375]. Recess walls end at a cap below -0.82, never at another rendered map. Gameplay tiles, collisions and links are unchanged.

The up rail/rung/foot plane shifts 0.046875 tile south, following the held source/context trial. That trial cleared red light 4167. A later indexed audit found two new shallow foot/grate joins at surface UID 19969 while removing two light and two lower-rail/grate pairs. Wire rails, APCs, lights and other fixtures remain present. This asset release preserves the reviewed geometry; it does not assert every context contact or packed admission is resolved. The corner platform at Redux UID 16881 and final native/export checks remain distinct context work.

## Deferred composition

CMU3DLadderThroughDown2 / CMUZLevelLadderThroughDown2 is deliberately excluded. Its one Redux surface placement, UID 12468, shares a pivot with a pallet and two cartons. Its separate inferred fitting proposal and proofs remain held under `.codex/ladder-down2-candidate`. This release installs no Down2 model, no companion part frames and no global pallet/carton changes. The unresolved composition retains its original representation.

## Source attribution and regeneration

Original ladder artwork is CC-BY-SA-3.0 per `Resources/Textures/_RMC14/Structures/ladder.rsi/meta.json`, from CM-SS13 structures.dmi at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/structures.dmi and dropship_equipment.dmi at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/dropship/dropship_equipment.dmi. All eleven derived crops retain exact source RGBA pixels and this attribution. Atlas slots are 1480–1490.

Run `python Tools/three_d/author_ladders.py --staging <directory>` for a separate output tree, or `--output-root .` to regenerate only these dedicated assets. Add `--check` for read-only asset comparison. Optional `--scene-manifest <final-scenes.json> --models-json <models.json>` reads already exported scenes for context cards; it never runs a global exporter. Native Vector2/Vector3 fields serialize as scalar comma-separated values. The generator verifies written PNGs, serialized YAML, source frames and saved map hashes. Historical staged inputs and the Down2 proposal are preserved. These remain drafts with inferred physical dimensions. No game client/server or build is started or stopped.

# Large desk computer drafts

Three exact source mappings cover six Redux placements: RMCPropComputerLarge (3), RMCPropComputerLarge0 (1), and RMCPropComputerLargeDark (2). The source is `Resources/Textures/_RMC14/Structures/Machines/computer.rsi`, states `largecomp`, `largecomp0`, and `largecomp_dark`. Each is one 32×32 frame and one direction, with zero Sprite offset and noRot=false. No new animation or power transition is invented.

The original art depicts a stepped monitor housing above a separate keyboard deck. These drafts use a thick rear case, four bezel solids around a physically recessed screen, stepped crown, hinge rail, sloping solid keyboard, and supporting base. Source width is 20 pixels / .625 tiles; the 26-pixel sprite height supplies a .8125-tile height reference. Depth (approximately .48 tiles), rear construction, keyboard slope, and the conversion from screen-space height to physical height are inferred. Source-colored planar crops are attached to those solids; the whole computer is not a single image plate.

All 508 opaque source pixels reassemble exactly from the on-disk crops. This verifies retained artwork, not an exact arbitrary-camera silhouette or automatic fidelity approval. Lit versus blank changes 19 display pixels; dark casing changes 282 pixels. All source alpha masks are identical.

The existing authored support selects table tops at .86, then adds a .002 gap above the model bottom. UID 10612 selects table 10615 over co-located 10616. Other support pairs are 10618→10617, 3094→687, 3095→701, 3096→703, and 4737→1042. Requisition-table border rivets reach .869; computer footprints stay inside the rim and context contact checks include those decorative parts. Saved positions and rotations are preserved, including UID 3094 at pi radians. Static, non-colliding, unanchored prop physics is unchanged.

Artwork license: CC-BY-SA-3.0. Source attribution from RSI metadata: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d5b119380250ea512db2a5319e36592c7f604250/icons/obj/structures/machinery/computer.dmi; edits to overwatch and register by github noctyrnal. Derived crops retain original RGBA pixels. Keep this note and the RSI attribution with redistributed assets.

Review images show actual saved modeled neighbors at both front and rear angles. Unknown neighbors remain explicit. Conservative part AABB checks do not prove gameplay collision, native capacity, or player visibility. Global exports and native validation are coordinated separately; no game is launched by the generator.

Reproduce the dedicated assets with `Tools/three_d/author_large_computers.py` (staging by default; `--apply` writes the dedicated resources). The source/context evidence is `Tools/three_d/generated/large-computers-proof.json`; review cards are in `Tools/three_d/generated/large-computers-review/`. The generator verifies the recorded historical source-context inputs before recreating those cards.

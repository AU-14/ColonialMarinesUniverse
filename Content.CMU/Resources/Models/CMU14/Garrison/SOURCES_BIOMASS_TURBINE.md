# Biomass power turbines — 2026-09-25

Four source-guided drafts cover 16 saved Stable Garrison Redux entities on levels -1
and +1. The audit checked all ten configured Garrison maps (seven Redux and three
classic); the classic maps contain no placements of this family. All four models
retain their exact source prototype, one-direction reference and saved entity yaw.

| Model | Exact source prototype | Reference state | Parts | Saved placements |
| --- | --- | --- | ---: | ---: |
| `CMU3DBiomassTurbine` | `RMCPropTurbine` | `biomass_turbine` | 64 | 5 |
| `CMU3DBiomassLeft` | `RMCPropTurbineStrutsLeft` | `support_struts_l` | 4 | 3 |
| `CMU3DBiomassRight` | `RMCPropTurbineStrutsRight` | `support_struts_r` | 4 | 3 |
| `CMU3DBiomassBorder` | `RMCPropTurbineStrutsBorder` | `biomass_turbine_border` | 1 | 5 |

## Original artwork and attribution

The source prototype definitions are in
`Resources/Prototypes/_RMC14/Entities/Structures/Props/biomass_turbine.yml`.
The original RSI is
`Resources/Textures/_RMC14/Structures/Props/biomass_turbine.rsi`.
Its metadata declares **CC-BY-SA-3.0** and attributes the artwork to cmss13:

[Original biomass_turbine.dmi at commit 0525b5ada7da1afcd9b260e76d5fea01500d9c8d](https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/structures/props/industrial/biomass_turbine.dmi).

The RSI metadata remains authoritative. Derivative geometry and source-pixel crops
retain this attribution and license. The source frame size is 32 by 96 pixels;
the four saved states are static and single-direction. Physical depth, back surfaces,
cross sections and elevation cannot be measured from that single source view.

Five unresampled opaque core crops preserve 580 exact RGBA pixels: the left and
right service grilles, intake detail, warning badge and battered bearing surface.
They use `CMU3DBiomassTurbine*` surface IDs at atlas slots 1080–1084. Untextured
core colors are sampled from opaque pixels in the original static frame.
The support generator uses eight exact bracket crops and one complete transparent
border image, at slots 1047–1055. Reassembling each support image reproduces its
entire 32-by-96 RGBA canvas byte-for-byte: 9,216 reconstruction samples across
three states, including 71 opaque pixels per strut state and 660 for the border.
These pixel-preservation checks do not establish fidelity of inferred geometry.

## Physical interpretation and source alignment

The core's authored collision rectangle is exactly X -0.5 to +0.5 and Y -1.5 to
+1.5 tiles. The draft remains inside that one-by-three footprint, with final
rotated part bounds X -0.49 to +0.49, Y -1.495 to +1.49 and Z 0 to 1.036.
Its axis follows local Y. Cylindrical bearing housings, red/brown collars,
paired raised top service banks and a central metallic conduit follow the visible
source arrangement. The source-pixel intake faces local -Y and the service grilles
face +Z. The source texture aspect ratio is retained on the recessed intake face.

The intake is physically hollow: twelve pitched solid segments form its deep
annular casing, and twelve more form the thin steel lip. There is no filled disk
in front of the rotor. The lip begins at Y -1.495; the rotor begins at -1.305,
giving 0.190 tiles of visible recess, with dark circular backing just behind it.
The 0.340-tile inner bore radius, segmented cross section and recess depth are
inferred. The generator checks actual rotated bounds and an unobstructed circular
cross section at Y -1.4. The casing closes behind the recessed assembly.

Core side lugs reach X +/-0.49 at Y +/-1.0 and Z 0.45–0.62. Independent support
entities keep the original sprite pivot [16,48] and 32 pixels per tile. Pixel
coordinates map to X = px/32 - 0.5 and Y = 1.5 - py/32. The left state occupies
the west edge of its own tile; the right occupies the east edge. Their source rows
13–20 and 77–84 place two separate brackets near Y +/-1.0. The far bracket is six
pixels wide and the near one five. Four nonoverlapping volumes per model retain
the actual L-shaped opaque masks and their gaps. The attachment height Z 0.45–0.62
is inferred to match the core lugs; the source provides no measured vertical height.

The border is an independent, noncolliding alpha-cut floor marking over the full
one-by-three canvas. Its open center, curved end outline and narrow hazard paint
remain transparent where the source is transparent. It lies at Z 0.008–0.012,
an inferred paint elevation. It is not mounted on the co-centered core and does
not form a platform or railing. Neither the 2D draw-depth order nor nearby floor
tiles are treated as a physical support height.

## Saved map context

All 16 saved transforms have zero yaw and parent UID 1. The only saved component
overrides are Transform and, for decorations, explicitly empty Fixtures. Source
Sprite offset is zero and `noRot` is false by default. Each model has
`useEntityRotation: true`; saved positions and angles are unchanged.

| Redux level | Core UID | Center X, Y | Co-centered border UID |
| --- | ---: | --- | ---: |
| -1 | 10275 | 197.5, -119.5 | 10277 |
| -1 | 10276 | 197.5, -115.5 | 10278 |
| +1 | 4739 | 97.5, -63.5 | 4741 |
| +1 | 4738 | 99.5, -63.5 | 4742 |
| +1 | 4740 | 101.5, -63.5 | 4743 |

The six strut entities occur only on +1, all at Y -63.5. Left UIDs 4745, 4744
and 4746 are at X 98.5, 100.5 and 102.5. Right UIDs 4747, 4748 and 4749 are at
X 96.5, 98.5 and 100.5. Two internal tiles therefore contain both strut variants
alongside `RMCCatwalkStrata`. Their brackets occupy opposite tile edges and keep
the central gap open. The model names describe source-art edges; they are not
instructions to mirror or recenter the saved objects. No struts are invented for
the lower-level machines.

No additional entity shares any core/border center. Upper cores are spaced two
tiles apart across X, with reinforced wall centers at Y -65.5 and -61.5. Extending
the turbine beyond its three-tile length would invade those wall footprints.
Lower cores are four tiles apart along Y, on `RMCPlanetHybrisaSidewalk` terrain.
Nearby prison catwalks and shallow water occupy adjacent corridors, not the core
centers. Their current model top elevations, 0.035 and 0.012 respectively, do not
raise the turbine or its warning paint. Nearby fusion generators do not establish
a turbine control relationship. Collision remains the source core fixture;
struts and border remain noncolliding decorations.

## States and animation limits

The separate `biomass_turbine-on` resource has three 32-by-96 frames at 0.1 seconds
per frame, stored in a 64-by-192 sprite sheet. Its existence is recorded, but no
operating animation, activation transition or controller is implemented by these
models. The saved maps select the static `biomass_turbine` state only.

The source audit found no ordinary prototype reference to the on-state, no saved
Sprite override, and no inherited Appearance, GenericVisualizer, animation,
power or toggle controller in this family. Searching 13,277 C# files for the
prototype/state-family names found no dedicated controller; 5,692 ordinary
prototype YAML files contained no on-state reference. This bounds the claim to
the audited source and saved maps; generic administrative sprite edits and runtime
spawns are outside the audit. The core's descriptive text about possibly turning
it on is not evidence of implemented gameplay. No active state is claimed complete.

## Authoring and verification status

Editable models are `garrison_biomass_turbine.yml` and
`garrison_biomass_supports.yml` under
`Content.CMU/Resources/ThreeD/Prototypes/World/`. Their matching art definitions are
`garrison_biomass_turbine_art.yml` and `garrison_biomass_support_art.yml`.
The two generators are `Tools/three_d/author_biomass_turbine.py` and
`Tools/three_d/author_biomass_supports.py`. Textures live under
`Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces/`.

Own-model validation and source/four-view review cards are complete. The cards are
under `Tools/three_d/generated/review/biomass-turbine-core/` and
`Tools/three_d/generated/review/biomass-supports/`. Exact support-mask reconstruction
is recorded in `Tools/three_d/generated/biomass-support-source-audit.json`.
Local core bounds, palette, crop and cavity evidence is recorded in
`Tools/three_d/generated/biomass-turbine-core-proof.json`; the ten-map source inventory and 570
same-level neighbor records are in `Tools/three_d/generated/biomass-source-placement-audit.json`, with
zero source world-transform errors across all 16 placements.

All four models remain drafts without fidelity approval. Height, elevation,
hidden construction, pipe meaning and intake depth are inferred. Final checks
in `Tools/three_d/generated/biomass-export-audit.json` verify all 16 saved
placements, 16 synthetic yaw poses, 927 exported entity roots and 19,224 part
transforms. All 837 model GLBs and 17 assembled/rotation exports validate with
zero Khronos errors or warnings. The source-fit audit verifies the support/paint
pixels, shared gaps, six attachments and preservation of all 833 older models.

`Tools/three_d/generated/biomass-native-budget-audit.json` uses the actual built
encoder for 64 camera scenarios in baseline/after/no-family modes. All 640 family
appearances fit, with zero introduced neighboring omissions or capture failures.
The same pre-existing upper-level platform remains omitted in 12 occurrences.
The focused native library-budget test and 213 Python tests pass. Work and visual
review are offline; no game/server launch, live input test or runtime fidelity
acceptance is included in this batch.

Offline assembled comparisons in `Tools/three_d/generated/review/biomass-context/`
show all 16 family entities and two co-centered catwalks alongside untrimmed
source sprites at 32 pixels per tile. Two CPU-rendered viewing angles preserve
the saved layout, open support gaps and south-facing intakes. The floor is a
labeled flat color reference and surrounding walls are omitted from these images;
full scene exports and native budget checks retain neighboring geometry.

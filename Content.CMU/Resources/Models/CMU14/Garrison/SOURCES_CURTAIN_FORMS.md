# Curtain fabric drafts

The 12 open/closed assemblies cover shower cloth, black theatre fabric, beige/black/green translucent curtains, and green opaque fabric. The source states are static 32 by 32 images. The crops are derivatives of the following artwork and retain its CC-BY-SA-3.0 license:

- `Resources/Textures/_RMC14/Structures/Furniture/Curtains/shower.rsi` and `green.rsi`: CM-SS13 contributors, [curtain.dmi at source commit 0a8d59a](https://github.com/cmss13-devs/cmss13/blob/0a8d59abad27ec6112ef59d7661ab2139e227d0a/icons/obj/structures/props/curtain.dmi).
- `Resources/Textures/_RMC14/Structures/Furniture/Curtains/colorable_transparent_alt.rsi`: the same CM-SS13 artwork, modified by GitHub user noctyrnal. Prototype tints distinguish the three translucent fabrics.
- `Resources/Textures/Structures/Decoration/Curtains/black.rsi`: created by TheShuEd (GitHub) for Space Station 14.

Source license: [Creative Commons Attribution-ShareAlike 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Attribution is transcribed from the local RSI metadata. Derived textures are in `Content.CMU/Resources/Textures/CMU14/ThreeD/CurtainFolds/`; 142 unique crops preserve the original RGBA data. Source hashes and crop rectangles are recorded in `Tools/three_d/generated/curtain-form-source-audit.json`.

Canonical definitions are `garrison_remaining_doors.yml`, `garrison_green_fabric_curtains.yml` and `garrison_curtain_fold_surfaces.yml` in the ThreeD prototype directory. The original source column occupancy is carried on thin box sections, including gaps in the open fabric and translucent pixels. The reverse uses the back of the same printed surface. These are textured folded volumes, not sculpted or simulated cloth.

Physical height (2.4 tiles), shallow fold amplitude (.023), minimum thickness (.012), rear construction and hanging hardware depth are inferred. Black theatre fabric spans .944 tiles to clear the saved bunk-alcove wall seams and uses the existing wall-facing rule to put the opening on the clear side. Green opaque fabric hangs at local Y -.35, inset from the default -.44 plane to clear medical-bay trim. These physical choices are not reconstructed dimensions. Saved entity positions and rotations are unchanged; presentation facing is recorded separately.

Existing shower opening, glazing and end-fitting rules are retained. Black translucent fabric fits named Hybrisa wall ends; green translucent fabric mounts inside its co-located blue directional windows. These rules apply independently to both stable poses. Heights, lighting, material transmission and arbitrary future contexts remain unapproved.

No animation clip is added. The source `Door` component declares 0.5/0.1-second phases, but the two source images do not define cloth motion between them. Transition timing, interruption, fire/destruction and native interaction remain unfinished. Six saved Closing curtains remain unsupported markers. Stable pose coverage does not establish full state coverage.

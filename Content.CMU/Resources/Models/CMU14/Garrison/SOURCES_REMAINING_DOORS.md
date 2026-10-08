# Curtains, secure windoors and resin doors

14 draft assemblies. Closed/open source frames and source fixture bounds inspected. Geometry is editable named solids; no sprite pixels are embedded.

New geometry is contributed under CC0-1.0 to the extent separately licensable. Source visual-design and derivative rights retain their existing licenses. Keep this attribution with exports.

Curtains keep the physical entity rotation despite their single-frame billboard sprites. Their hanging plane is at local Y -0.44 near the facing tile edge; a top-view map check at shower #10352 exposed and corrected the original centered placement. Folds gather at both ends. Tint values come from the resolved source prototypes. Windoors retain their thin south-edge fixture and fixed side housing. Resin doors use an irregular arch and diagonal sinew instead of an airlock frame. Height and hidden surfaces remain inferred. Native glass/fabric are opaque; dynamic overlays, cloth simulation and resin animation remain unfinished.

| Model | Pose | Exact sources | Reference state |
| --- | --- | --- | --- |
| CMU3DShowerCurtain | Closed | RMCCurtainShower | closed |
| CMU3DShowerCurtainOpen | Open | RMCCurtainShowerOpen | open |
| CMU3DBlackTheatreCurtain | Closed | State-only alternate | closed |
| CMU3DBlackTheatreCurtainOpen | Open | CurtainsBlackOpen | open |
| CMU3DBeigeBlinds | Closed | RMCCurtainTransparentBeige | closed |
| CMU3DBeigeBlindsOpen | Open | State-only alternate | open |
| CMU3DBlackBlinds | Closed | RMCCurtainTransparentBlack | closed |
| CMU3DBlackBlindsOpen | Open | RMCCurtainTransparentBlackOpen | open |
| CMU3DGreenBlinds | Closed | RMCCurtainTransparentGreen | closed |
| CMU3DGreenBlindsOpen | Open | RMCCurtainTransparentGreenOpen | open |
| CMU3DSecureWindoor | Closed | CMWindoorSecure, RMCWindoorSecureTSEPABrig | closed |
| CMU3DSecureWindoorOpen | Open | State-only alternate | open |
| CMU3DResinDoor | Closed | DoorXenoResin | resin |
| CMU3DResinDoorOpen | Open | State-only alternate | resinopen |

## Attribution

### Resources/Textures/Structures/Decoration/Curtains/black.rsi

- Metadata: `Resources/Textures/Structures/Decoration/Curtains/black.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Created by TheShuEd (github) for Space Station14

### Resources/Textures/_RMC14/Structures/Doors/Windoors/secure.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Windoors/secure.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/windoor.dmi

### Resources/Textures/_RMC14/Structures/Furniture/Curtains/colorable_transparent_alt.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/Curtains/colorable_transparent_alt.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0a8d59abad27ec6112ef59d7661ab2139e227d0a/icons/obj/structures/props/curtain.dmi, modified by github noctyrnal

### Resources/Textures/_RMC14/Structures/Furniture/Curtains/shower.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/Curtains/shower.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0a8d59abad27ec6112ef59d7661ab2139e227d0a/icons/obj/structures/props/curtain.dmi

### Resources/Textures/_RMC14/Structures/Xenos/xeno_resin_door.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Xenos/xeno_resin_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/af7813ee8c36f9195a88f3b3d018af2b8833cbea/icons/mob/xenos/effects.dmi

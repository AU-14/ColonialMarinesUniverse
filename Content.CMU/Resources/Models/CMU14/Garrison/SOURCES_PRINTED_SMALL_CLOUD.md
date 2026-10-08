# Printed and smoking-prop cloud drafts

## Scope and evidence

Source repository: TheHellFireo/CMU-Garrison-3D, ref `Chip/garrison-3d`. Actual RSI PNGs were fetched with GitHub `fetch_file` base64 and verified against all 71 returned Git blob SHA values. Prototype inheritance and relevant storage/card client defaults were inspected. Original source records and retrieved file hashes are in `reference/printed-small-fetches.json`.

22 editable physical assemblies cover 14 exact target IDs (13 mapped designs and nine unbound construction studies). Inventory reports 29 Redux and 15 classic placements for those IDs. Placement counts are not scene-fit or runtime evidence. All models remain draft. No engine or runtime files were edited, no gameplay/map/server was launched, and nothing was published.

Canonical art files:
- `Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_printed_small_cloud.yml`
- `Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_printed_small_cloud_art.yml`
- `Content.CMU/Resources/Textures/CMU14/ThreeD/printed_small_cloud/`

The 27 surface indices use the exact first 27 entries of `families.printed_small.indices` in `reference/cloud-atlas-allocations.json`, preserving all gaps. No numeric-range assumption was used. Cross-family index uniqueness passes. Source crops are original unchanged RGBA pixels. The cookbook cover tint is the explicit source `#e22541` part multiplier; decorations retain their original colors.

## Geometry and state boundaries

- Cigars and cigarettes: true circular rolled bodies, tapered or cut ends, raised paper bands and separate cooled ash. No whole-sprite backing slab. Matching unlit/burnt loose-world states only; lit/burning, smoke, held/equipped forms and consumption are unsupported.
- Ashtray: actual open recessed basin, sixteen wall facets, notched rim and closed floor. The inspected sole StorageFill layer uses icon-0/icon-1/icon-2, so the unchanged source-state adapter selects three static coarse visual fills. Actual item identity, pouring and broken debris remain unsupported.
- Cigarette packets: independent back, side and end folds, front face and separate lid. Default closed storage hides four/twenty initial contents. Existing single-visible-layer closed adapters match both packets and reject their open multi-layer composition. Open empty/full geometry is unbound construction study only; no dynamic multi-layer storage/count support is claimed.
- Cigar case: hollow frame, cedar liner, separate decorated lid, brass hinges and latch. Two unbound open studies expose the cavity and seven real stored cigar volumes. Default closed source contains no visible cigars; its existing one-layer adapter matches closed and rejects the open multi-layer composition.
- Boots magazines N113 and N131 intentionally share exactly the same icon-1 source, with one mapped physical design. The cover, rear, folded spine, paper leaves and staples are independent solids; no issue-specific text was invented.
- Matchbook: inherited GenericVisualizer hides openLayer when closed even though child mpacket0 lacks visible=false in raw YAML. Default is mpacket only. Unbound empty/full studies contain a physical fold, retaining strip and six match stems/heads. Burning and multi-layer count selection are unsupported.
- Cookbook: source paper, red-tinted cover_base, decor_wingette and icon_apple remain separate original layers on a hardback assembly with spine and leaves. Reading UI, interior print and page-turning are unsupported.
- Card deck: actual source is a printed carton. Inspected client code selects deck for 52 cards, deck_open for 1–51, and deck_empty for zero. The existing one-layer adapter provides those three static coarse states, including a genuine empty carton opening. Open paper leaves are representative, not an asserted exact remaining count; shuffle motion, order, individual cards, hand stack and UI remain unsupported.
- Folder: two real thin leaves and folded spine with original color/base artwork. Initial contents are random 0–5 documents, so only the invariant shell is mapped. One unbound paper-overlay study is representative, not a claimed default. Live ItemMapper overlay/count support remains open.

## Reproduction and bounded checks

Run from the repository root:

    python Tools/three_d/author_printed_small_cloud.py
    python Tools/three_d/verify_printed_small_cloud.py
    blender -b --python Tools/three_d/check_printed_small_blender.py

Authoring calls the existing unchanged `build_models.glb_bytes` directly. No post-export GLB edits. Verification checks deterministic equality for all 22 assets, finite transforms, buffer bounds, original PNG Git hashes, exact crop equality, source states/directions/delays, default contracts and 18 specific occupied/empty sample checks. Another 25 source-adapter checks verify matching defaults and rejection of unsupported source states/open layer combinations. Blender 4.3.2 independently imports all 22 final GLBs with zero nonfinite vertices or repaired meshes, recorded separately; neither check certifies visual fidelity or native rendering. Khronos glTF validation is unavailable in this environment and was not run.

Reports:
- `Tools/three_d/generated/printed-small-cloud-verification.json`
- `Tools/three_d/generated/printed-small-cloud-source-audit.json`
- `Tools/three_d/generated/printed-small-cloud-blender-import.json`

Previews:
- `Tools/three_d/generated/review/printed-small-cloud/printed-small-highlights.png`
- `Tools/three_d/generated/review/printed-small-cloud/printed-small-source-orbit-sheet.png`
- `Tools/three_d/generated/review/printed-small-cloud/printed-small-fixed-scale.png`
- Individual source/top/two-orbit comparisons and all supported stable ashtray/deck states are beside those sheets

## Explicit limitations

Inferred depth, hidden construction, seams, bevels and contact geometry remain draft. Source perspective painted into flat printed surfaces is preserved rather than falsely claimed as a measured physical shape. There are no added animation clips. No native renderer acceptance, map contact audit, gameplay interaction, complete state coverage or fidelity approval is claimed.

## RSI attribution

### /Textures/Objects/Misc/books.rsi

- License: CC-BY-SA-3.0
- Copyright / source: Base sprite taken at: https://github.com/tgstation/tgstation/commit/37fb6bc6dd20005775dde8d886f48f7722606b77 , splitted on layers by TheShuEd (github)
- Retrieved RSI metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/Objects/Misc/books.rsi/meta.json

### /Textures/Objects/Misc/folders.rsi

- License: CC-BY-SA-3.0
- Copyright / source: Taken from tgstation at https://github.com/tgstation/tgstation/commit/e1142f20f5e4661cb6845cfcf2dd69f864d67432, inhands by TiniestShark (github)
- Retrieved RSI metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/Objects/Misc/folders.rsi/meta.json

### /Textures/_RMC14/Objects/Consumable/Smokeables/Cigarettes/lucky_sloths_4.rsi

- License: CC-BY-SA-3.0
- Copyright / source: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/6710c72c8a67d4cd21b9fa10b5c52ffa0747b8dc/icons/obj/items/cigarettes.dmi, https://github.com/cmss13-devs/cmss13/blob/106c92cdf232ebc12c9d7a2feb23956c6755496f/icons/mob/humans/onmob/items_lefthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/106c92cdf232ebc12c9d7a2feb23956c6755496f/icons/mob/humans/onmob/items_righthand_0.dmi
- Retrieved RSI metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Consumable/Smokeables/Cigarettes/lucky_sloths_4.rsi/meta.json

### /Textures/_RMC14/Objects/Consumable/Smokeables/Matchboxes/matchbook.rsi

- License: CC-BY-SA-3.0
- Copyright / source: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/6710c72c8a67d4cd21b9fa10b5c52ffa0747b8dc/icons/obj/items/cigarettes.dmi, https://github.com/cmss13-devs/cmss13/blob/106c92cdf232ebc12c9d7a2feb23956c6755496f/icons/mob/humans/onmob/items_lefthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/106c92cdf232ebc12c9d7a2feb23956c6755496f/icons/mob/humans/onmob/items_righthand_0.dmi
- Retrieved RSI metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Consumable/Smokeables/Matchboxes/matchbook.rsi/meta.json

### /Textures/Objects/Consumable/Smokeables/Cigars/cigar.rsi

- License: CC-BY-SA-3.0
- Copyright / source: Taken from tgstation at commit https://github.com/tgstation/tgstation/commit/bfc9c6ba8126ee8c41564d68c4bfb9ce37faa8f8. lit-equipped-MASK-vox & unlit-equipped-MASK-vox states taken from /vg/station at commit https://github.com/vgstation-coders/vgstation13/commit/4638130fab5ff0e9faa220688811349d3297a33e | vulpkanin version taken from Paradise station at https://github.com/ParadiseSS13/Paradise/commit/f0fa4e1fd809482fbc104a310aa34cebf7df157d
- Retrieved RSI metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/Objects/Consumable/Smokeables/Cigars/cigar.rsi/meta.json

### /Textures/Objects/Consumable/Smokeables/Cigarettes/cigarette.rsi

- License: CC-BY-SA-3.0
- Copyright / source: Taken from tgstation at commit https://github.com/tgstation/tgstation/commit/bfc9c6ba8126ee8c41564d68c4bfb9ce37faa8f8 | vulpkanin version taken from Paradise station at https://github.com/ParadiseSS13/Paradise/commit/f0fa4e1fd809482fbc104a310aa34cebf7df157d | vox version made by Kittygyat
- Retrieved RSI metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/Objects/Consumable/Smokeables/Cigarettes/cigarette.rsi/meta.json

### /Textures/_RMC14/Objects/Decoration/ashtray.rsi

- License: CC-BY-SA-3.0
- Copyright / source: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/smoking/ashtray.dmi
- Retrieved RSI metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Decoration/ashtray.rsi/meta.json

### /Textures/_RMC14/Objects/Consumable/Smokeables/Cigars/cigar_case.rsi

- License: CC-BY-SA-3.0
- Copyright / source: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/cigarettes.dmi
- Retrieved RSI metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Consumable/Smokeables/Cigars/cigar_case.rsi/meta.json

### /Textures/_RMC14/Objects/Consumable/Smokeables/Cigarettes/executive_select.rsi

- License: CC-BY-SA-3.0
- Copyright / source: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/cigarettes.dmi
- Retrieved RSI metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Consumable/Smokeables/Cigarettes/executive_select.rsi/meta.json

### /Textures/_RMC14/Objects/Consumable/Smokeables/Cigarettes/unfiltered_roll.rsi

- License: CC-BY-SA-3.0
- Copyright / source: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/05eaa3484b1a35aa759300bcfc8f0119f009daae/icons/obj/items/clothing/masks.dmi
- Retrieved RSI metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Consumable/Smokeables/Cigarettes/unfiltered_roll.rsi/meta.json

### /Textures/_RMC14/Objects/Misc/boots_magazine.rsi

- License: CC-BY-SA-3.0
- Copyright / source: Taken from https://github.com/cmss13-devs/cmss13/blob/8ebd2f8cb96c2cee31df43f39634d65147a8a265/icons/obj/items/paper.dmi, WeYa Orrery made by SharkSnake98 on GitHub
- Retrieved RSI metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Misc/boots_magazine.rsi/meta.json

### /Textures/_RMC14/Objects/Fun/playing_cards.rsi

- License: CC-BY-SA-3.0
- Copyright / source: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/playing_cards.dmi
- Retrieved RSI metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Fun/playing_cards.rsi/meta.json

## Exported assemblies

- `CMU3DBookHowToCookForFortySpacemanPrintedSmallCloud.glb`: BookHowToCookForFortySpaceman; 14 default parts; 1 static scene(s); zero clips
- `CMU3DBoxFolderBasePrintedSmallCloud.glb`: BoxFolderBase; 5 default parts; 1 static scene(s); zero clips
- `CMU3DBoxFolderBasePrintedSmallCloudPaperStudy.glb`: unbound construction study; 9 default parts; 1 static scene(s); zero clips
- `CMU3DCMCigarettePackLuckySlothsMiniPrintedSmallCloud.glb`: CMCigarettePackLuckySlothsMini; 9 default parts; 2 static scene(s); zero clips
- `CMU3DCMCigarettePackLuckySlothsMiniPrintedSmallCloudOpenEmptyStudy.glb`: unbound construction study; 10 default parts; 1 static scene(s); zero clips
- `CMU3DCMCigarettePackLuckySlothsMiniPrintedSmallCloudOpenFullStudy.glb`: unbound construction study; 18 default parts; 1 static scene(s); zero clips
- `CMU3DCMMatchBookPrintedSmallCloud.glb`: CMMatchBook; 4 default parts; 1 static scene(s); zero clips
- `CMU3DCMMatchBookPrintedSmallCloudOpenEmptyStudy.glb`: unbound construction study; 5 default parts; 1 static scene(s); zero clips
- `CMU3DCMMatchBookPrintedSmallCloudOpenFullStudy.glb`: unbound construction study; 17 default parts; 1 static scene(s); zero clips
- `CMU3DCigarPrintedSmallCloud.glb`: Cigar; 10 default parts; 2 static scene(s); zero clips
- `CMU3DCigarSpentPrintedSmallCloud.glb`: CigarSpent; 21 default parts; 2 static scene(s); zero clips
- `CMU3DCigaretteSpentPrintedSmallCloud.glb`: CigaretteSpent; 7 default parts; 2 static scene(s); zero clips
- `CMU3DRMCAshtrayPrintedSmallCloud.glb`: RMCAshtray; 33 default parts; 4 static scene(s); zero clips
- `CMU3DRMCCigarCasePrintedSmallCloud.glb`: RMCCigarCase; 12 default parts; 2 static scene(s); zero clips
- `CMU3DRMCCigarCasePrintedSmallCloudOpenEmptyStudy.glb`: unbound construction study; 11 default parts; 1 static scene(s); zero clips
- `CMU3DRMCCigarCasePrintedSmallCloudOpenFullStudy.glb`: unbound construction study; 25 default parts; 1 static scene(s); zero clips
- `CMU3DRMCCigarettePackExecutiveSelectPrintedSmallCloud.glb`: RMCCigarettePackExecutiveSelect; 9 default parts; 2 static scene(s); zero clips
- `CMU3DRMCCigarettePackExecutiveSelectPrintedSmallCloudOpenEmptyStudy.glb`: unbound construction study; 10 default parts; 1 static scene(s); zero clips
- `CMU3DRMCCigarettePackExecutiveSelectPrintedSmallCloudOpenFullStudy.glb`: unbound construction study; 50 default parts; 1 static scene(s); zero clips
- `CMU3DRMCCigarettePrintedSmallCloud.glb`: RMCCigarette; 4 default parts; 2 static scene(s); zero clips
- `CMU3DRMCMagazineBootsN113PrintedSmallCloud.glb`: RMCMagazineBootsN113, RMCMagazineBootsN131; 11 default parts; 2 static scene(s); zero clips
- `CMU3DRMCPlayingCardDeckPrintedSmallCloud.glb`: RMCPlayingCardDeck; 9 default parts; 4 static scene(s); zero clips

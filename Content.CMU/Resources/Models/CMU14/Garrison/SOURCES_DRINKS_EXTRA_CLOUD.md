# Additional drinks, chemistry props and produce: physical art drafts

Date: 2026-10-06. Frozen source: `TheHellFireo/CMU-Garrison-3D` at `6e37a4d0a7d9433838c393a82c02422cb704dd5a`.

## Delivery and scope

This art-only family contains **26 editable assemblies**, **31 distinct authored poses**, 42 portable GLB scene entries and **zero animation clips**. The direct `build_models.py` exporter is unchanged. All model statuses remain `draft`; no gameplay, renderer, runtime adapter, map, engine or publication change is included.

- Sixteen exact-ID static draft proposals describe 28 Redux / eight classic source-inventory placements
- Six conditional `TreasureSampleTube` models describe its six actual random choices, with one additional Redux inventory placement; they are **not** six exact IDs, and no guessed unconditional default is bound
- Four unbound studies: open/empty whiskey, two intermediate coffee fills, and empty cryoxadone vessel
- All seventeen assigned IDs were checked against `physicalModelingFamilies` and the Redux inventory
- Thirty-six unchanged source-detail PNG crops use exactly the first 36 entries in `families.drinks_extra.indices`, currently 2147–2182. The authoritative allocation list is read directly rather than assuming contiguous slots. Byte-identical crops are shared between source states

Canonical resources:

- `Content.CMU/Resources/ThreeD/Prototypes/World/garrison_drinks_extra_cloud.yml`
- `Content.CMU/Resources/ThreeD/Prototypes/World/garrison_drinks_extra_cloud_art.yml`
- `Content.CMU/Resources/Textures/CMU14/ThreeD/drinks_extra_cloud/`
- Direct `CMU3D*DrinksExtraCloud*.glb` files beside this document
- `Tools/three_d/generated/review/drinks-extra-cloud/`: original compositions, source-facing / orbit / rear views, fixed-scale sheet and highlights
- `Tools/three_d/generated/drinks-extra-cloud-files.json`: delivery manifest with hashes

These placement numbers are inventory references. They do not establish native admission, live state selection, saved-map fit or approved fidelity.

## Source defaults and state interpretation

### Three RMC alcohol bottles

`RMCDrinkAlcoholWhiskey`, `RMCDrinkAlcoholSake` and `RMCDrinkAlcoholVodka` inherit `RMCDrinkAlcoholBase`, with 100 units of Whiskey, Sake and Vodka respectively. The source has **no Openable component**. Only the one-direction static `icon` is used for the loose object. Its visible neck closure is retained as static physical art; no invented cap toggle is bound. The whiskey's conspicuous pale vertical neck seal is an exact source crop, alongside its front label. Source owner: `_RMC14/Entities/Objects/Consumables/Drinks/alcohol.yml`.

### Coffee liqueur and generic whiskey

`DrinkCoffeeLiqueurBottleFull` has 120/120 CoffeeLiqueur and inherits the ordinary **closed** Openable default. Its single visible layer supports actual `icon` and `icon_open` state geometry through the existing rotating sprite-state contract. The open form has a genuinely hollow neck and mouth. Native interactions and saved GenericVisualizer selection remain unverified.

`DrinkBottleWhiskey` is **not** `DrinkWhiskeyBottleFull`. The target's actual inherited solution has 120 capacity and no reagents; its Openable default is false. Although `DrinkVisualsAllFilled` supplies a template `fill-5` layer, the solution owner computes zero fill and hides that layer. The exact draft is therefore **empty and capped**, using `icon_empty`. Its open/empty `icon_open` study is deliberately unbound because the target's layered Fill + Openable composition is not supported by the restricted drinking-glass adapter.

Evidence is not based on a template visibility flag alone. `SolutionComponent.Solution` is marked `AlwaysPushInheritance`; the audit uses the existing source-aware merger. `SharedSolutionContainerSystem.UpdateAppearance` writes actual FillFraction and Color, and `SolutionContainerVisualsSystem` hides the fill at zero when no EmptySpriteName exists. `OpenableComponent.Opened` defaults false, and OpenableSystem initialization forwards the actual Boolean. All these frozen source files are included under `reference/drinks-extra-source/` with verified Git blob hashes.

### Four cans

`CMDrinkCanCola` and `CMDrinkCanMountainWind` inherit closed Openable defaults; the owner explicitly maps both true and false to `icon`. Their top does not claim an observable opening change. Actual source `crushed` states have separate short, buckled physical assemblies. This does not certify that gameplay currently selects `crushed` for these targets.

`DrinkColaCanEmpty` explicitly inherits `DrinkBaseOpenableOpen`: its actual default is empty, open and `icon_open`, with a true offset drinking aperture. `DrinkEnergyDrinkCan` starts closed with 40 units of EnergyDrink. Both have their source `icon` / `icon_open` geometry in the existing single-layer state contract. The metadata records two portable static poses per can, with no invented animation clip. Saved template-state selection can differ from the live GenericVisualizer result; neither is certified here.

### Coffee mugs and number-one mug

`RMCCoffeeCup` and `DrinkMugOne` start empty. `RMCCoffeeCupFilled` contains 30/30 RMCCoffee, whose owner explicitly sets `#704435`. Its comparison composes `icon-0` with source `icon-3` tinted by that color. The other two RMC fill levels are unbound static studies. An inherited top-level `icon` is absent from the RMC RSI, but the explicit Sprite layer is `icon-0`; the missing inherited fallback is not substituted for the actual layer.

All mugs have separate walls/floor/rim and a through-open ceramic handle. The number-one marking is preserved only on the evidenced front. Dynamic fill volume/color, mixtures, spilling, breakage and held appearance remain unsupported. The restricted solution-glass adapter accepts its named clear/drink-glass resources, not these mug resources.

### Cryoxadone beaker

`CryoxadoneBeakerSmall` inherits the ordinary `Beaker` source, **not CMBeaker**. It has no Openable component. The source solution is 60/60 Cryoxadone, color `#0091FF`, so the default source composition is `beaker` plus tinted `beaker6`, with an open mouth and no cap. The empty study exposes its genuine interior. Six world fill sprites and the unused lid asset were inspected, but no invented cap state or live chemistry adapter is bound. The actual graduation columns remain original front-facing pixels.

### Sample tubes

`TreasureSampleTube.RandomSprite.available` selects `blank`, `power`, `reinforcer`, `energy`, `synchronizer` or `stabilizer` on the string layer `base`. Six `randomSpritePrototypes` choices use that exact layer and state. The saved random choice is not assumed when missing. Tube body, translucent envelope, grey closed end plugs and collars are physical parts; there is no guessed fixed-color exact mapping.

### Oil flask and produce

`RMCHelmetGarbGunOil` includes only the loose world `icon`. The flattened flask, rounded edges, shoulder, nozzle and source instruction label are modeled. The four-direction `helmet` overlay is explicitly unmodeled, with no claimed worn-state support.

`FoodPotato` and `RMCFoodGoldenApple` use the inspected `produce` icons. They have volumetric organic bodies and source palettes: irregular potato contours and small skin eyes, and a squat apple with raised twin shoulders, a recessed stem notch, dark lower golden band, short golden heel and a bent forked stem. Growth/harvest, potency changes, in-hand views, eating/cutting and cooking remain unmodeled.

## Physical construction and inference

Bottles have a separate heel, thin segmented body walls, inward shoulder sectors, hollow neck, rolled mouth and a cap only where supported. Shoulder overlap was reduced after orbit review; faceting and fine seams still need fidelity work. Metal cans have actual walls/floors, rolled seams, recessed lids and a true pull-tab loop. Open cans have a cut-out drinking aperture through a segmented lid. Ceramic handle loops and chemistry mouths are empty geometry, not painted holes.

Labels are small unchanged source crops on front tangent strips; no unseen rear label or wraparound lettering was invented. Hidden backs, round sections, depths, shoulder construction, wall thickness, metal embossing and translucency are deliberate inferences. Colors on untextured parts are selected from the actual source composition; this is a palette check, not a lighting or appearance match. Alpha in physical glass/translucent materials is an authored inference, not an assertion that source alpha became a measured 3D material. Software preview stippling is the preview renderer's transparency treatment and is not a source texture.

Whole-sprite extrusion and billboard stand-ins are not used as object geometry. Organic produce, glass optics, meniscus detail, subtle materials, source-projection fidelity and hidden geometry remain draft.

## Verified evidence and limits

- 26 direct GLBs exactly reproduce unchanged `build_models.glb_bytes`; the unchanged command-line builder and immediate `--check` additionally verify deterministic GLBs, manifest and viewer assets. Finite node transforms, buffer ranges, source states/timings and the 128-part budget pass. Maximum authored pose: 78 parts
- All 31 authored poses use untextured RGB values present in their actual source composition
- All 36 crop PNGs match the original source RGBA bytes; 86 fetched files, including 46 PNGs, match their original Git blob hashes
- 31 explicit geometry tests cover 4,243 sample points through mouths, interiors, handle openings, open-can apertures, supporting floors, liquid surfaces, closed caps and the source-specific apple crown/lower bands
- All 31 authored default/state poses form one connected positive-volume part-contact component using direct GLB convex planes and linear-programming intersection witnesses
- Blender 4.3.2 independently imports all 26 final GLBs, with finite vertices and zero mesh-validation repairs

The contact proof is **not** a whole-model manifoldness or surface-opacity proof. Point samples do not certify all possible collision rays. Full Redux contact/support fitting, native/GPU packing, renderer performance, live gameplay state selection and overall fidelity have not been tested. Khronos glTF validation is unavailable; no Khronos result is claimed. No engine or runtime code was modified to admit the models.

Reproduce from the repository root:

    python Tools/three_d/author_drinks_extra_cloud.py
    python Tools/three_d/verify_drinks_extra_cloud.py
    python Tools/three_d/verify_drinks_extra_connections.py
    blender -b -t 2 --python Tools/three_d/check_drinks_extra_blender.py

Detailed evidence: `drinks-extra-cloud-verification.json`, `drinks-extra-cloud-source-audit.json`, `drinks-extra-cloud-contact-checks.json` and `drinks-extra-cloud-blender-import.json` under `Tools/three_d/generated/`.

## Attribution

All sixteen inspected RSI resources declare **CC-BY-SA-3.0**. Keep this document and each original RSI `meta.json` with derivative art. Exact source metadata, complete attribution strings and Git blob hashes are retained in the source audit and frozen source folder.

- RMC alcohol / mug / can / oil art: CM-SS13; RMC whiskey metadata additionally credits edits by Whisper. The original metadata preserves the exact drink, can, handheld and helmet-garb source URLs
- Generic coffee liqueur, whiskey and number-one mug: CEV-Eris, drink sprites at commit `f7aa28fd4b4d0386c3393d829681ebca526f1d2d`; in-hands credited to TiniestShark
- Generic cola and energy can: CEV-Eris food sprites at commit `9c980cb9bc84d07b1c210c5447798af525185f80`; in-hands credited to Tiniest Shark
- Ordinary beaker: CEV-Eris commit `740ff31a81313086cf16761f3677cf1e2ab46c93`
- Sample tubes: tgstation chromosome sprites at commit `fb1012102257b7b0a08d861fd2b8ba963c416e93`
- Potato and golden apple: vgstation13 commit `1dbcf389b0ec6b2c51b002df5fef8dd1519f8068`; metadata credits Chaoticaa for produce/growth art, mubururu_ for in-hands and Prole0 for in-hand modifications

The geometry and exact PNG crop adaptations retain the applicable source attribution and share-alike terms. No ownership transfer or fidelity approval is asserted.

## Golden-apple silhouette refinement

The nine-part apple was refined before the next delivery freeze. Its squat golden body now has two integrated, full-depth rounded shoulder volumes and an actual recessed point in front of the stem, rather than a nearly spherical silhouette. Source `#AB401E` and `#E5970D` form the exposed dark lower skirt and short golden heel. Four explicit solid/cavity tests check those regions. All other drink-family GLB bytes remain unchanged. The updated apple, combined review sheets, direct-export evidence, Blender import hashes and delivery manifest were regenerated. Hidden contours and material response remain draft.

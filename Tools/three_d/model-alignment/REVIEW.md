# Model alignment review — 2026-10-08

## Integration into CMU master

The current master names for the foreman uniform, engineer outer suit, UPP fire coat,
and Raider uniform/armor replace five retired prototype IDs in the model references and
worn bindings. Their source artwork is unchanged. The retired drug-dealer satchel shares
its model with `RMCSatchelBlack`, which is now its reference; the new drug-dealer bag has
different artwork and retains its sprite fallback. Model IDs and texture states remain
stable. The six affected GLBs were regenerated, and the attribution file retains the
original snapshot IDs as `sourceSnapshotPrototypes`.

The review below describes the original standalone snapshot.

This pass reviews all 1,676 existing drafts, corrects eight visible source mismatches, and adds rigid worn and held equipment to the native first-person prototype. The library now contains **2,476 draft assemblies**: 1,676 existing models, 799 worn assemblies and one unloaded-rifle variant. None has been promoted to full fidelity approval.

## Source and geometry review

The original library was inspected in 70 contact sheets showing source artwork, front/back oblique views and undersides. The worn candidate library was inspected in 39 sheets; 120 fragmented weapon/tool reconstructions were rejected, leaving 799 complete assemblies. Guns and tools use their separately authored solid models. The contact sheets are local review evidence under `.codex/model-style-review/`; selected reproducible results are retained here.

[Eight corrected models](style-corrections.png) cover the M41A pulse rifle, open/closed maintenance door, MPS console, mapping console, two sensor consoles and wall television. Changes follow source silhouettes, tan/gray equipment colors, orange horizontal door warnings, screen layouts and the television's blue CRT and left-hand indicator. Consoles have closed cabinets, rear service covers and undersides. Screens remain static approximations of animated artwork. Geometry keeps existing map pivots and wall mounting planes.

[Three assembled outfits](equipment-outfits.png) compare the modeled pieces from actual CMU starting gear; [the accompanying list](equipment-outfits.json) identifies every included item. Bodies and unmodeled pieces are omitted from this offline image. Worn geometry uses the composed front, back and side source silhouettes, original layer tints and a palette sampled from those pixels. Small details are quantized to 3–12 source colors. Outer clothing, belts, bags and neckwear have slightly expanded horizontal bounds to separate overlapping layers. Hidden construction is inferred; these are rigid standing poses, without cloth deformation or skeletal animation.

The browser library now also tracks 935 previously missing comparison images and 800 new equipment comparison images, so its source references survive a fresh checkout. Twenty-four layered review references now include all static source layers and tints. Four console references intentionally show the first powered frame. Original source attribution, licenses and hashes are retained in `SOURCES_STYLE_ALIGNMENT.json`, `SOURCES_EQUIPMENT_REFERENCES.json` and `SOURCES_WORN_EQUIPMENT.json` beside the GLBs.

## Equipment coverage and runtime behavior

The [coverage inventory](equipment-coverage.json) traces 476 starting-gear definitions, 603 loadouts and 77 role-loadout groups, including filled containers, ammunition and attachments. It includes optional/hidden roles; these are unique prototype counts, not spawn frequencies.

| Coverage | Unique item prototypes |
| --- | ---: |
| Direct equipment choices | 1,743 |
| Including nested contents | 2,224 |
| Exact dropped/world model | 184 |
| Missing exact dropped/world model | 2,040 |
| At least one worn pose | 909 |
| At least one held pose | 165 |
| Missing referenced prototypes | 0 |

The 166 held profiles retain authored geometry, grip pivots and orientation. The pulse rifle has explicit loaded/unloaded geometry and accepts the five pixel-identical folded-stock camouflages. Live magazine and attachment layers choose a compatible pose; unknown attachments, dyes, transforms or animated layers retain the original sprite. The resource-path comparison accepts both texture-relative and `/Textures/`-rooted references.

Equipment uses replicated inventory/hands and interpolated wearer positions. First-person held items follow local camera motion every frame. Left-hand placement translates the model without mirroring its markings. Only successfully replaced sprite layers are hidden, and their visibility is restored after atlas rendering. Picking and wall/prop occlusion use the transformed geometry. Equipment has a separate budget of 64 complete assemblies, up to 512 parts each; remaining wearers/items retain sprite layers. Ordinary world-model budgets are unchanged.

## Verification

- Release builds through the isolated repository test wrapper: **148 focused unit tests passed**, including actual library preview admission, camera/grip transforms, ray targeting and existing appearance adapters.
- **Two connected integration tests passed**: full model-library loading on server/client; equipped clothing, held tool, unsupported dye fallback, unequip replication and pulse-rifle magazine insertion selecting different geometry.
- **28 Python checks passed**: 26 existing model/export regressions and two solid-hull/material-coverage checks.
- Native Robust shader generation, OpenGL compilation and pixel checks passed both desktop GLSL variants, including attachment rotation, part indices above 255 and equipment in front of/behind world geometry. See [shader evidence](native-shader-validation.json).
- Launch follow-up: the native Release client passes content-sandbox verification and connects to the local server. Resource normalization uses string offsets to avoid an unverifiable span return; 29 focused appearance tests pass after this correction. This confirms startup, not movement/animation acceptance.
- Final deterministic export and Khronos GLB validation results are recorded in [verification.json](verification.json).
- An isolated 640×360 GPU fixture on an RTX 4070 Ti SUPER measured about 4.3 ms for one worst-case 512-part attachment filling much of the screen. [Raw timing](isolated-gpu-timing.json) is **not a game-frame-rate measurement**.

## Remaining work

The 2,040 missing exact world models remain an explicit authoring queue. Some have worn geometry but no dropped-item equivalent. Unsupported worn appearances retain sprites; prone/dead/scaled actors also retain their sprite presentation. Rigid equipment does not yet follow walking limbs, wielding poses, recoil or ragdolls. Unmodeled first-person held items remain represented in the existing inventory HUD. The 64-assembly budget can mix 3D equipment with sprite equipment in crowded scenes. These changes have not had a full interactive gameplay/movement acceptance pass, and hidden-side fidelity remains inferred from sprite evidence.

## Reproduction

Use the checkout's Python environment and the repository's isolated .NET wrapper. Do not reuse another checkout's build outputs.

```text
python Tools/three_d/equipment_inventory.py
python Tools/three_d/author_worn_equipment.py
python Tools/three_d/author_held_equipment.py
python Tools/three_d/compose_equipment_references.py
python Tools/three_d/build_models.py --no-review
python Tools/three_d/build_models.py --check --no-review
python -m unittest discover -s Tools/three_d/tests -p test_models.py
python -m unittest discover -s Tools/three_d/tests -p test_equipment_hull.py
node Tools/three_d/validate_glb.cjs <absolute-path-to-gltf-validator-package>
python Tools/three_d/validate_native_shader.py --powershell <PowerShell-7-executable>
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.Tests -Configuration Release -Filter 'FullyQualifiedName~CMU3DLibraryBudgetTest|FullyQualifiedName~CMU3DEquipmentTransformTest|FullyQualifiedName~CMU3DSpriteAppearanceTest|FullyQualifiedName~CMU3DDoorAppearanceTest|FullyQualifiedName~CMU3DDoorButtonAppearanceTest|FullyQualifiedName~CMU3DLightAppearanceTest|FullyQualifiedName~CMU3DBarricadeAppearanceTest|FullyQualifiedName~CMU3DWallPaperTest'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Configuration Release -Filter 'FullyQualifiedName~CMU3DEquipmentAppearanceTest|FullyQualifiedName~CMU3DModelLoadingTest.ModelLibraryLoadsOnBothSidesWithValidGeometryAndReferences'
```

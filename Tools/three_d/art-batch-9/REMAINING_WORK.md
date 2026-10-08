# Remaining work after the source-guided art pass

## What this snapshot establishes

The finite original missing-physical queue has been worked through. All 473 prototype types now have either exact default-pose draft geometry, conditional-choice geometry, or explicitly unbound physical studies. This is not a claim that Stable Garrison Redux is fully modeled, approved, or playable in 3D.

- 462 original missing types have exact staged draft references, representing 951 committed Redux inventory records.
- Four conditional types have geometry: FloraTree and FloraTreeLarge retain twelve existing choices; FoodEgg has two new choices and TreasureSampleTube six. Their eleven saved records are not counted as exact mappings.
- Seven original types, representing 280 saved records, have physical studies without live binding: FloorChasmEntity, CMUMultiZStairsFlight, CMUMultiZStairsNoPreview, AU14CorporateASRSElevator, CMCargoElevator, CMCargoElevatorGovfor and VehicleLift.
- The separate 110 inherited-candidate audit covers 483 Redux records. Eighty types/422 records received source-specific draft replacements; 27 types/53 records retain source-compatible existing candidates; three types/eight records remain integration-unresolved.
- Those three inherited cases are CMUZLevelHatchThroughDown, RMCLadderHatch and CMUZLevelLadderThroughDown2. Two shared physical studies represent their hatch/through-ladder forms. They add no exact bindings.
- This cumulative addition contains 637 canonical draft assemblies and 1,021 added surfaces. Pose, direction and unbound variants do not inflate unique target counts. Twenty-nine portable GLB clips are present; no live playback is certified.
- A separate arbitrary-mesh helmet study supplies a continuous thin crown and real inner cavity. Its editable Blender mesh is not part of the canonical assembly or mapping count. High-resolution (44,724 triangles) and lower-poly (4,716 triangles) derivatives are distinguished in their own evidence; they are one design with two resolution variants, not two unique modeled target types.

## Remaining integration work: needs runtime/schema engineering

No engine, runtime, schema or saved-map edits were made. These tasks are outside the authorized art-only implementation:

1. Chasm neighbor topology: native floor-opening validation currently requires one source state/direction and a one-tile interior opening. Chasm sprite smoothing uses neighboring corner/state combinations. The isolated, straight and corner cliff meshes are physical art studies, not a working native hole/neighbor solution.
2. Five-by-five elevator apertures: current slab opening bounds lie within one unit tile and require anchored, floor-placement, unit-grid, zero-ground-offset data. A single five-by-five platform owner cannot cut all affected floor tiles through that metadata. Lowered shaft and raised deck art exist; multi-tile cuts and state routing need integration.
3. Multi-Z stairs: the logical HighGround profile is not consumed as a native physical rise path. Both source-specific stair forms have actual treads, but the required physical elevation/visibility/transition contract is unresolved. Adding a large static stair mesh does not establish correct travel between levels.
4. Through-hatch/ladder: the existing LadderCompound path is specialized to CMUZLevelLadderThroughDown2 and its owner-plus-two-companion contract. Independent hatch/ladder meshes must be reconciled with that compound and its apertures; binding a duplicate visible ladder would be incorrect.
5. Smooth custom shells: the current cmu3DModel contract consumes named fixed primitives and associated surface metadata, not arbitrary external GLB vertex/index meshes. The helmet .blend and GLBs need a separately designed importer/mesh contract to enter the native renderer. They remain useful standalone art.
6. Live appearance: portable state scenes/clips and source-default geometry do not implement every layered sprite selector, power/broken/filled state, destruction/consumption state, worn/held pose, rig, skinning or game-driven animation. Existing code may support some selections, but this snapshot has not established those live paths.

Evidence: native-source-contract-check.json; original family source notes; topology-studies source notes and validation; inherited-structure-audit.json; helmet SOURCE_AND_LIMITS.md. Source inspection is not a native execution test.

## Remaining context/acceptance work: needs a complete game workspace

- Load the entire legacy library plus these additions, rebuild normal global manifests/coverage, then perform real native admission, picking, visibility and performance tests. The source scene budgets are 8,192 default/32,768 extended parts, with separate spatial-reference limits; passing each <=128-part model does not establish full-scene admission.
- Validate all StableGarrisonRedux maps -2 through +4, including saved overrides, orientation, wall/floor contact, stacked objects and elevations. This workspace is a bounded source subset obtained via authorized connected reads. Only a bounded portion of map -2 has contact evidence; the larger full-map blobs and native build are not present here.
- Resolve the two demonstrated medical/divider contacts at original map -2 pivots: console entity741 versus divider2386 (nine convex-part intersections), and scanner746 versus divider2387 (34 part intersections). Six other medical placements clear only the tested neighboring geometry. No source-backed offset/shape correction was found; the saved transforms were not silently changed.
- Check maintenance-cover voids against actual floor rendering; valid mesh holes may still be occluded by an unchanged floor slab.
- Run the Khronos glTF Validator and the repository's complete validation pipeline. Blender import/mesh checks and buffer/accessor checks are supplied, but they do not replace these tests.

## Remaining art work and quality limits

These are art/review tasks, not reasons to fabricate bindings or declare completion:

- The canonical primitive riot helmet remains scalloped; the separate smooth thin-shell study resolves that geometry issue only outside the current native contract. Loose headwear also retains low-poly facets/roof seams, and party-hat stripe bands remain approximate. Those canonical shapes have not received final fidelity approval.
- Hidden backs, interior construction and many depths are inferred from limited 2D pixels. The plain reverse of the Bobda cans is explicitly inferred; no unsupported label wrap was invented. Additional reference or art-direction decisions are needed to certify unseen details.
- Source-grounded default poses are not complete alternate-state, worn, held or rigged character art. Any full-state production pass needs an explicit state inventory and compatible presentation/binding plan, rather than counting the default as every state.
- The pre-existing 1,039 drafts were byte/identifier checked, not comprehensively visually approved. The 110 inherited-candidate audit examines relevant source identity/default composition and selected parent geometry; it does not approve the entire original library. The 27 compatible retained cases are still draft candidates.
- All 637 added canonical assemblies are drafts. Source-facing/orbit reviews and localized corrections are evidence, not automatic final art acceptance. Low-level checks cannot prove that every silhouette, material or inferred depth is artistically correct.

## Exact delivery boundary

No original queue target is left without either default/conditional geometry or a useful physical study. Ten exact-binding cases remain blocked by topology/compound integration (seven original plus three inherited). Four conditional-choice types should remain choices rather than guessed fixed defaults. Custom thin-shell meshes remain separate unbound artifacts. Further claims of a fully modeled, source-faithful and working Redux require the integration, full-context tests, alternate-state work and art approval above.

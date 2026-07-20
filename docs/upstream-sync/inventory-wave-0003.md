# SS14 upstream inventory: wave 0003

Audit date: 2026-07-20

- Pinned baseline: `59633f6dc50e77dda8cefa344d87c7b01e06a810`
- Pinned target: `40ca2c7f90d11d27be5457d177c133f0947d1c08`
- Range: ordered first-parent commits, zero-based indices 0400 through 0599
- Columns: index | full SHA | exact upstream subject | disposition | core-system areas | rationale

`Ported (CS-####)` links an accepted core-system change to the durable audit;
`Ported` is used for accepted non-core cleanup. An `AlreadyPresent (CS-####)` entry
links an audited equivalence rather than a newly applied behavior change.
`Port candidate` and `PortCandidate` are equivalent lane-local labels for retained
target behavior that still needs integration. `Already present/equivalent` and
`AlreadyPresent` mean CMU already has equivalent behavior. `Dependency-blocked/deferred`
and `Deferred` preserve downstream behavior pending focused reconciliation.
`Superseded` means a later target change replaces the commit. `Non-code/no-op` and
`Irrelevant` identify commits with no standalone behavior to port. Lane-local labels
are retained so future audits can trace each classifier's original decision.

~~~text
0400 | fd52f698c743d240573e37d34219bc109736362c | Predict PoweredLights (#36541) | Deferred | Physics, Interactions, GameTicking | The large server-to-shared powered-light prediction migration intersects CMU power, appearance, and light state and needs focused reconciliation.
0401 | 291a6c98086adf964510bb169181dd4a4381c786 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0402 | de7486b8dba0481c1abc676f05e32beeaa67ea6a | Fix ReagentQuantity Equality check (#39574) | AlreadyPresent (CS-0003) | Chemistry | The audited downstream equality contract already compares both reagent identity and quantity correctly.
0403 | 915d8152542f45bd197965147fff65807393e7f7 | feat: make ReagentId hash by value (#39494) | AlreadyPresent (CS-0005) | Chemistry | CMU already has the audited value-based hash adapted to its order-insensitive equality contract.
0404 | c76d21e9735b9fff29ae30b5cf6f8c43407bbabe | Fix antag objective assignment (again) (#39565) | Superseded | Gamerules | Later target objective and mind-filter changes replace this intermediate assignment fix.
0405 | 9b6cb79fa2e2d554480c1f617529ad3a3d7b4484 | Fix dock radar colours (#38942) | Ported (CS-0118) | Movement, Physics, Interactions | Accepted as downstream commit c84e937df2; docking-port colors now survive the BUI round trip and render in both radar controls.
0406 | 8b76ace136a985a99d22f3c768aeaf0274facfa5 | Slightly shorten examine text for splashing a solution with a melee attack (#39428) | Ported | Chemistry, Interactions | Accepted as downstream commit 47bc96d588; CMU now uses the retained shorter spillable-attack guidance.
0407 | 39cb27fe2171b55cf236d10bfc801350f7bdd38d | Borg hands & hand whitelisting (#38668) | Deferred | Interactions | The broad virtual-hand and whitelist architecture must be reconciled with RMC borg modules and inventory behavior.
0408 | 6f2699f52108ba9b5b7a485d1abe01bdc8e48fff | Automatic changelog update | Irrelevant | — | Generated changelog only.
0409 | 1901fafc501ff734617f42f3b71e16044334a599 | fix: fix lights not always enabling correctly (#39585) | Deferred | Physics, GameTicking | The fix is coupled to the unported predicted powered-light state introduced by 0400.
0410 | dcfdd8914a43a13c4d396f61d9a2b0f63acc5edf | Automatic changelog update | Irrelevant | — | Generated changelog only.
0411 | 024301e69846711c031033f4e4486140350b4a79 | RandomChance trigger condition (#39543) | Deferred | Interactions | This condition depends on the shared keyed trigger framework absent from current CMU.
0412 | cea8dea005e18be2656632bc2036550e7d0d2245 | Fix: Break do_after if target/tool becomes inaccessible (#35079) | Superseded | Interactions | Upstream reverted this implementation in 0420 rather than retaining its accessibility behavior.
0413 | 1f4dfcdcf94dc327b5de2d016dba2fe18c3acbe2 | Predict GetVerbsEvent in PowerSwitchableSystem (#39589) | Deferred | Physics, Interactions | Shared verb ownership depends on the broader power-switch prediction migration and CMU interaction reconciliation.
0414 | 2743dcf67f027a9f452d16b4a63dc0be7edba49b | Move mind role components to shared (#39606) | Deferred | Gamerules, GameTicking | Moving role state across the network boundary affects RMC antags, minds, and later predicted-role work.
0415 | 99ad34ed06985e665bb24fcc1fc9d92eece1fa1b | Disable the lock/unlock verb if we can't do that (#39605) | Ported (CS-0109) | Interactions | Accepted as downstream commit 10bd291a1d; quiet lock-attempt checks now disable rejected alternative verbs.
0416 | 2cb4e01019e63992ed40a30399f220b665969549 | StaminaDamageOnTriggerComponent (#39607) | Deferred | Medical, Interactions | The component is retained but depends on the absent shared keyed trigger architecture.
0417 | cf79477de329924918226e8a4bf6cf56003eed76 | Weapon Resizing (#36473) | Deferred | Shooting, Movement, Interactions | The broad weapon-size, wielding, slowdown, and prototype changes require RMC combat and inventory review.
0418 | 26e407d35cad934906130388610344d8326af46e | Automatic changelog update | Irrelevant | — | Generated changelog only.
0419 | b89a406735d0268cfb309f14ffd3e2beff9d33af | Compact Security Jetpacks (#39569) | PortCandidate | Movement, Interactions | Target-final retains the compact security jetpack prototypes and assets that are absent from CMU.
0420 | 0a991593f5d9e17691005b845c163c8bcc983033 | Revert "Fix: Break do_after if target/tool becomes inaccessible" (#39617) | Superseded | Interactions | This revert only cancels 0412's intermediate approach; later target behavior should be audited from its final implementation.
0421 | b427e7e8d4d49500b18e60c24f6b568be3600530 | fix lightbulb color (#39623) | Deferred | Physics | The retained color correction overlaps the predicted powered-light and appearance-state chain.
0422 | b8b37f44ac641a9052c233f5d8e8687c399fe1dc | Automatic changelog update | Irrelevant | — | Generated changelog only.
0423 | 1e54f4ca5f837a6f5b7aba86d3e8ac8dcbe28986 | In Memoriam - Memorializing those who've passed within the SS13+SS14 community (#39621) | Irrelevant | — | Memorial content has no standalone CMU core-system behavior to port.
0424 | 0872c4d7e109039231371809dee1e00147f3a871 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0425 | 770dc68a48ad0d2135bba8103428713cf9243f7c | Add trigger-refactor components and systems: Batch 1 (#39391) | Deferred | Interactions | This is a foundational shared-trigger migration whose APIs are absent from current CMU.
0426 | 72e302dd469cdcb4500d9be883e76b17da8acf54 | Add myself to codeowners file (#39636) | Irrelevant | — | Repository ownership metadata only.
0427 | d939b4dec795ccdc5d94578153d14716ed86f753 | RemoveComponentsOnTrigger, ToggleComponentsOnTrigger (#39639) | Deferred | Interactions | Both behaviors require the shared trigger-refactor foundation from 0425.
0428 | 114d00d1afdefaf496a50a36b1c1ff28d21a7bcb | Rebalance advanced Brute chems, and more (#39472) | Deferred | Medical, Chemistry | The retained medicine changes are a broad balance and metabolism decision requiring RMC medical review.
0429 | 09a8b789191627f0981b514786015974662e258b | Automatic changelog update | Irrelevant | — | Generated changelog only.
0430 | 96cc6a77851068212bf14a1d920ad8e922188c83 | Meat spike conjugation error (#39651) | Ported | Medical, Interactions | Accepted as downstream commit edb62ed3b5; the kitchen-spike popup now uses the correct conjugation.
0431 | 2b31fa98c9eb3b282888e6f46f24ca3da4bfb129 | Add container-related triggers (#39647) | Deferred | Interactions | Container insertion and removal triggers depend on the unported shared trigger architecture.
0432 | d4f50c7f0a6a403ae304637a2a8c6c435d7a0967 | Animal organs now prefixed with 'animal' (#39228) | Superseded | Medical | Later target body and organ prototype reorganization replaces this intermediate naming pass.
0433 | 7a31e3c1f8890d7fdacc537e2bfa9e2699e08082 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0434 | 7a3026b4f89888d6d79a047b902247a3348b63ba | Throwing triggers (#39650) | Deferred | Interactions, Physics | Throw-start and land triggers depend on the shared keyed trigger systems absent from CMU.
0435 | 2b91f965a2aa68856e7c88c29d934eb67c995a63 | Add integration test for MobPriceComponent (#39524) | Ported | Interactions | Accepted as downstream commit 3c137b75c6; the fancy crown now carries its retained MobPrice component value.
0436 | 5bfd1b180a60d66a445e34e035ea7b8baf2b6dfb | Banana peel headgear fixes (#39457) | PortCandidate | Movement, Interactions, Physics | Target-final retains the wearable peel fixes and assets; CMU still has the older headgear behavior.
0437 | 766e7e462c424b6f64788fb7f3c7f425a20b3011 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0438 | d08252facb5c1698539e07a77264cff3c88442f0 | Fix unbuckle admin logs only showing the ids and not names of the entities involved (#39655) | AlreadyPresent | Interactions | RMC's current unbuckle logging already formats the involved entity identities rather than raw IDs.
0439 | b2c505df6a81da1a7062d01bf9df3fe1548d8c2e | Fix instances of predicted randomness (#39661) | Superseded | Interactions, GameTicking | Later target prediction and deterministic-random changes replace this mixed intermediate cleanup.
0440 | f23e8c286153b5742de258a4e722803a1a4fba78 | Multiantag Gamemode (#37783) | Deferred | Gamerules | The large multi-antag preset, role-selection, UI, and objective changes need a dedicated RMC gamerule migration.
0441 | 890ac9f64582329b61be307f4080cff3e66a4236 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0442 | a8d6dbc3241c880f846a7abba8bd330788df9fc1 | Added button and manager for in game bug reports (Part 1) (#35350) | Deferred | — | The retained UI and external-report manager require deployment, privacy, and fork-service decisions.
0443 | 3e84efb05172f6ddd41c0d03c70a9e17c4eee1f4 | Tabs in the Credits window only populate once (#39667) | Ported | — | Accepted with a CMU-compatible adaptation as downstream commit a469d70369; repeated tab changes no longer duplicate credits.
0444 | d4f8568e50c3d5941830198648571a24e5d1f499 | Added baby and cube hair (awesome) (#39680) | PortCandidate | — | Target-final retains the hair prototypes and binary assets that CMU lacks.
0445 | 6af3698c1f21332e86cea83f3c2920907242be9b | Automatic changelog update | Irrelevant | — | Generated changelog only.
0446 | 05aad3bfd29b8915c8af7df0e69fd00cb7c3273d | Expand soap making, but better (#39303) | PortCandidate | Chemistry, Interactions | The retained recipes, reagents, constructions, and soap content are absent and can be reconciled as a focused feature.
0447 | 0947e2cd2248e3bbe6d2f8b7592aff72e890afa2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0448 | 71f5c2d665b124845d6e0b73f969d70d995999bd | Equip and unequip triggers (#39675) | Deferred | Interactions | Equipment trigger components require the shared trigger-refactor chain.
0449 | 529d7ff2922788c13997a26b61e384119116648d | Predict MessyDrinker (#39660) | Deferred | Chemistry, Interactions | Shared prediction ownership must be reconciled with CMU's older food/drink systems and RMC ingestion behavior.
0450 | bd05e10a2e9a11317de212a5708a4f1c4ba02307 | Merge staging into master (#39694) | Ported (CS-0181) | Chemistry, Interactions | Ported the merge's retained material-reclaimer switches and disabled material recovery on the industrial grinder, preventing its Extractable whitelist from recycling output indefinitely.
0451 | 201bc6cc5ce8b6132f663c501a96866478acf26b | Swap ExudeGasses and ConsumeGasses (#39688) | Ported (CS-0110) | Chemistry, Physics | Accepted as downstream commit b3cace9c56; each plant mutation now updates its namesake gas dictionary.
0452 | 6e8260cf3fe0ac11a05ae212c0492ba430c8e30c | Trigger for OnInteractUsing (#39692) | Deferred | Interactions | This interaction trigger depends on the absent shared keyed trigger framework.
0453 | 01f4f0cf1492a80eee16dfb88a09078b7c49729f | Increase the bananium horn use delay (#39674) | Ported (CS-0111) | Interactions, GameTicking | Accepted as downstream commit aa7889e288; the bananium horn now has the retained three-second use delay.
0454 | 1b16a837484d1230508833a9e78f65f1c60f2906 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0455 | 5bf44ea0335e8012f0ba17bd2521d55689f85793 | Update Credits (#39697) | Irrelevant | — | Upstream credit snapshot only.
0456 | 5ea928863f2aa00455ba040487f4c78ee33e4495 | Cleanup warnings in ChatSystem (#36773) | Irrelevant | — | Warning and dead-code cleanup without standalone behavior.
0457 | 514c28137ccd42a638ece921acafa3f692c3e8d6 | Fix typo and make capitalization consistent in fax names (#39455) | Superseded | — | Later target map and fax-prototype changes replace this intermediate naming edit.
0458 | 367156fba3150a1e71c2a3b0c5e39f59969756dd | Automatic changelog update | Irrelevant | — | Generated changelog only.
0459 | 8e34228309e85d2db45e1ce04baf47318df6f94e | Packed Station - North East Overhaul (#38339) | Superseded | — | Target-final Packed Station contains later map revisions; reconcile its final snapshot instead.
0460 | 9f83cc7671a2c16cbca1c90c771bd0171f320af9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0461 | d58ef22d62795c1c4393c1eb09d33c1ff78087c6 | Make diagonal grilles transparent (#39580) | AlreadyPresent (CS-0045, CS-0046) | Shooting, Medical, Physics | CMU already carries the audited specialized-window tags and diagonal-grille GlassLayer behavior.
0462 | 5630bf6bde3cb6cc5a2f4817ed463c3e10b54619 | Removes duplicate CE and paramedic jumpsuits (#39520) | Deferred | — | Uniform deletion and loadout cleanup require review against divergent RMC jobs and clothing assets.
0463 | 497a5956a046070575b1ea747c51a17955a2bff8 | fix cl (#39706) | Irrelevant | — | Changelog correction only.
0464 | 26badb79142fb7227c1ed1897595e40ad6bfd8d9 | Update OpenTK to latest (#39227) | Superseded | — | Later dependency versions replace this package update.
0465 | 3d35435747bb6080a2cdcfe65c6d34afcfdf2be0 | Allows disabler, practice disabler, disabler SMG, and practice laser rifle to be used by pacifists (#37164) | Ported (CS149) | Shooting, Interactions | Ported after CS148 separated lethal and practice laser inheritance; only harmless practice/disabler weapons receive the permission marker.
0466 | af8295413bbea92f1a72f262392ec149e25bc075 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0467 | 02f37a8eecaf1ad3fa0ae1b7c968e2754c569f6b | Allow hamster cages to sit on tables (#37953) | PortCandidate | Interactions, Physics | Target-final retains the cage asset and placement changes that are absent from CMU.
0468 | 79072d7d6a213e8a980fbfa4bd9f855a7a2b4cdd | Automatic changelog update | Irrelevant | — | Generated changelog only.
0469 | 8034cabbaeed60f7f476d1be193c5d476eac8309 | Fix error when deleting a toggled `ToggleableClothingComponent` (#39191) | Ported (CS-0112) | Interactions | Accepted as downstream commit 141365833e; terminating toggleable clothing now skips teardown-time reinsertion.
0470 | 81aef0fb1c6f3bd232238a2af9844327b27c8a80 | update oasis -- AI rework (#39592) | Irrelevant | — | SS14 Oasis map-only change is outside RMC map usage.
0471 | dc86665c2420549ddc3402d831576c40940d52fb | Stable (#39712) | Deferred | Movement, Medical, Interactions, Physics, Gamerules | This broad stable merge must be decomposed and reconciled rather than ported as an aggregate.
0472 | 9d32e7db4ea8a5d5f35cc5f0dcf9980159b14ca0 | Atmos Firesuit Vox sprites (#39705) | Deferred | — | Binary Vox equipment assets require comparison with RMC's customized species and suit art.
0473 | e16bca5b181c4d72070513f675cd0d320f7b04ff | Hand pickup and drop triggers (#39663) | Deferred | Interactions | Hand trigger behavior depends on the unported shared keyed trigger architecture.
0474 | d9f125787e64e5931ed210083611bc0210311089 | Teaches tacos how to spell (#39717) | AlreadyPresent | — | Current CMU already contains the corrected taco text.
0475 | c59f7a53633f69eb091fe43aa05cda705e189257 | Fix ninja spawning with jetpack internals (#35067) | Ported (CS-0114) | Interactions, GameTicking | Accepted as downstream commit 2ac6ad7617; composite loadout completion now raises once after all equipment is applied.
0476 | ba44a88f075f4f6c44098357b449256d79f5ba6f | Automatic changelog update | Irrelevant | — | Generated changelog only.
0477 | 1bed929298f0ac87a449cf2449e3a9748f782b54 | Add new nukeops spawners! (#39088) | Deferred | Interactions, Gamerules | New operative spawners, roles, and prototypes need reconciliation with RMC ghost roles and nuke operations.
0478 | f525bdbb834479475aa94ef445a052a3042e4b5e | Rebalance nukie planet (#39090) | Irrelevant | — | SS14 nuke-planet map balance is outside RMC map usage.
0479 | 2ecc3b85c4f04463d697517e13320e7d948542a5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0480 | 14f949c3112e903cd9adc1a8355a3aa3a9c1fcab | Jumpability collisions (#39710) | Deferred | Movement, Physics | Collision-based jump eligibility intersects RMC xeno, stun, and movement behavior and needs a dedicated migration.
0481 | 9b9ea3b40de9b149cafa40314afa3248f3aee018 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0482 | c0b739d1dc13cfcfe2ba78ad575e7824bcfa19bd | Remove StaminaResistance from cardboard armor (#39727) | Irrelevant | Shooting, Medical | Balance cleanup for the deferred cardboard feature has no current CMU behavior.
0483 | 0a1c17cbc81b319cf4dcf88f3b36becbaae35c11 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0484 | ac0c1d518e895fa6863e37cd27220f5118e2032a | PR stable to master (#39729) | Deferred | Movement, Shooting, Medical, Interactions, Physics, Gamerules | Broad aggregate merge requires constituent-level reconciliation and must not be applied wholesale.
0485 | b31cc6010011a1ed6961884f5d78e1017aea16a8 | add myself to codeowners for the trigger system (#39725) | Irrelevant | — | Repository ownership metadata only.
0486 | 342fc84f169274aa90b0890ac8245618a82a061c | Hotfix: Camera offset for "Separated Chat" HUD fix & targetting fix (#35087) | PortCandidate | Shooting, Interactions | Target-final retains the camera-offset correction while CMU still has the affected separated-chat targeting path.
0487 | d38d2e209a97885118dd9e87fbbfac4d4843e728 | Rebalance infiltrator (Nukie ship) (#39091) | Irrelevant | — | SS14 nuke-ship map balance is outside RMC map usage.
0488 | fdfdecf57b587103a71e02e12725ba99ac2859be | Automatic changelog update | Irrelevant | — | Generated changelog only.
0489 | aa4ca4199a70c606192acf32cc06a16236da9dc0 | Minor fix to give Lone Operatives the correct roletype (#36521) | Irrelevant | Gamerules | SS14 lone-operative role balance is not used by RMC's role flow.
0490 | 5a5b81f7dc8434a2ca5000cb3e3a4e031e56c4b2 | Fix rebinding keys crashing the game (#39732) | Ported (CS-0113) | Interactions | Accepted as downstream commit 5ef459952c; menu controls now tolerate temporarily unbound keys.
0491 | fedba2425bf94f78bcdd72f7d45835152e7f6d38 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0492 | d737e39a98a88d07bee24106901206cfc7d8fbb4 | cleanup LockOnTriggerComponent (#39720) | Deferred | Interactions | The cleanup assumes the shared keyed trigger framework absent from CMU.
0493 | 6b73d320b9eb35cab7cbcc9301759fa0ff395a80 | [NEW STATUS SYSTEM] Drunkenness, Stuttering, Slurred Speech, and Bloodloss (#38678) | Deferred | Medical, Interactions, GameTicking | This major status-effect migration intersects RMC damage, speech, metabolism, and prediction.
0494 | 87705e0335620828d11ba95520a72de80bd1e0ee | Automatic changelog update | Irrelevant | — | Generated changelog only.
0495 | 47cf99fb7e34888a3d4798122620e08f721e8f21 | Fix medipen injectors not respecting entity identity (#39735) | AlreadyPresent (CS-0021) | Medical, Chemistry, Interactions | The audited standard and RMC hypospray paths already use the target's presented identity.
0496 | 020f25139c7a3e22bd80f7936efe5debae861e51 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0497 | 3323a17b3d3f5830e8e7a6c6418019c373b9d017 | predict StackSystem GetVerbsEvent (#39741) | PortCandidate | Interactions | CMU still owns stack verbs server-side while target-final retains their shared predicted implementation.
0498 | 8768074706197e2b59795faa125df4f4eebb68e4 | More informative changeline devour armor text (#39745) | Deferred | Gamerules | The localization improvement depends on the absent Changeling devour feature.
0499 | a491718be6acd6b30753798fa87c267f2368fb78 | Rebuilt Box Armory (#39733) | Irrelevant | — | SS14 Box map-only change is outside RMC map usage.
0500 | 438090a5054aabf23237ca37a40c282a7eda4243 | Cleanup warnings: CS0414 (#39748) | Irrelevant | — | Compiler-warning cleanup only.
0501 | be2eeb3cb14795cf24862263ff868a53a14ffe71 | Cleanup: Un-hardcode reagents standout (#39752) | PortCandidate | Chemistry, Physics | Target-final retains data-driven standout reagents and prototype-reload caching; CMU still hard-codes the puddle reagent list.
0502 | de240e1739b09940a318b53a1eb067383b4b83ce | Xenoborgs part 5 (#37068) | Deferred | Shooting, Medical, Interactions, Gamerules | This large xenoborg feature stage adds combat, silicon, role, module, prototype, and asset dependencies absent from CMU.
0503 | 021adbe1e1e5df2146ec51fc2fdaa5bd0d9e08d7 | New Feature: Kitchen spike rework  (#38723) | Deferred | Medical, Interactions, Physics | The retained rework changes impaling, body handling, construction, audio, prototypes, and prediction and needs an RMC-specific batch.
0504 | dcbbce52b6c8b64302cb8730fd53fb358f100bb4 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0505 | 5cd9ba6016fb82bc7214fd9cb9e1a9014f5e44ca | Xenoborgs part 6 (#39595) | Deferred | Interactions, Gamerules | This xenoborg content and map stage depends on the earlier absent xenoborg feature chain.
0506 | 9de76e70c71097241b3b2a2720eef0c1d34aba89 | EVENT BASED WEIGHTLESSNESS (#37971) | Deferred | Movement, Physics, GameTicking | The broad cached event-based gravity rewrite conflicts with CMU's pull-based RMC weightlessness behavior.
0507 | da23bc9dcc0965b59d3ec8152c3f134952164a3d | Crawling Fixes Part 4: Can't crawl when weightless. (#39099) | Deferred | Movement, Physics | This fix depends on both the absent upstream crawling feature and the deferred event-based weightlessness architecture.
0508 | 32ad429b8ff7174726350a5e73fbb5c1bdb6e550 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0509 | 325f0e45fa44b100271e69d89c26e6d162a42dab | Viable Canesword (#39586) | Irrelevant | Shooting, Interactions | SS14 cane-sword combat balance is not applicable to RMC's weapon balance.
0510 | f2d512e19a8f61ee7c2efb0bf0481b93751e3143 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0511 | e90fac14eb9d8ecd7267ea7d7381ad0f3abc8886 | Fix: Untoggle removed actions (#39526) | Superseded | Interactions | Upstream reverted this implementation in 0521; later final action-removal behavior must be considered separately.
0512 | 2659e421c03f20a67765cf5152241433d89d89d5 | Fixing foam dart sprite in hands (#39758) | PortCandidate | Shooting | Target-final retains the corrected in-hand foam-dart asset while CMU still has the old sprite.
0513 | 3f14ceec0fa9aad8526bc4ced17fa6880bd81874 | fix performer's suit sprites (#39722) | Deferred | — | Binary suit assets and displacement behavior require comparison with RMC's customized clothing art.
0514 | a26a18243f0bcbefdf75c830d38ec0183a38e43f | fix upload console (#39756) | Ported (CS-0115) | Interactions, Gamerules | Accepted as downstream commit 65604179d7; upload consoles now copy an initialized provider's active runtime lawset.
0515 | d0b0a4a92611663bdb76150488e4908cc27cffaf | Automatic changelog update | Irrelevant | — | Generated changelog only.
0516 | b317d7514f34c56a989c661668290857fdef6f57 | fix: don't do emergency shuttle stuff in lobby (#38732) | Ported (CS-0117) | GameTicking, Gamerules | Accepted as downstream commit 883b292448; emergency-shuttle console work now skips the pre-round lobby.
0517 | 95b0df9a8948a7dd7d80b082981e25a2d9dc5afa | Fix nuke disk getting lost when polymorphed holder is deleted (#36058) | PortCandidate | Interactions, Physics, Gamerules | Target-final safely relocates the disk before polymorph cleanup; CMU retains the deletion path that can lose it.
0518 | 47dd036ef2414df7cdeab6ed89ae610c3a1abf79 | Prevent shoe buffs while crawling (#39648) | Deferred | Movement, Interactions | The equipment-speed fix depends on the absent upstream crawling state and RMC movement reconciliation.
0519 | db84d766e9fa6bd28e7da5ccd0dbb7da74c1be42 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0520 | 2bbff7f8c0c492c418d3330276b977fe72b7ad44 | Cleanup subdermal implant code (#39755) | Deferred | Medical, Interactions | The cleanup assumes later implant and trigger ownership that diverges from CMU's current implementation.
0521 | d1916fa4d3c6835df5004791d79a29867c42b486 | Revert "Fix: Untoggle removed actions" (#39776) | Superseded | Interactions | This revert only removes 0511's intermediate fix and is not a standalone target-final behavior to port.
0522 | dd74bfc083052bace7eaee20f04c0d9aa68f9faa | Remove BodyComponent check from MobPrice test (#39786) | Irrelevant | — | Test-only adjustment with no production behavior.
0523 | 9b8fa1af6f126cfa962e31d481accd03fafceb16 | fix: spellbooks can have charges ≠ 3 (#38769) | PortCandidate | Interactions, Gamerules | Target-final derives spellbook state from configured charges while CMU retains the hard-coded three-charge assumption.
0524 | def514bb3b293293dab242d7741a839d3ae67ec9 | Fix tricky nades not emitting their sounds. (#39792) | AlreadyPresent | Shooting, Interactions | CMU's older SoundOnTrigger always uses positional PlayPvs playback and exposes no positional field, so this dependency-specific failure is already avoided.
0525 | f78280501a1757e7c6f124d1efc1bc7c2e470767 | Moving Zombie Components to Shared (#39791) | Deferred | Medical, Gamerules | Shared ownership and networking must be reconciled with RMC zombie systems, components, and antag behavior.
0526 | e5011cb30515fe7368ef4e73e0c8160d1ada50dc | Automatic changelog update | Irrelevant | — | Generated changelog only.
0527 | d286a70d47bca6ea5c4835bd147f9b0bd97987e4 | cleanup changeling namespaces and prototypes (#39794) | Deferred | Gamerules | The cleanup belongs to the absent Changeling feature chain and has no independent CMU landing point.
0528 | cd0cc721576a6db7905b1c25709c002f9f1bc7ef | Impaired Mobility Disability (#39398) | Deferred | Movement, Medical | The trait depends on upstream crawling, slowdown, standing, loadout, and accessibility behavior that diverges in RMC.
0529 | 96d3b4bb29b60d85bb9f9a1e31dcfb7c23bad259 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0530 | 3d0573e7c8cb7eafa2e5df64d21d906a9e22ed0b | Added Hemophilia Trait (#38224) | Deferred | Medical | The retained trait changes bleeding, damage, character preferences, and localization and needs RMC medical balance review.
0531 | ac09e16765a7fe1bdb5e6d1a4494ec6e40166d59 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0532 | b124d0def58aea3fa16489c6b5bc85c3b1351095 | Cane Sword Priority Fix (#39795) | Ported (CS-0116) | Interactions | Accepted as downstream commit 242fef600c; the existing cane-sheath blade slot now uses retained verb priority 3.
0533 | 9c546a0072eaf62255ce0fe25b22067260b58a8c | Predict Mind Roles (#39611) | Deferred | Gamerules, GameTicking | Prediction depends on 0414's unported shared role components and broad RMC mind and antag reconciliation.
0534 | 002d9272e6903a3c1240f5f092c362493e08a666 | Fixed a typo involving the Space Lizard Plushie (#39808) | Ported | — | Accepted as downstream commit 6b1e4d81ea; the plush description now uses “colleagues.”
0535 | f67cebf7a4ed451f1911aa5c8bf6f04c198b917f | Admin Log Browser Improvements (#39130) | Superseded | — | Later stable merge 8f5d05c8bb removes the new Entries UI and migrations, restoring the pre-feature target-final shape.
0536 | be62e08de4f0eed4b72d35a6fd7c578ab2b197be | Automatic changelog update | Irrelevant | — | Generated changelog only.
0537 | d4f96fd1c640973c9eec03132c0f2f63dd1bf01e | predict morgue and crematorium (#39293) | Deferred | Medical, Interactions, GameTicking | The 30-file prediction and shared-storage migration must be reconciled with CMU's split entity storage and server-authoritative morgues.
0538 | 027ec912f20f11c3db5f602f3a96c9c59124d341 | Recolor Mime and Musician job icons (#39775) | PortCandidate | — | Target-final preserves the recolored job-icon intent after later ID-card reorganization; CMU still has the four old PNGs.
0539 | d61ebf2c87547f7ed6fb2a6e0041d8ad8b5875aa | Fix grenades not playing sounds when detonating (#39815) | Ported (CS-0119) | Shooting, Interactions | Accepted as downstream commit 7543aeabeb; the eight applicable grenade emit-sound prototypes now use positional playback.
0540 | c55157f27b53ebc1e9d6e9248962500dee1d19e0 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0541 | 49e0157325bc7bfbed0871428c7b88c682e3a133 | Adds stencil lettering to the spraypainter (#39701) | PortCandidate | Interactions | Target-final retains the self-contained lettering decal prototypes and stencil RSI asset pack, which CMU lacks.
0542 | 9ff62c9fe295ec0214f1772b602a88f14112b0d2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0543 | 73a5c45c49cf231d973c713dbc303430bd685b74 | Fix texture sprite specifiers to RSI PNGs (#39783) | AlreadyPresent | — | Current CMU already uses RSI sprite specifiers for the anomaly, revenant, and admin ghost icons, including the corrected mass-scanner state.
0544 | 8206126fb2dfcf4da53aaf903f5016a22337d553 | feat: add verb for smartfridge item insertion (#39807) | Deferred | Chemistry, Interactions | Target later moves this verb into a shared system, while CMU only has the divergent RMC smart-fridge implementation.
0545 | cfdf330a99f492429b6d018a0710770a8c8862a9 | Made moths less vulnerable to flames (#39672) | Deferred | Medical | Target and CMU retain different moth and flammable Heat modifiers, making this an explicit RMC species and fire-balance decision.
0546 | e4e883a5283cce71c7905a2a468186d163cfae01 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0547 | 91fc0f4378d3fedc8ba6e5efe19f524ce1d19214 | Invert HasMouthAvailable check (#39834) | AlreadyPresent | Chemistry, Interactions | CMU's vape path already returns when the target's mouth is blocked, which is behaviorally equivalent to the corrected availability check.
0548 | 0da1eee245b29ede29d8613c278d5c596c575629 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0549 | a13d5916bf1f6a553d2b11e715917b7f737b3552 | Fix admin objects tab (#39832) | PortCandidate | Movement, Physics | Target-final retains distinct grid, map, and station teleport handling plus confirmed deletion and refresh; CMU lacks these fixes.
0550 | 9dd071691dcba9109ad2d5e37a5b047331605f08 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0551 | 9880648f225e0082252ed74a5c08de4f463c9f79 | cleanup material doors (#39851) | Superseded | Interactions, Physics | Later commit 101b9ffb25 rewrites the material-door cleanup and subsequent balance changes alter the final prototypes.
0552 | 5c206ede67b0f3feb413068000f08a5031cab067 | Added more Derelict Cyborgs. (#38159) | Deferred | Shooting, Medical, Interactions, Gamerules | The large role, module, weapon, asset, event, and ghost-role expansion needs a dedicated silicon and balance port.
0553 | 958a98814aaac320a96bc78721e8cb8dcbd3e2e1 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0554 | 7429f56bd16cd7ec99a8ade96270fc18c62d8fbe | Fix "multiple keys" issue in Textures/Objects/Specific/Medical.rsi (#39861) | Ported | — | Accepted as downstream commit f7840a4f88; the duplicate top-level copyright keys are consolidated without changing current RSI states.
0555 | ed7bea8e019e947b54499b2ba0964a5a622ad2e2 | Inflatable Module (#35100) | Deferred | Interactions, Physics | The 20-file borg module, safe disassembly, component move, tests, prototypes, and assets need reconciliation with CMU's borg graph.
0556 | 114e444ed30941a83a55449642a9e7536503a62b | Automatic changelog update | Irrelevant | — | Generated changelog only.
0557 | 4c537b7dfa452a7945183ac2b06db6d218107aa1 | Fix electricity for Reagent Grinder at Marathon (#39801) | Deferred | Physics | This saved-map UID and wiring edit is embedded in later large Marathon revisions; reconcile the target-final map instead.
0558 | 8d3bbe2b7868d2e052f0e8621f6210add7e970c3 | Killed a resolve in ClientAdminManager (#39863) | Irrelevant | — | Dependency-injection and formatting cleanup only, with no standalone behavior.
0559 | 69bbf3b599f8dee12e3d09f4a3da27e353854a91 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0560 | ab681dbcbbc040cb245024c80acf0386e6734146 | GameRuleOnTrigger (#39845) | Deferred | Interactions, Gamerules | Current CMU lacks the shared keyed TriggerEvent and base trigger-on-X component required by this gamerule trigger.
0561 | e8c40f54ce0ce314c55e13f869c86bc7ec8d040e | Update Credits (#39864) | Irrelevant | — | Upstream credit snapshot only.
0562 | a8e0001f348d2f4a81ecd9679b5c91452368edab | Cleanup of resolves and usings. (#39865) | Irrelevant | — | Resolve and using cleanup only.
0563 | 91a4cee6e1b8b9810ad59eab8e9a944b24ad22a2 | [Bugfix] Lizard smite fix (#39842) | Deferred | Medical, Interactions | Blindly reversing CloneAppearance arguments affects all current appearance-transfer configurations; target later replaces the API with explicit copy direction.
0564 | 30aa61c29c9bc5654b719fdae5e0fa1ca9a69cbd | Changeling cleanup and bugfix (#39843) | Irrelevant | Medical, Interactions, Gamerules | CMU has no functioning Changeling antag, components, or systems for this internal cleanup to affect.
0565 | b5529ecf2b5db7052f2e5978df1136b812eac008 | Batchable lathe jobs, editable lathe job order (#38624) | Deferred | Interactions, GameTicking | The queue model, serializer, networking, production scheduling, and UI require a focused lathe migration.
0566 | b168f7be5a849863f6ce81048ca68fbd3eecefa9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0567 | 6c154fb79e467fff1191a4bb01c045e86cf7703f | Moving FlammableComponent to Shared (#39870) | AlreadyPresent | Medical, Physics | CMU already has the shared networked component with RMC-specific extensions and no server duplicate.
0568 | ffc7cc5e5de9fc0ba8ddd928afdfaea348bd1480 | Combine AdminFrozenSystem in shared. (#39885) | AlreadyPresent | Movement, Interactions | Current client and server subclasses both inherit the shared behavior, preserving runtime semantics despite structural duplication.
0569 | e8320cc9d8c96eda04420e74b26b1b4802b8633c | [Bugfix] Fix topical self healing time multiplier not working (#39883) | PortCandidate | Medical, Interactions, GameTicking | CMU calculates the self-heal penalty from the item, effectively squares the base delay, and fails to update repeats from current patient damage.
0570 | 652190ff3207d3e83b88945f430260af04d91053 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0571 | 2f752f2e5946a749b7c3a52661aa31e446a09288 | Fixed pacified people using the laser carbine (#39891) | Deferred | Shooting, Interactions | CMU lacks the marker entirely, closing lethal use but also harmless practice use; parity requires a safe prototype-inheritance review.
0572 | 5c821f32de702ffaa1fe39d8f3546ed8b7a5198e | Automatic changelog update | Irrelevant | — | Generated changelog only.
0573 | a566b4cc84738aa3f685ee063250e32b3be68e62 | Fix Smile's hat displacement map (#39824) | PortCandidate | — | Target-final retains the corrected displacement PNG while CMU still has the old asset.
0574 | dfa1b01b5e6b9e5fb0eaed6db41d04c3a0fafa75 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0575 | b714733e62add8a2711c9fc556e85740e2666fa0 | Exo: Add atmos network monitor (#39330) | Deferred | Physics | This saved-map addition belongs in a target-final Exo reconciliation because both target and CMU map states have diverged.
0576 | ceda478ae2fb0900b85ef4001c633e217958cb08 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0577 | fd4a0a29b4588f000017de2f8708636a0f9376f9 | fix: Block EntityStorage from inserting into mechs (#37942) | PortCandidate | Interactions, GameTicking | CMU can dump stored contents into a mech equipment container and hardlock non-equipment; target-final retains the outer-container cancellation and cleanup fix.
0578 | 49888f3c473b5b0b7b060fc2cd4575bfe4c3b978 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0579 | 688c0b5884003b2f2cf81bcbb1842915851356b3 | Syndicate locks are now selectable (#39532) | Deferred | Interactions | Selectable component adders, voice-lock UI, and prototype changes depend on the trigger architecture and are extended by 0595.
0580 | 4505f61ff2d1d2827385607de84e6613248dafc4 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0581 | c9c8fcbb8217197b64e18c9ceeacaab2c268d0aa | Option to disable Crawling in Cvar (#39739) | Deferred | Movement, Medical | CMU lacks upstream crawling, CrawlerComponent, and its knockdown integration; RMC vent crawling is unrelated.
0582 | 931dec8e289013281654012212718d168164ee96 | Remove the dynamic game mode from player votes (#39902) | AlreadyPresent | Gamerules | CMU has no Dynamic game preset, so players cannot vote for it.
0583 | b1084ed906a015df9b8bede3a37d555f18dc15af | Automatic changelog update | Irrelevant | — | Generated changelog only.
0584 | 1800be1f5822149d6d8755b5680e915a8ffec1c0 | Prevented Engiborgs from picking up AI lawboards (#39730) | AlreadyPresent | Interactions | CMU's construction borg module has no virtual circuitboard hand and therefore cannot accept lawboards; the target guard depends on later virtual-hands architecture.
0585 | 72738f7c8715149c77f8782f42a3c30b840e30f9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0586 | 8e8b318862ec0bd6238986085a6c25d4803d8c5e | Fix chameleon backpacks not being able to be opened when locked (#39784) | Deferred | Interactions | Making storage locking opt-in could silently unlock divergent CMU and RMC Lock-plus-Storage prototypes without a full consumer audit.
0587 | 79c006701ea50c665ac776a5efac77381e298d6a | Automatic changelog update | Irrelevant | — | Generated changelog only.
0588 | 00360968b74820576a50fbaa75b95b1b25d6160b | Make Modular Grenades with Chemical payload respect their trigger delay (#39905) | Deferred | Chemistry, Interactions, GameTicking | The trigger-key guard cannot be expressed in CMU's old server-only unkeyed TriggerEvent and must follow the trigger migration.
0589 | 37b4649a50a479c1a1ab4b0c451fefdc09f14e78 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0590 | 0236a9065b461013a706d64cc9e051d66033b5e6 | Wizard can no longer teleport to arrivals (#39901) | Superseded | Movement, Gamerules | Later commit d15d16651f restores WarpPoint and solves access through teleport-specific whitelists and tags.
0591 | 2965522cddc9ea7b1a6e58d85a11d042a0bb009c | Automatic changelog update | Irrelevant | — | Generated changelog only.
0592 | 60ea2b37fbb5561ab4bfbc339720372350044601 | Clipboards added to autolathe (and other folder changes) (#37705) | Deferred | Interactions, Gamerules | The broad folder split mixes recipes, assets, loadouts, crates, vendors, random spawners, and traitor content and needs decomposition.
0593 | cfb13f98e1e4c7c91495f55f62ab728b411d6f38 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0594 | 36c9f2006d62d90555ca9ecf05c1c700868a8872 | [Bugfix] Fix Cross Grid Magboots (#39910) | AlreadyPresent | Movement, Physics | Upstream refreshes a cached weightless field, while CMU computes weightlessness live and cannot retain that stale cross-grid state.
0595 | c36778e25e214659dd73da43a604a323cd91b9dc | TriggerOnSimpleToolUsage and add tool locks to syndicate items (#39900) | Deferred | Interactions | This requires shared keyed triggers, base trigger-on-X components, and 0579's selectable component-adder infrastructure.
0596 | ae199ba314b91540163589e05edcafc36a18af7c | Automatic changelog update | Irrelevant | — | Generated changelog only.
0597 | 973689425c78988ec338e940ca545089d738c4d5 | Fixes Diona rooting not working since event based weightlessness refactor (#39893) | AlreadyPresent | Movement, Physics | CMU's pull-based weightlessness query has no cached state to refresh and observes the rooted event response live.
0598 | 149bb4ca14faf858f19c9754c1033a0dd1f1e672 | Re-anchorable structures (#39542) | Deferred | Interactions, Physics | The broad structure anchoring and rotation contract diverges in RMC and has later target throwing and station-only anchoring fixes.
0599 | 10f7c2e568b31c44c80a12ad1deab417972cb715 | Automatic changelog update | Irrelevant | — | Generated changelog only.
~~~

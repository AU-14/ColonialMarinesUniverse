# SS14 upstream inventory: wave 0004

Audit date: 2026-07-20

- Pinned baseline: `59633f6dc50e77dda8cefa344d87c7b01e06a810`
- Pinned target: `40ca2c7f90d11d27be5457d177c133f0947d1c08`
- Range: ordered first-parent commits, zero-based indices 0600 through 0799
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
0600 | adefde67c0c5181491655f5ad228af022864b289 | exo decor update (#39896) | Deferred | — | Large intermediate Exo map revision; reconcile the target-final map rather than importing this generated snapshot.
0601 | af2bea7f66a1e482bc14235a81f8f093e43ca83c | Add test of disposal unit throw-insert behavior (#39479) | Irrelevant | Interactions, Physics | Integration-test-only commit with no production behavior.
0602 | ecc499a7d5df976772cb6950bb48ef0ab48642b1 | [Bugfix] Generators can now be weightless. (#39787) | Deferred | Movement, Physics | Removing the prototype gravity override is coupled to upstream's event-based weightlessness model; CMU still uses the older pull-based gravity path.
0603 | 63f38558ca88d527dc99d7f84b8618783aea0fb7 | [Cleanup] Remove FellDownEvent (#39762) | Irrelevant | Movement | Removes an unused event and dead handler without changing current behavior.
0604 | 2ebdd9d4cd04a5fdfd671db5e3ff05e52b3c8976 | Reagents now drop when dispensers are deconstructed (#39676) | Ported (CS-0120) | Chemistry, Interactions | Accepted as downstream commit baf58e109a; reagent dispensers now empty both storage and beaker containers during machine deconstruction.
0605 | a7087c1512914732beeaaf4a85a415cb8e342414 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0606 | 1ecf62e72ad36585c1d0cbb1160609141b8ef500 | fix: monoify card_drag, card_tube_bonk (#39511) | Irrelevant | — | Audio encoding normalization has no standalone gameplay delta.
0607 | 5028161c7bef126ec115bf34c7778d520f72169b | New Feature: Symptoms of radiation poisoning (#39805) | Deferred | Medical, Chemistry | New vomiting and popup threshold behaviors alter radiation feedback and require RMC medical and radiation-balance review.
0608 | 420fb5cebfdb707b9b7d664a78956c9875dbca6a | Automatic changelog update | Irrelevant | — | Generated changelog only.
0609 | 60ea135fd3b07c69519f1823ae55b19959fb37de | Revert "Added button and manager for in game bug reports (Part 1)" (#39872) | AlreadyPresent | — | CMU does not contain the reverted external bug-report UI and service, matching target-final behavior.
0610 | 49daf74069df0a2c762ad9abb763ce6bff9ace79 | Fix audio mispredict when quick inserting (#39930) | AlreadyPresent | Interactions, GameTicking | CMU's smart-equip path already gates the insertion sound to first-time prediction.
0611 | b093a688aa81c3a2ed557bea87993da4b596c9d5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0612 | 84e0a0f6fac1934abe217cb5961d9c2c9296ec3b | Improved cardboard-weapon descriptions (#39927) | Irrelevant | Shooting | The affected cardboard weapon and shield prototypes are absent from CMU, so these descriptions have no applicable landing point.
0613 | 886e3c099dcff60a445177bcf08a123e43ef34a5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0614 | eef99915a0d55d8001ab7a6f45c9c409c8eba295 | fix bagel mapped containers missing (#38933) | Deferred | Interactions | Saved Bagel map container data belongs in a target-final map reconciliation.
0615 | 679c641611d0defbc023bf0b6ca0b794a2b2c450 | Major Relic Update (#39215) | Deferred | Gamerules, Physics | Eight-file, roughly 72k-line station, shuttle, parallax, and map-prototype update requires a dedicated target-final map integration.
0616 | 80cfd8041d552c33270b301c5aa83eef9727cf3c | Automatic changelog update | Irrelevant | — | Generated map changelog only.
0617 | ff54dcc2f433eceda3b213185174facf343351dd | box station: tweak hop office (#39779) | Deferred | — | Intermediate Box map edit; import only through a target-final map review.
0618 | 5ddf503331e4dbbeae33bab6375842ccd807e0c4 | Updated Aseprite Tools (#39358) | PortCandidate | — | Retained displacement-map authoring scripts are newer than CMU's copies and can be updated independently.
0619 | fe21b9a5d6ce728ceac19da1b69eeaad3295be93 | Amber Station - Added Late Join and Pressure Update (#39943) | Superseded | — | Numerous later Amber revisions replace this intermediate generated-map snapshot.
0620 | b8ee881d60089fec3dcb360976de5cd1ae9905d4 | Marathon - Pressure Update (#39955) | Deferred | Physics | Large intermediate Marathon atmos map update should be reconciled from target-final.
0621 | d73502f7fd6b1d277092c3293c91456e366b9a5a | Automatic changelog update | Irrelevant | — | Generated changelog only.
0622 | 20ed31d4c9fa978bbf1763239477b67c94865bbc | Automatic changelog update | Irrelevant | — | Generated changelog only.
0623 | 43ce4047e33acb7d754ed073220ebe8373144017 | Box Station - Pressure Update (#39954) | Deferred | Physics | Large intermediate Box atmos map update should be reconciled from target-final.
0624 | 91222f78b130bac7e67d56bfede9f5426d2d60b0 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0625 | acd0fd26443a5a200b45f8880f28b2615360e50b | Bagel Station - Pressure Update (#39945) | Deferred | Physics | Large intermediate Bagel atmos map update should be reconciled from target-final.
0626 | 5380be0085936678b0fcca5443d206c14022cfd1 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0627 | 9be78ced63b0639aa574e612ce394bd86dfb07f2 | Remove a default Cyborg name (#39948) | Superseded | — | Later cyborg dataset and naming changes replace this intermediate single-name removal.
0628 | 0bbe335a3aec216e55e901b9d043de8b0d0c4db1 | Stop Sentience Event targeting Zombified Creatures (#39950) | AlreadyPresent (CS-0043) | Medical, Gamerules | CMU already removes SentienceTarget during zombification under the audited CS-0043 contract.
0629 | 8739271e43a9278e78f784a70533bfcf34fc0b1a | Automatic changelog update | Irrelevant | — | Generated changelog only.
0630 | 65bffbebf1540d9349f5eb009c971603dea0db61 | Sentry turrets - Part 7: Electronics and construction graphs (#35236) | Deferred | Shooting, Interactions | Electronics, construction graphs, and turret prototypes depend on the unported sentry feature chain.
0631 | 4797c0fe06cfb7996805036cf3d295cecf03e3ec | Small Status Effect Cleanup (#39944) | PortCandidate | Medical, GameTicking | Target retains the small status-system expiration cleanup; adapt it only after confirming CMU's divergent status lifecycle.
0632 | a590d65dc52e1c4d60952528d11a8cd13204ad60 | Add SnoutCover appearance layer (#39949) | Superseded | Interactions | Later Nubody and species-layer reorganization replaces this intermediate visual-layer addition.
0633 | 800b7e1a88ab9957d1bc6ef783b4f0d4aa0d0a77 | Fixed changelog error (#39971) | Irrelevant | — | Changelog-only correction.
0634 | 941e0daca73871cdd81667ba054f5d51157fa134 | Staging into Master (#39977) | Deferred | Movement, Medical, Interactions, Gamerules | Zombie marker networking is already equivalent, but the retained derelict-syndicate-borg hierarchy is absent and dependency-blocked by deferred shared-zombie and silicon work.
0635 | 6a22ee7d39be79f9929dde64e1e66b847ca6d640 | Fix forensic scanner leaking fingerprints onto the scanning object if you use the verb (#39964) | Ported (CS-0121) | Interactions | Accepted as downstream commit 4f00733fca; forensic scanner verbs now opt out of contact evidence creation.
0636 | 8614aafabaec0980a5ea1cb60fe0a0c9530bf66b | Automatic changelog update | Irrelevant | — | Generated changelog only.
0637 | 8f55a4fcfc79dce27a8e21e5a260ebc74f5ef970 | Scurrets - can wear pet bags, mail bags and spears (#38774) | PortCandidate | Interactions | Retained inventory-template, prototype, and mail-bag asset support is absent from CMU and can be imported as a focused scurret-content unit.
0638 | 294b32373ac2ccf80b61ef778c76dbb6e32eb94a | Automatic changelog update | Irrelevant | — | Generated changelog only.
0639 | 5a40913bebdef4994425fe7245ae99924d42d4a4 | Messy drinker immunity and cleanup (#39989) | Deferred | Chemistry, Interactions | The nullable immunity/tag cleanup is coupled to the later shared ingestion and MessyDrinker architecture absent from CMU.
0640 | 4a6fc71d07ae230d62201cddc4c11c8d35497c3c | SharedKitchenSpikeSystem bugfixes (#39959) | Deferred | Medical, Interactions | The retained popup, timing, and state fixes overlap RMC kitchen-spike behavior and need a focused reconciliation.
0641 | c7a10e8bce0d80db8a0ae480b4aa5ef4b2df63a0 | Stop derelict borgs from duplicating their ghost roles. (#39992) | Ported (CS-0122) | Gamerules | Accepted as downstream commit e2936b2da4; the existing derelict borg role is now one-shot and cannot duplicate its listing.
0642 | c1e3eba88c5d8c22287facaa4d0f0150c0fcb4a6 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0643 | 9e22aa4cd5c05d2cabdfb79cc1c1b2b22660cfe8 | Clown bags squeak when inserting items (#39931) | Ported (CS-0123) | Interactions | Accepted as downstream commit 13743e4eac; all three clown-bag families now play the retained insertion sound.
0644 | 5bee17686c1e7cea376d7099cd427fcf9af99fb8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0645 | f20a42a7c36e39a25bdbfeed64779cf2689d75e3 | Bagel AI Turrets + Camera Coverage (#39968) | Deferred | Shooting, Interactions | Intermediate Bagel turret and camera map data belongs in a target-final map reconciliation.
0646 | b24cb11f86ef0640d308b669ae6ea0e5518576f9 | Automatic changelog update | Irrelevant | — | Generated map changelog only.
0647 | 13db6cdac1a87cb32bb3fa6aec77aa99bb6fcbe8 | Marathon AI Turrets + Camera Coverage (#39969) | Deferred | Shooting, Interactions | Intermediate Marathon turret and camera map data belongs in a target-final map reconciliation.
0648 | 3b95ab390b64d912220ac81d8487ded97c5f901a | Automatic changelog update | Irrelevant | — | Generated map changelog only.
0649 | c89b20b19f27a7bb62ade8de9a0617f259ab2995 | Nullable messydrinker tag (#40002) | Deferred | Chemistry, Interactions | Depends on the unported shared MessyDrinker and ingestion model from 0639.
0650 | bbe9b33abf00814628dc45598257a362e3c2c4dd | Update Credits (#40005) | Irrelevant | — | Upstream credits snapshot only.
0651 | d03f9f41526aa861018df138f5b08a6f54dc8a7f | Removed unused asset from devmap (#39974) | Irrelevant | — | Generated development-map asset cleanup has no production behavior.
0652 | 487c280f1c1495d93be1b857fc9fde66fb86482a | Staging to master merge (#40013) | Deferred | Gamerules, Physics | The retained delta adds missing latejoin spawn groups to Elkridge, Exo, and Plasma; reconcile each from its target-final divergent map instead of replaying serialized map churn.
0653 | 0e23e4537fd895fb0a6fcdd7b435dbb43d7d7822 | Migrate all mechs to PartAssembly and remove legacy MechAssemblySystem (#39027) | Deferred | Interactions, Physics | Mech construction migration intersects RMC mechs, graphs, migrations, and assembly systems and requires a dedicated batch.
0654 | 1c706cdbc3453bca33c961fa91186a45cdcca809 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0655 | 8f5d05c8bb08de873185caf15f014f6bc303b5a7 | Stable merge (#40025) | AlreadyPresent | — | CMU already has the target-final branch shape without the reverted admin-log-browser feature and database migrations.
0656 | 0ba5d036a2091657268c56dceb28e7ee96401916 | improve spawnpoint error logging (#40021) | Deferred | Gamerules | The useful null-job logging is entangled with a CMU Brigmedic wildcard sentinel; reconcile that branch-specific null contract before porting.
0657 | 5d25cae33d6b3191073d212e0d2530cdab589cd7 | TriggerOnMeleeHit and more (#39826) | Deferred | Shooting, Interactions | Melee hit, miss, and swing triggers depend on the absent shared keyed-trigger foundation.
0658 | 5ee093b13dcdbcd01e12f2cd680f31660b94dd45 | Merge stable into master (#40034) | Deferred | Physics | The retained Terminal hotfix converts the map to a grid and corrects atmosphere/fan state; reconcile it from the target-final map because CMU retains the older format and divergent serialization.
0659 | 9f36a3b4ea321ca0cb8d0fa0f2a585b14d136d78 | Fix docstring typo starts -> stops (#40031) | Irrelevant | Movement, Interactions | Comment-only correction in PullStoppedMessage.
0660 | 817a2973e57d745655a86883409ebe85a2bb7265 | Moths cannot eject items from military boots (#40049) | Ported (CS-0124) | Interactions | Accepted as downstream commit 9c747942ed; the military-boot item slot now wins interaction-priority conflicts.
0661 | feb0fac20fc0e71f144b9a9a7d5b8b89f930848b | Automatic changelog update | Irrelevant | — | Generated changelog only.
0662 | 86e77f05ce3901b6d5ab4c7702295f50beea5804 | Predict InjectorSystem (#39976) | Superseded | Medical, Chemistry, Interactions, GameTicking | Later target chemistry, solution, and injector ownership changes replace this intermediate 11-file prediction migration.
0663 | 40b0b49dbcd0fea2819cc7b2352ffb7d45abb072 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0664 | 2624647e71f5fab3df11e5738bf8efea340b0a61 | Fix incorrect bullet & cartridge names (#39993) | Ported | Shooting | Accepted as downstream commit b2506fd4a9; standard rifle ammunition names now match their actual projectile and cartridge types.
0665 | 831d384ff5c28f1c2bfc902dc96b9d93557129b2 | Texture Scaling for clothing (#39714) | PortCandidate | Interactions | Target retains configurable clothing texture scaling; CMU lacks the component field and client visual application.
0666 | b2eeedb34884c99d8a65315c0ac3eed5d3201e87 | Lizard Tails Can Be Hidden By Clothing (#40026) | Superseded | Interactions | Later species and displacement-map restructuring replaces this intermediate Reptilian entity-prototype flag.
0667 | 05a4e6d00cd9e1794caded0f1c402d098f492219 | Fixed Corpsman Name (finally) (#40055) | Ported (CS-0131) | Gamerules | Accepted as downstream commit af42c8b2b0; nuclear-operative medic metadata now resolves to the Corpsman title.
0668 | 46f13fc1dd086b501de58712d3dc512bfd0ca68a | Event Shuttle Fixes (#40059) | Deferred | Physics, Gamerules | Intermediate Cryptid event-shuttle map fixes should be taken from the target-final map after event compatibility review.
0669 | 3c11a6a80b629a4bc5f0a2cffc722ca993ec4715 | Automatic changelog update | Irrelevant | — | Generated map changelog only.
0670 | 8a041fa5cb6ad7cc21cb79ab7967235c1e869473 | Update 4 visitor shuttles & nanomed inventories (#39718) | Deferred | Medical, Interactions, Gamerules | Large shuttle-map, medical-vending, restock, prototype, and asset bundle needs target-final map and RMC medical-vendor reconciliation.
0671 | c99c9ed2004cc4c9fa60dfe0c4cc37cb7f4ef315 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0672 | ca29e0a16690a5f827095718afb60cfb44e702a8 | Fix radiation vomit for dead mobs (#40020) | AlreadyPresent (CS-0049) | Medical, Chemistry | CMU already carries the audited dead-mob vomit guard and force escape hatch adapted to its server-owned system.
0673 | ec5791fc612eaa35272d050fce0ba164b6fc16ad | Automatic changelog update | Irrelevant | — | Generated changelog only.
0674 | d41786ff332ade1b1e1f6eec9414922cbc5ceb35 | Remove empty `drink-component.ftl` file (#40064) | Irrelevant | Chemistry, Interactions | The file is not empty on CMU's branch and contains localization still required by its pre-Edible DrinkComponent implementation; deletion is non-applicable.
0675 | 8e97f8c45b4379a855e76d083fa4bda096a99c4f | Add myself to Codeowners for Stunnable and Nutrition (#40061) | Irrelevant | — | Repository ownership metadata only.
0676 | e8583da476ee50b77264c6892c1cb84112c772b0 | prevent double-mapping lights (#39939) | Ported (CS-0132) | Interactions, Physics | Accepted as downstream commit aab159d309; always-powered wall lights now share the retained placement-replacement key.
0677 | 3bd1ba940b68a78630ee740d2f6646f0a911cc77 | Adds a secHUD to the noir-tech glasses (#39859) | PortCandidate | Interactions | CMU's noir-tech glasses lack the retained security HUD component.
0678 | c947f741e13dfc318b7287b8ea71161b0acecc2c | Automatic changelog update | Irrelevant | — | Generated changelog only.
0679 | 7511b3bed3b992c30e3821965e0cdcb15dd1b196 | Fix benchmark (#40039) | Ported | — | Accepted as downstream commit 9cda91e29b; the map-load benchmark now resolves its parameter source correctly.
0680 | d3731395b6de358b13364b02eaeff1244a54aa2f | Make git hooks work in git worktrees (#40038) | PortCandidate | — | Retained BuildChecker hook path handling is absent and can be reconciled with CMU's local bootstrap scripts.
0681 | 0e884da5eb1a1b8edbf48c4ea3c0e45fc81bed92 | Localize, cleanup, and LEC round control commands. (#38812) | Deferred | GameTicking, Gamerules | CMU's older commands include RMC maintainer flags and delayed-round-end CVar behavior; reconcile the retained localized entity commands without losing those fork semantics.
0682 | c709d4d55c3cc024fdd6585c7d6483e3e2c9330e | Add CVar for disabling loadout item role timers (#36775) | PortCandidate | Gamerules | CMU lacks the retained CVar and role-timer bypass used by lobby loadout validation and development presets.
0683 | 69b3df03d8ee6a624b675537ec1542c4008bcdb2 | Don't show item dropping popup when wielding. (#40032) | Ported (CS-0138) | Interactions | Accepted as downstream commit 589f33e549; wielding silently clears obstructing virtual-item hands while other callers retain drop feedback.
0684 | e68e71c0680855008490f3f69fc98067579dedfa | Trimmed Sentience Targets from Corgis Smile and Cockroaches (#39810) | PortCandidate | Gamerules | CMU still marks both ghost-role-capable creatures as Random Sentience targets; target-final removes the conflicting eligibility.
0685 | 9ca7b754452dd94efcc9b911a5cc472420377605 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0686 | 103c3983df4631fa8c57d22974342211e4f5ce7d | Updated inspector description to reflect functionality (#40072) | Ported (CS-0133) | Shooting | Accepted as downstream commit ac1a3e42f3; the Inspector revolver description now identifies its existing double-action behavior.
0687 | 3775da3345aaf4437d44cd047139471132ebc7e9 | Helm + Mask Displacements for Reptilians (and some unique helmets) (#39351) | Deferred | Interactions | Hundred-file binary displacement-asset batch must be compared with RMC's customized Reptilian and helmet art.
0688 | e0ead5a83a913561496e5fb725f483b3beeff98b | Reptilian tail sprites for hard/softsuits (#35842) | Deferred | Interactions | Hundred-nine-file suit and tail asset migration requires target-final import and RMC species-art review.
0689 | 6403c3f5f1bbfa5d863aa2eaef586535233011d1 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0690 | d1deb5b059dba128cfcd8573e0234b1450966635 | Some more vox customization (#39083) | Deferred | — | Retained Vox markings and binary assets need comparison with RMC's divergent Vox customization.
0691 | 15f3381bc31b2a0be82c7ef448d25ca9ec1b148c | Automatic changelog update | Irrelevant | — | Generated changelog only.
0692 | 3aeecd0139c794408a5434091609077b106c53b1 | Add inhand sprites to Cartons and Cups, give new inhands to Cans. (#39814) | Deferred | Interactions | The 126-file drinks asset and RSI metadata rewrite should follow target-final drink organization and ingestion reconciliation.
0693 | e6d00428a8a77527929452e44f400a8486077596 | Fix small issues with Text Highlighting (#38144) | PortCandidate | — | Target-final retains the chat-highlight matching and localized options fixes that CMU lacks.
0694 | 7916819203d9ae19fbd0aa565eab9658918ce981 | Burger Inhands (#39894) | Deferred | Interactions | Forty-two-file burger prototype and binary in-hand asset bundle overlaps earlier item-size changes and should be imported from target-final.
0695 | 3e63e4590d8d9df78eaf0dafc3cc601c12b73bd0 | Adjust bureaucratic error to prevent only passenger being available (#40001) | AlreadyPresent (CS-0048) | Gamerules | CMU already protects the four apprentice roles and Station AI under the audited Bureaucratic Error policy.
0696 | f5a980edc2222deedf3e0a0c640e12eef3ccaa1a | Cleanup AddPolymorphActionCommand and LEC conversion. (#38853) | PortCandidate | Interactions | The retained localized-command conversion and obsolete locale cleanup are absent from CMU's current administration command.
0697 | d01f65223d2b40832f24d3b743ea3dde6dd3c12f | Automatic changelog update | Irrelevant | — | Generated changelog only.
0698 | 1a452494e6473811c7f8435bc94423850d8dfbaf | Add senior courier PDA for cargo techs (#37661) | Deferred | Interactions | PDA, loadout, job, role-loadout, localization, and binary asset changes must be reconciled with divergent RMC cargo roles.
0699 | 8fdcb8f91f5493674c3399157ba6cfc453e9524e | Automatic changelog update | Irrelevant | — | Generated changelog only.
0700 | 893f4f14036b34505c47bff43f287a19ab4a4d67 | Use a fixed amount of decimal points in gas analyzer window (#40081) | Ported (CS-0134) | Chemistry | Accepted as downstream commit 7d18297d25; pressure and temperature readouts now retain stable precision.
0701 | 7f511abb944f1d1a08fb7c001babea7f677f00e5 | Berry Delight recipe edit (#40085) | AlreadyPresent | Chemistry, Interactions | CMU's Berry Delight recipe already uses the corrected target-final ingredient quantities.
0702 | deb08579a484534e711a8eac54151b24bec6825d | Automatic changelog update | Irrelevant | — | Generated changelog only.
0703 | 24f4b40881fc4094c76dcbba7088af930a3d37ca | Don't enqueue construction events without validation (#39869) | AlreadyPresent (CS-0015) | Interactions, GameTicking | CMU already carries the audited validation-before-queueing contract and its paired specialized-handler fix.
0704 | f63eb2e97af9372988e13865ae2ff9b73b8b2ba7 | Remove unused combat-equipped-helmet (#40095) | Irrelevant | — | Removes unused sprite states and metadata only.
0705 | 20f2cb920b2a1c593fdf98da44809a18f03c4caa | Atmos Delta-Pressure Window Shattering (#39238) | Deferred | Physics, GameTicking | Foundational 22-file pressure-damage simulation, CVar, benchmark, test, map, and prototype feature requires a dedicated atmos migration.
0706 | 53c9f336cf0b83f234086816622bdc3c6389053b | Automatic changelog update | Irrelevant | — | Generated changelog only.
0707 | 723a0030ba7e15859d5ed3f06252d2eda3b87e7a | Give inflatable walls the DeltaPressure component (#40098) | Deferred | Physics | Prototype behavior depends on the unported delta-pressure system from 0705.
0708 | 3d8958486004601c90e07a37a10bf62eed21409c | Automatic changelog update | Irrelevant | — | Generated changelog only.
0709 | 70ffc1eb5dbf761b57fbac97e01b243f110e8436 | Add heat distortion shader for hot gases (#39107) | Superseded | Physics | Upstream removes this overlay, CVar, shader, and texture in the later staging merge at 0861.
0710 | a6be4ff3385a321310d9033a657c4e4c0cc4126d | Automatic changelog update | Irrelevant | — | Generated changelog only.
0711 | 348f462b122cfb6cb91be19d0cfa3b533bec9ce3 | Fix QM Golden Knuckledusters not being a objective (#40096) | Ported (CS-0135) | Gamerules | Accepted as downstream commit fe42f13beb; the existing theft objective is now reachable from its weighted objective group.
0712 | d7fd4cfb80e7f696f72b9f140d8d4a1a6923cbd2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0713 | ed12c1d3f5607db906712e3a5d13d7342dec7fc0 | Telepad Label Fix (#39975) | Ported (CS-0136) | Interactions | Accepted as downstream commit 196ba4c995; cargo telepad labels and receipts now use each queued order's account.
0714 | 427e4a88ea3c3bdb3b592aa117c2aecf61c6ade4 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0715 | 2f3d976c33df1f94b5a544f5c1942172b5d40039 | Fool players with decoy presets (#40053) | Deferred | Gamerules | Preset aliases, lobby selection, and administration behavior intersect CMU's RMC-specific game presets and require a focused rule audit.
0716 | c51104952e902e68af378e20c3f8896a2214fba0 | Automatic changelog update | Irrelevant | — | Generated admin changelog only.
0717 | f521ec31de6cb559c55265a5c919ea5ea8965ef1 | Fix: Ability to open AHelp in the lobby by pressing the hotkey (#39525) | PortCandidate | GameTicking | CMU retains the affected lobby hotkey path; target-final allows AHelp without an attached player entity.
0718 | df4d923a9b709c9f3b5f123ce743db57c713351a | Add 2.25 second delay to scurret petting (#40097) | Ported (CS-0137) | Interactions | Accepted as downstream commit 95092c5d3f; scurret petting now throttles repeated effects, sounds, and popups.
0719 | 90dcf834711f9bd5992299ade7c30bdf3cc926d2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0720 | 0daccbf457a31bd8ebc2a0ebbce67a89fac9e2e0 | Stop packaging `Resources/ServerInfo` and `Resources/Changelog` on the server (#39897) | PortCandidate | — | CMU's server package still includes client-facing server-info and changelog resources that target-final excludes.
0721 | d33478e41ff3d406cf39601df85a548bb72561a5 | Package win-arm64 and osx-arm64 servers (#40113) | Irrelevant | — | Upstream GitHub publishing matrix change is deployment-specific and does not alter CMU runtime source.
0722 | 7dbe1b219c5105030aa53beba77a5a8e8280571e | Improve Gas Yaml Serialization (#40070) | Deferred | Chemistry, Physics | Thirteen-file gas-array serializer and prototype schema migration touches atmos reactions, mixtures, tanks, jetpacks, and RMC gas consumers.
0723 | 12e869764824088b2c8c52168a3affee67d82a5c | Organize JobIconPrototype yml (#39774) | Irrelevant | — | YAML ordering and grouping cleanup without standalone behavior.
0724 | 52c903cab85aa73502e439c73c7808d19d6570dc | Dynamic anomaly scanner texture (#37585) | Deferred | Interactions, Physics, GameTicking | Twenty-five-file anomaly scanner state, prediction, vessel, prototype, and binary asset rewrite requires dedicated anomaly integration.
0725 | 587d9ad191cdc443c7ebd6a7e7f9d06628a21240 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0726 | 467f19b58ef849f20be178b16d979a944169063b | Reuse lathe queue instead of redrawing (#39886) | Deferred | Interactions, GameTicking | Client queue-control rewrite depends on the deferred batchable and reorderable lathe-job model from 0565.
0727 | 9f65cf7a7518fc407a84dd3f9dda6f737b7dd5fc | Automatic changelog update | Irrelevant | — | Generated changelog only.
0728 | ad3874b8cd71d44a8ceac8f04dbe280760315999 | Skip MapImages folder in packaging (#38928) | PortCandidate | — | Target-final excludes generated map images from shared, client, and server packages; CMU's packaging paths still include them.
0729 | 01a7fc66f011967579330b1c155c326b61fa5806 | Can't crawl over counters (#40099) | Superseded | Movement, Physics | Upstream explicitly reverts the collision group and table-prototype changes in the staging merge at 0861.
0730 | b15132585bd01b54236da247e48eddc26ab125cf | Automatic changelog update | Irrelevant | — | Generated changelog only.
0731 | a8ba84ecf70eba6c740e641c41ca96392d056d41 | Fixes Theobromine missing from Iced Coffee (#40063) | AlreadyPresent (CS-0027) | Medical, Chemistry | CMU already carries the audited architecture-compatible theobromine output for iced coffee.
0732 | 36dfc7979797c6dc5ec9ee084079cdbac9e7b5ee | Automatic changelog update | Irrelevant | — | Generated changelog only.
0733 | 60d1d2c9b1abbb0da512fb15cfaf14b45d8ac16d | Fix xenoborg action icons (#40118) | Deferred | Interactions, Gamerules | Prototype icon corrections depend on the absent Xenoborg feature chain.
0734 | 4125d28b752414fde6e8d7ea760221b3f7402970 | Fixed a error in the "Adventures of Ian and Renault" books (#39932) | Ported | — | Accepted as downstream commit 4b1db535ed; the Arctic adventure book no longer repeats unrelated city-story lines.
0735 | 1db8496dd71a4af2ec3e046bbbdb59ad47a266b0 | Fix DeltaPressure damage not capping beyond a certain pressure (#40125) | Deferred | Physics | The retained pressure-cap calculation depends on the unported delta-pressure simulation introduced at 0705.
0736 | 63a17312cc06b7882e7f5ed9d13efa1b364e9245 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0737 | 99cdbfc431fc4430df40fa69f22ba1a7949f9dcb | Give shutters the DeltaPressure component (#40126) | Deferred | Physics | Shutter prototype behavior depends on the unported delta-pressure simulation from 0705.
0738 | 5e0e5e045a13ce5512bd6e31609e23f5a73974fe | Automatic changelog update | Irrelevant | — | Generated changelog only.
0739 | d488ca96b2131b56045b9cca088e6c8f5767bdb1 | Alerts Cleanup and API (#39544) | Deferred | Movement, Medical, Interactions, GameTicking | Broad 18-file alert API and lifecycle rewrite touches gravity, stamina, internals, blood, buckling, crawling, and RMC alert consumers.
0740 | d89f0682e6e671975d1034e0051729ebe882b993 | fix a typo in the comments for game.ipintel_exempt_playtime (#40129) | Ported | — | Accepted as downstream commit df51bbcc6d; the malformed IP-intelligence exemption XML documentation is corrected.
0741 | b2c910683930373ae1480f0683978cc6c0a0202e | Vulpkanin Species (#37539) | Superseded | Movement, Medical, Interactions | Later Nubody and species reorganizations replace this 403-file intermediate species implementation, while CMU already has a divergent RMC Vulpkanin.
0742 | f45bf4590f0026cf6f5fcff3beb32b3213cce06a | Automatic changelog update | Irrelevant | — | Generated changelog only.
0743 | 828b1f2044900eca9122c710cc6025fcc08c1291 | Rejig LogStringHandler (#30706) | Deferred | Chemistry, Interactions | Seventeen-file admin-log serialization and interpolation rewrite affects entity, mind, session, chemistry, botany, and RMC log call sites.
0744 | e93177145964e6b7cd4ab92d2531e15f24d29115 | Expedite gender reassignment (#36894) | PortCandidate | Interactions | Target retains the chameleon gender-pin base and tag, allowing pins to change appearance; CMU has only static pins.
0745 | a9ffbdcdae4d35217fe602b43f83de479c1883d7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0746 | d4b9b45bdd09995bf0db7404cb9822f317beb5cd | Adjusted minimumPlayers for Wizard midround events. (#38424) | PortCandidate | Gamerules | CMU retains the ten-player Wizard threshold while target-final raises it to thirty; this is an explicit event-balance decision.
0747 | d1c41d0373f758371f8a6a7e580891a1241adaaf | Automatic changelog update | Irrelevant | — | Generated changelog only.
0748 | 8f44b5e30b87f4b5e48c0d065e12b4353a8c8890 | Add water cooler interaction test (#39612) | Irrelevant | Chemistry, Interactions | Integration-test-only commit with no production delta.
0749 | 5d3de5d1aa9bae43288d796c78f4be73fc820ce4 | Add a space in osx-arm64 to fix arm64 osx builds (#40137) | Superseded | — | Later publishing-workflow revisions replace this intermediate runner-label correction.
0750 | 816f6ed2fcc722dad34b282b93293deac362567f | Fix admin logs going to admin chat (#40141) | AlreadyPresent | Interactions | CMU's current AdminLogManager already starts the alert aggregation flag false, preventing ordinary logs from leaking into admin chat.
0751 | dcd0f10070f3176fd7a47003b9b5f95f789ca2ef | Drink outta da toiler (#40133) | Deferred | Chemistry, Interactions | The toilet drinking prototype requires EdibleComponent, which is absent while CMU retains the older Food and Drink systems.
0752 | d8400c65205bee5fd86246b0a9aa1ffec4e86495 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0753 | d02aa1a4e2e106b9bfd8e9516464c9dbd86df7ca | Disable vulpkanin human hair (#40144) | Superseded | — | Later Vulpkanin body/species customization replaces this intermediate SpeciesPrototype flag.
0754 | f98fd98380b93753fec6c0f2be8bb4ac3916afdd | Fix bad loop in LogStringHandler.AddFormat (#40147) | AlreadyPresent | Interactions | CMU already has the corrected loop condition that stops after adding or finding an equal formatted value.
0755 | 3f11e20f9076c2d4ab827f7f5827df7ebabc89f6 | Fix exo burn chamber (#40152) | Irrelevant | — | Exo saved-map-only change is outside the active RMC map flow.
0756 | 47629fe277a74df3f03a420e9e4be94e14b0ecb1 | Automatic changelog update | Irrelevant | — | Generated map changelog only.
0757 | 3aff3dff93d5182e445eada0dfd748c18f72a363 | Fix resin windows inheriting wrong dP values (#40151) | Deferred | Physics | Prototype cleanup assumes the unported delta-pressure hierarchy and must preserve RMC xeno resin-window behavior.
0758 | 07fdd52756e2cb3ce744bbf645f46f2dc26c28c9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0759 | ca32d83417e1a33eccd64b84d286bdb03596ddbb | Stable Into Master (#40155) | Deferred | Medical, Interactions, Physics, GameTicking | Mixed food-sequence, cream-pie, polymorph, and ingestion merge intersects CMU's older nutrition systems and RMC polymorph behavior.
0760 | 2201d290482b11354288f844a04e19239e750be2 | Revert antique laser and appraisal tool sizes (#40158) | AlreadyPresent | Shooting, Interactions | CMU's appraisal tool and antique laser already match the target-final reverted item sizes.
0761 | e761cf5afe6701e99a8b895a5485e65a2a33b033 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0762 | 912aaf846a2050311c6245d63bbd356342c2eb13 | Fixed disconnected grid on box station (#40161) | Irrelevant | Physics | Box saved-map-only correction is outside the active RMC map flow.
0763 | 8bd1970337fcaf33ac837fb022caf02e3e1ae020 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0764 | 4eab05b55e538ea3f7fd7d30b4f145524d90d8a6 | Add some alternate jumpsuit designs which can be toggled (#31213) | Deferred | Interactions | Thirty-seven-file foldable uniform, localization, prototype, and binary asset feature requires RMC clothing and hidden-layer reconciliation.
0765 | 40fcc0a45a709a8a0634869f882e9d6b6cbe7504 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0766 | 7aaa063944ba378413d7b1d30f400fc5351cd2ea | Update Credits (#40187) | Irrelevant | — | Upstream credits snapshot only.
0767 | 8f8db391d8c1e589d7703d50de142ce6f20fceb9 | Atmospherics Delta-Pressure YAML refactor (#40174) | Deferred | Physics | Twelve-file threshold and prototype hierarchy rewrite depends on the unported delta-pressure system and RMC atmos review.
0768 | ae9f56b234bdfd8d01625d5207a4798cda99c50e | fix: Atmos dP Window Inheritance (#40192) | Deferred | Physics | Follow-up inheritance correction belongs with the deferred delta-pressure YAML migration at 0767.
0769 | fb454351d2beacfb8964f38393b1f1c7994d7029 | Restore transfer amounts on regular syringes to 5, 10, 15 (#40197) | AlreadyPresent | Medical, Chemistry, Interactions | CMU's regular syringe already exposes the retained 5, 10, and 15-unit transfer choices.
0770 | d14b6a31aa6e470bcd6a613866c8847f84de6199 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0771 | a699639834fcd7349844c3ff78673ca73d428a12 | Allow Vulps With Human Hair To Be Shaved Without Clyde Joining The Circus (#40171) | PortCandidate | Interactions | Target retains displacement-map and appearance handling that avoids invalid Vulpkanin hair states; adapt it to CMU's RMC species paths.
0772 | c1ca510e781a4eafb90356b9044db27072379211 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0773 | 499dde1ec1b43c2cb52468200e2493b0adfc2ef0 | Bug fix for APCPowerReceiverBattery (#40188) | Ported (CS-0130) | Physics, GameTicking | Accepted as downstream commit 4b40bc12d7; paused battery-backed APC receivers no longer mutate power state or emit redundant events.
0774 | a93f6b8cdf5bdc4c0bf509e0706126eea500b531 | Atmos dP Guidebook Entry (#40194) | Deferred | Physics | Guidebook and component descriptions depend on the unported delta-pressure simulation and prototype chain.
0775 | 946e9cc2cd625e3cfb5bbc68ab2ffc91dc561c40 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0776 | 2315ea6ac295b4e60e12a150cadb94204762ebce | Being grappled with a grapple gun allows you to cross chasms (#39983) | PortCandidate | Movement, Shooting, Physics | CMU lacks the retained chasm exemption for entities connected through grappling joint relays; adaptation depends on its older grapple implementation.
0777 | 88e927f10ac7efca8fad5475626160ba2b7e3d73 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0778 | f885075d2e266ac81dd3be6bb8a05e557b9dd51b | DoAfter support for Actions (#38253) | Deferred | Interactions, GameTicking | Eight-file action/DoAfter state and component migration affects prediction, cancellation, serialization, and RMC action consumers.
0779 | 905935e6edb61311db105bb195fe6872f9804cc5 | Lets diona sap trigger artifact blood nodes (#40211) | Ported (CS-0126) | Chemistry, Interactions | Accepted as downstream commit b75ba44c54; Sap now satisfies the existing xenoartifact blood trigger.
0780 | db94ef5a5013b51d1b18e5775b7c7efa9e5e63e7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0781 | 82b94ec6de854723ff3b8c67c26fcd2d9e948011 | Derelict Mediborgs can Scan Solutions and see Mob Health (#40206) | Deferred | Medical, Chemistry, Interactions, Gamerules | Borg module additions depend on the broader deferred derelict-cyborg content and must be reconciled with RMC silicon modules.
0782 | 874160c8f8872a0b95859020d0c59b0fa459bd5b | Automatic changelog update | Irrelevant | — | Generated changelog only.
0783 | cb4bbf3d382f346e9662c402a5799b5061880add | Reworded the Galoshes description to be more clear about what they actually do. (#40200) | Ported | — | Accepted as downstream commit 08fe13e17a; the description now explains protection from slipping rather than merely being waterproof.
0784 | fca45851cc7d7c146615c3dd68f9dec5a89cefbf | Automatic changelog update | Irrelevant | — | Generated changelog only.
0785 | 0c9752027623a22be7367b453e2a8e82fc68c03c | Fix usages of TryIndex() (#39124) | Deferred | Movement, Shooting, Medical, Chemistry, Interactions, Physics, GameTicking, Gamerules | Mechanical 136-file prototype lookup rewrite spans nearly every content domain and must be split around RMC-specific consumers.
0786 | 088fa2013dd38c7cad31f2a91075c2315a164419 | Cleanup: Remove unnecessary ``IEntityManager`` reference from the ``EmotesUIController`` (#40243) | Ported | — | Accepted as downstream commit 33a45fe485; the controller now uses its inherited entity manager.
0787 | da210e812b0c4d8af906090b5a6e59f950d54fd3 | Make location in crew monitoring console localizable (#40247) | Ported (CS-0139) | Medical | Accepted as downstream commit 213d3d376a; focused crew-monitor coordinates now use the retained nav-map localization key.
0788 | c7406f65abfbd068403130f2da6148e22d2757e2 | Make Foldable Clothing Hidden Layers "reset" Hidden Layers when un/Folding (#40251) | Ported (CS-0129) | Interactions | Accepted as downstream commit 8e1b6f4314; both fold directions now assign or clear destination hidden layers symmetrically.
0789 | 35d69e0f33cfc33f349f045be7254dd2f36e7067 | feat: SimpleRadial menu support for sprite-view and more extensibility (#39223) | Deferred | Interactions | Twelve-file radial-menu and consumer rewrite intersects RMC UI, ghost-role, station-AI, RCD, emote, and absent Changeling flows.
0790 | a05d466a5e529fda4d2d3ed1bbb1926164b09acd | Decal spawners spawn on a higher layer (#39956) | Ported | Physics | Accepted as downstream commit a54eed3e97; random decal spawners now use the retained higher placement layer.
0791 | 960174acc5f90e6735f877d1715db699878814e6 | Fix RGB staff not working (#40258) | Ported (CS-0140) | Interactions | Accepted as downstream commit c3da28d9cf; the RGB staff action now carries the TargetAction component required by validation.
0792 | 7ad2d73605db27555c9886a8b5598ace18a51aa1 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0793 | a5ef016f1e3afc0d4cd89a5c1b810e13834bd09c | Make "Confirm" in VerbMenuUIController localizable (#40248) | Ported (CS-0141) | Interactions | Accepted as downstream commit 1e936a88dc; confirmation verbs now use the existing localized generic-confirm label.
0794 | 3da0b0299f30db96477525926e2d3bc396765e21 | Add support for contraband text to the reagent guidebook (#37113) | PortCandidate | Chemistry, Interactions | Target retains reagent contraband severity data, guide rendering, localization, and shared helper behavior absent from CMU.
0795 | ebfcddc62f5179865684a35311cab03d5c86400e | Fix emergency evac shuttle console early launch mispredict (#39751) | PortCandidate | Interactions, GameTicking, Gamerules | CMU lacks the retained shared predicted console state and launch-time reconciliation; integrate with its emergency-shuttle overrides.
0796 | f7e3a2f88119da4b4ca677bbf7c8d4dade3c1fd4 | SpawnEntityTableOnTrigger (#39909) | Deferred | Interactions, Physics | Entity-table spawning depends on the absent shared keyed-trigger architecture.
0797 | b86094eb45599525c36c53cea416e1bef70d8544 | PopupOnTrigger (#39913) | Deferred | Interactions | Popup effects depend on the absent shared keyed-trigger architecture and its prediction semantics.
0798 | 1666e302c29b2700e7a6bf91b00e371ce8c3159b | Merge Stable into Master (#40263) | Ported (CS-0127) | Gamerules | Accepted as downstream commit b89a462992; only the retained role-identity restoration was extracted from this mixed merge.
0799 | 327f217e18925d03f72b898e038592cdd548da95 | Do after checks for being inside container (#39880) | Ported (CS-0128) | Movement, Physics, Interactions, GameTicking | Accepted as downstream commit 25c9bc609f; enabled target checks now require both range and container accessibility while preserving RMC opt-outs.
~~~

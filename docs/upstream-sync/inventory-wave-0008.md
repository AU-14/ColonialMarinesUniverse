# SS14 upstream inventory: wave 0008

Audit date: 2026-07-20

- Pinned baseline: `59633f6dc50e77dda8cefa344d87c7b01e06a810`
- Pinned target: `40ca2c7f90d11d27be5457d177c133f0947d1c08`
- Range: ordered first-parent commits, zero-based indices 1400 through 1599
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
1400 | 2f0538fa9aa599fde967ae3a26223d93469b359f | Made a new generic borg module for art; the Artistry Module! (#39679) | Deferred | Interactions | The borg module, recipes, tools, and assets depend on RMC's divergent borg-module inventory and should be reconciled as one feature.
1401 | f5afd99f38782ddd7c0faced7d52e3eb2ac955b5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1402 | e809073f6f8e0606ef60d07810ed123fa9ee2b38 | Folders and clipboards recycle into what they are actually made of now (#40954) | PortCandidate | Interactions | The small recycle-output correction is isolated, but its material yields and lathe recipes should be checked against RMC prototype inheritance.
1403 | f2b99c8eb502438029c81f431d620ab115848d93 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1404 | 86880a31942c54b7a092e0418185925eb3804d12 | Remove rag forensics cleaning (#40818) | Ported (CS-0203) | Interactions | CS-0203 removes CleansForensics from RagItem while preserving its ordinary cleaning and solution-absorption behavior.
1405 | 2d2ca483b3a35f44d817886f20d9766dc598109a | Automatic changelog update | Irrelevant | — | Generated changelog only.
1406 | 253c6b4e0a47827b888529e2e827c30c37f0a941 | Slightly resprited the service borg (#39764) | PortCandidate | — | The service-borg sprite correction is asset-only and needs a direct comparison with CMU's retained chassis sheet.
1407 | 68ea91d070d24d626f4b485acd74b3b496c598dd | Fix Space Villain tie message (#40958) | Ported (CS-0204) | Interactions | CS-0204 removes the trailing space from the Fluent key so simultaneous deaths display the intended localized tie message.
1408 | 6720e85c6f1c4175931c60143970ed6cfcf37814 | Add sprites for Vox organs (#40555) | PortCandidate | Medical | The Vox organ prototypes and sprites are self-contained, subject to comparison with CMU's body and organ inheritance.
1409 | feadb819892f7f917634ff57aedbdab1444855fc | Automatic changelog update | Irrelevant | — | Generated changelog only.
1410 | ae349a446a70abaa49787c42567a238a63932d8d | Cargo orders that contain beverages now come in freezers (#40955) | PortCandidate | Interactions | The crate-fill substitutions are small, but cargo container inheritance must be checked so beverage orders remain cold in RMC.
1411 | ccd47a00a3a26e11b7f93e09b6e3c6638c9a0bac | Fix AddReagent modifying to solution being added in some cases (#40959) | Ported (CS-0017) | Chemistry | CS-0017 adapts the additive ownership fix so a partial AddSolution no longer mutates the caller-owned solution.
1412 | 303e0aae177937f0a4271dd614479ee603c1fc66 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1413 | c65f0aeb316ff693c8476aa48fcf02b3c90511e7 | Make a lot more puddle stuff predicted (#38871) | Deferred | Chemistry, Interactions, Physics, GameTicking | The large predicted-puddle rewrite crosses solution containers, fluids, evaporation, drag/drop, and prediction contracts that diverge in RMC.
1414 | a7614c6ef78b36d51173730073946ce8c6b58394 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1415 | 219aeda2353caed8e37eea7ee943190dad06e66b | AddReagentOnTrigger (#39875) | Deferred | Chemistry, Interactions | The new solution trigger depends on the target trigger framework and solution APIs; integrate it with the retained trigger chain.
1416 | 7168fd9ed9a427c6f6b8f53496d943db0c3a417c | Trigger On Hitscan (#40964) | Deferred | Shooting, Interactions | Hitscan trigger components depend on the target ranged-event and trigger-system contracts and should land with that framework.
1417 | a2927b773cc6b161ff033b87eba60da55091246b | Update outdated comment re: borging & borg playtime requirements (#40886) | Irrelevant | — | This removes only an outdated source comment and has no standalone runtime behavior.
1418 | 3c1982a85fedb8a38a7bcf51ea05a22b1eea5a99 | All pens embed (#39104) | Superseded | Shooting, Physics | The later stable merge at index 1552 replaces the all-pens behavior with a restricted embeddable-pen hierarchy and exploding-pen activation fix.
1419 | 8837851b004c8af2e9d81d1d820134d9c885c942 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1420 | 24216b1bc877312a4f034da7307aaccbbd627468 | Fix Ninja EMP themselves. (#40968) | AlreadyPresent | Interactions, Physics | CMU retains the cancellable class event; PowerCell relays the same reference and NinjaSuit mutates it, so EMP cancellation already propagates without the target's record-struct signature churn.
1421 | 748ff7d49adb8fb5c9c887dd07c6b53ad8f53f03 | Update Credits (#40969) | Irrelevant | — | Upstream contributor-credit metadata is project-specific.
1422 | 2d08773acc28b665413fdda6b3c0b00e308c4f1c | Added the sidearm tag to energy magnum (#40974) | PortCandidate | Shooting | The single Sidearm tag addition is isolated but should be checked against RMC weapon-slot and sidearm categorization.
1423 | a10e3ef9936c4ae9ae7470becdc3c9f7d919b107 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1424 | 2c71b92a6007259ee8470a95aac64e4fe4d053bf | Document tags: H-L (#40976) | Irrelevant | — | This is tag documentation and declaration-order cleanup with no standalone gameplay delta.
1425 | cb261babd8ffae155efa0f57e533e329154007a4 | Ninja headset (#40054) | Deferred | Interactions, Gamerules | The Ninja headset spans antag loadouts, communication prototypes, and assets that diverge from RMC's role and radio setup.
1426 | 69e2963945883a322f433cd5fa8f05023dec5199 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1427 | 1b7fa857336815b665f373246ab81ad28fb8b82f | Add EntityEffectOnTrigger and RejuvenateOnTrigger (#40967) | Deferred | Interactions, GameTicking | The generic entity-effect and rejuvenation triggers introduce shared systems and admin-path changes that depend on the retained effect pipeline.
1428 | 8e3af00f631f7bb0a6f25f8a12a2cba99f516596 | Adding cotton seeds to cargo seeds crate (#40970) | PortCandidate | Interactions | The cotton-seed cargo fill is a small prototype addition, subject to RMC cargo and botany availability.
1429 | eba8ea87aa6f16ab72417853242b2ecbe06d7436 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1430 | 29018beccea46388bc52b99a7e5b3d6ed399f926 | Fix some crystals (#40985) | PortCandidate | Interactions | The crystal prototype corrections are small and should be compared with RMC material and construction inheritance.
1431 | 239b4ba00983248be7d1512866a5fa94ad1507b5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1432 | 8b8357792f9baf5ab53db9852fbfd054f40c29d9 | Lets monkeys & kobolds shove/disarm! (#38542) | PortCandidate | Interactions, Physics | The one-line animal interaction capability is isolated, but shove/disarm behavior should be checked against RMC mob combat and collision rules.
1433 | 1541c107e5f9d898fafd1a283b30d0020a687320 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1434 | 1469b9484dd3f5c3c89f40f1a9998063cacb8a9d | Add virtual chat API methods in Shared (#40895) | Deferred | Interactions, GameTicking | The broad shared virtual-chat API migration touches many server systems and must be reconciled with RMC radio, speech, admin, and NPC extensions.
1435 | 3ea2b1b1822f1b57fe4d48c6e004be1f161c9f2b | fix arachnid lungs (#34381) | PortCandidate | Medical | The arachnid lung prototype correction is isolated and can be checked against CMU's retained body graph.
1436 | e44083b2806a8d981a0fa446283b428d463ae298 | Silicon lawset book and Law boards can now point to the list of lawsets. (#40944) | Deferred | Interactions, Gamerules | The law-board and book rewrite depends on Silicon lawset prototypes and guide links that diverge in RMC.
1437 | edfa3d92f4c8b5a09395fbff7aa69b2a07787903 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1438 | 545cacbcaeef497fcb953b047921a9f413bfee27 | `StyleNano` removal: Palette system and Sheetlets (#29903) | Deferred | Interactions | The 217-file StyleNano replacement is a foundational client UI migration and requires a dedicated CMU/RMC stylesheet reconciliation.
1439 | 9c44c1707eca28dab3011c631dd86016d7ff453a | Automatic changelog update | Irrelevant | — | Generated changelog only.
1440 | 22fe5185a3c121f50c30afd470d55d969f7f838c | refactor: new overload for SharedRandomExtensions.HashCodeCombine (#40990) | Deferred | Interactions, GameTicking | The hashing overload changes deterministic prediction seeds across effects, throwing, nutrition, and other shared systems and should be audited as a unit.
1441 | 1bbf958171f629374c042135f3578ee0f306cf4e | New job lizard plushies + Job-specific trinkets loadout (#34127) | Deferred | Interactions, Gamerules | The large plushie and job-trinket feature crosses role loadouts, cargo fills, localization, and many assets that diverge from CMU jobs.
1442 | 5e4728c1beaf4f16ebd5455e09ed9ff783513418 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1443 | 218c31630a6d3ec066c9bf1f323f0087305f97a6 | Large thruster (#37681) | Deferred | Physics, Interactions | The large-thruster prototype and animation depend on RMC shuttle physics, power, and mapping balance.
1444 | 8dde7f00389570209fa3fd79c38ea0266e0b3a3b | Automatic changelog update | Irrelevant | — | Generated changelog only.
1445 | 2328b3faa11d0c7403461020f42f92af2779df3c | Slime organs metabolizing slime restores blood level + halves slime hunger satiation when consumed by a slime organ (#32537) | PortCandidate | Medical, Chemistry | The slime-organ metabolism values are isolated reagent data, subject to CMU organ and hunger-balance review.
1446 | 7e0f15e1c7a49884bdb01204d14e362bd25ed4c6 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1447 | 31808fde3e8aab083bb0fcf14f3c8400aaacd7b4 | Added the cosmetic carp suit to the autodrobe inventory (#40995) | PortCandidate | Interactions | The vending inventory addition is isolated and can be reconciled with CMU's theater wardrobe contents.
1448 | ed307860d2b215761299658e694df3e82eb9d127 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1449 | 5da71c27e3cb3ec768269e44eba6e0783ef76be5 | Remove StressTestMovementComponent (#40993) | PortCandidate | Movement | The obsolete stress-test movement component and debug prototype can be removed after confirming no CMU stress tooling still references them.
1450 | b92f5dc533621c854ae9f6438a9a24d4cd695d3e | Consistency fix for soap making (#40998) | PortCandidate | Chemistry | The soap reaction consistency fix is a small recipe-data correction, subject to RMC's active reaction set.
1451 | 43e6c524a4b938192cc8faf0df95c36c57cccd8e | Zombies can't hurt II. (#41007) | Deferred | Medical, Interactions, Gamerules | The zombie damage rewrite depends on RMC's divergent damage, zombie-role, and friendly-fire behavior.
1452 | b4d148a016b1e50f577eec65ebaf22dff1a2fd9b | Automatic changelog update | Irrelevant | — | Generated changelog only.
1453 | e3880a3c4370c75486d60ec4790b3377b3334690 | Criminal console status expansion (#36244) | Deferred | Interactions, Gamerules | The criminal-status expansion crosses shared enums, server records, client UI, icons, and localization that diverge from RMC security records.
1454 | 1c799515a7387a5285c3ef744df4b1d259618094 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1455 | 8634cec82a765fe9fe1db7ca2299f0c5aa5c2c1c | Allow matches to be placed into ash trays. (#41016) | Irrelevant | — | This merge-parent commit has an empty effective first-parent delta, so there is no standalone change to port.
1456 | 401f461fcd91f47faa383187b42df2994c20ac11 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1457 | bba83e88b0b59507cfe912a1ec19b5daa0358718 | Allow upgraded solars to take structural damage (#40992) | PortCandidate | Physics | The solar prototype cleanup is isolated, but structural-damage thresholds and upgraded-panel inheritance need RMC power-balance review.
1458 | 7bef430b2143801f6090c4820a0a8a9cd11346c7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1459 | 20e0c41995ad82c2010d7e84bcca206e6b7dd2c6 | goats eat kudzu again (#40220) | Deferred | Medical, Interactions | Restoring goat kudzu consumption spans nutrition components, ingestion logic, NPC utility scoring, and RMC-specific food behavior.
1460 | 0086ee305a2f1c2d8019baf314c93b5561ec031d | Automatic changelog update | Irrelevant | — | Generated changelog only.
1461 | 28ca7d011f497be2326d683f3ab5dc16302662e0 | Update Controls.xml (#40978) | PortCandidate | — | The controls guide update is user-facing documentation and should be adapted to CMU's actual bindings before import.
1462 | a77877b948c7b0fdc9183056c89365b9bbf253d5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1463 | 479e5f61d684296fbf62f2e467685a3bcea09ec1 | Prevent freindly fauna node from spawning hostile mobs (#40979) | PortCandidate | Interactions, Gamerules | The artifact fauna-node filter is localized, but its entity tables and friendly/hostile classifications require RMC xenoartifact review.
1464 | 3b210fc28f9d84b4a240cf955e42028aa03428b8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1465 | 0241a4855fc08873f1440c2926cb75a328cf254a | Fix IdCardConsoleSystem NRE (#40994) | Deferred | Interactions | The ID-card console null fix sits in an access system touched by the target chat API migration and RMC access extensions.
1466 | 3bbc1e1dde0006f165b0e2362e588bbd70edef56 | fix species name in station records (#36217) | PortCandidate | Interactions | The station-record species display correction is client-only and can be adapted around CMU's retained record UI.
1467 | 0a0806ac78ea457fa834a4d331d6557ae353babc | Feature/door remote radial (#36378) | Deferred | Interactions | The door-remote radial feature is a broad shared, server, and client UI rewrite that must preserve RMC remote access and door behavior.
1468 | d0dd5b21d9c7562fd9a1c8a91073a1e6bfd7c5e6 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1469 | 578b4c61df5526f3dea7dea0e551fa418da06bde | Add Integration Testing for issue #40868 (#40972) | Irrelevant | Medical, Interactions | This commit adds only an integration regression and no standalone runtime behavior; its scenario can be retained when the related nutrition fix is integrated.
1470 | 31b0a52235f54f8fb518351a214ac7955f68b968 | Changed Vox Head Marking Point Limit To 4 (#40542) | PortCandidate | — | The Vox marking-point limit is a one-line preference prototype change, subject to CMU character-customization policy.
1471 | 43b9d71973a99b4d3d8bc736ecf1ca5f409f4ddf | Automatic changelog update | Irrelevant | — | Generated changelog only.
1472 | 5a0a984aa8f278f602cd5689d3eaf6f6cfb3f30c | fix: make IdentityComp.IdentityEntitySlot optional (#39357) | Deferred | Interactions | Making the identity entity slot optional changes shared identity contracts and UI assumptions that need RMC identity and disguise reconciliation.
1473 | 1250b388f382f534673843d6fcc1d4c9a5772cd2 | Mosin be tested, Verin be breaded. (#40957) | Deferred | Shooting | The weapon-test commit also adds public ammo-count APIs to SharedGunSystem and should be reconciled with RMC magazines and gun overrides.
1474 | 04a2c2e9685dc78c4eebc61442ea6661fe571b91 | Don't show NaN/infinity if AME has no cores (#41026) | Ported (CS-0205) | Physics, GameTicking, Interactions | CS-0205 guards the AME output estimate when its node group has no cores, preserving zero output instead of NaN or infinity.
1475 | 4aac3dbc9dbdbca541097435b41ae9dd76b9e8b9 | Fix Being Drunk! (#41002) | Deferred | Medical, Chemistry, GameTicking | The drunk fix crosses metabolism, status-effect timing, speech, and client overlay code and depends on the target status-effect migration.
1476 | 393197e94f2ef3c4cffa48de9091df5bf17f568a | Automatic changelog update | Irrelevant | — | Generated changelog only.
1477 | 09aada2e3ea7a89dea847867c2e7dad4ce4202f3 | Fix refresh button in fax machine (#41024) | Deferred | Interactions | The fax refresh-button fix depends on the target StyleNano and sheetlet migration from index 1438.
1478 | c6352786f1abef3623d2a991e4430c275ecdff3a | Add doafter to filling the hypopen (#40538) | Deferred | Chemistry, Medical, Interactions, GameTicking | The hypopen do-after feature is a large solution-transfer and interaction rewrite that must preserve RMC medical tools, prediction, and refill semantics.
1479 | 672c837786ad0f9e97073b97fab73b94acb2fd34 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1480 | 18feb67ff5444cfa5527fdcdf61132c23504a2b2 | Fix style on paper editing background (#41009) | Deferred | Interactions | The paper-editing style correction depends on the target stylesheet architecture introduced at index 1438.
1481 | 914ae617ac01edbd6cf22f2453230bb1600a551b | Add a sheetlet for ConfirmButton (#41011) | Deferred | Interactions | The ConfirmButton sheetlet is part of the target stylesheet migration and should land with that dependency.
1482 | 341dc4d3838bf8c17c8a6a5a06fee8cd5fe5a568 | Changed mindswaps cooldown from 5 minutes to 3 minutes (#41027) | PortCandidate | Interactions, Gamerules | The mindswap cooldown is isolated prototype balance and can be reviewed against CMU spell and antagonist pacing.
1483 | 19712e80900f87c1dd9049b9ff7fd0cb45da6d8a | Automatic changelog update | Irrelevant | — | Generated changelog only.
1484 | ad2e70f3d74b27715b111667fdca604f88e00573 | Move ChemMaster buffer sort button out of transfer/discard button group (#41018) | PortCandidate | Chemistry, Interactions | The ChemMaster layout correction is a one-line client UI change, subject to CMU's retained chemistry window.
1485 | 599af998f73e6ef28dc2e66cc5a9aadbb5dfee44 | Remove reference to Velcro (#41032) | PortCandidate | — | The cloak wording correction is isolated prototype text.
1486 | 7cfd957c1f34d4ee9c73990b3abbbdab2ae2f882 | added seclight to hos locker (#41031) | PortCandidate | Shooting, Gamerules | The locker-fill addition is isolated but should be reviewed against CMU security equipment and map balance.
1487 | 151f3bfd4544deb203e4ebd4df88846cd85b5d1a | Automatic changelog update | Irrelevant | — | Generated changelog only.
1488 | 31dd4ed0bdfbc01fa28059ca5cfb79bf10673003 | Changes Slippery Slope to not require a robe and hat for casting. (#41038) | PortCandidate | Interactions, Gamerules | The two-line spell prerequisite removal is isolated but changes wizard balance and must be checked against CMU's retained spellbook.
1489 | f9ef2d09a3862372dbe5eb4a3b2715c26c69de19 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1490 | 80d4f3d8f821a0c1fc20b2ad83a74015892ff10e | Toilet cistern stashes spawn containing basic loot (#41013) | Deferred | Interactions, Gamerules | The toilet-stash feature adds a broad loot table and secret-stash prototypes that need CMU loot and map-policy reconciliation.
1491 | cbb7c84fdaec7832bb1e41f794c73c091308f238 | Fix: LockSystem - HasUserAccess - Set DenyReason Localization Text Properly (#41012) | AlreadyPresent | Interactions | CMU's retained lock path already localizes its access-denied message before showing the popup, so the target localization fix is semantically present.
1492 | c6ea860ab4653997f0fb55822708c96fc1207f26 | Banana bread now shows up in the guidebook (#41047) | PortCandidate | — | The guidebook visibility flag for banana bread is an isolated recipe-data correction.
1493 | 9f2da5d650a5b811f0cf98e2ad11afef0aaf6bd9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1494 | 0abb5f0765fcd33189f1763b76d45769448ba175 | Remove a code comment (#41052) | Irrelevant | — | This removes only a source comment and has no standalone runtime behavior.
1495 | 6159801442fd29394aab0ddeae3df6feb8b25518 | Predict DestructibleSystem Part 2: First batch of entity effects (#41039) | Deferred | Medical, Interactions, Physics, GameTicking | The predicted destructible entity-effect migration changes damage, explosions, tiles, effect dispatch, and prediction contracts that diverge in RMC.
1496 | 043ad94262d48fe732b945f0ac31e070b3bcd6ed | Fix pre-round Discord ahelps showing incorrect round number (#41060) | PortCandidate | Gamerules | The pre-round Discord ahelp round-number correction is isolated, subject to CMU's retained Bwoink and external-chat integration.
1497 | 63b38a8a36a7ece30145428fe53e4e15bb06117d | Automatic changelog update | Irrelevant | — | Generated changelog only.
1498 | f1c95bfbb1a0b614a46007c6511db89c617cf580 | Hand labeler UI improvements (#40318) | PortCandidate | Interactions | The hand-labeler UI improvements are localized and can be adapted around CMU's retained labeler window and localization.
1499 | 3ff86e794e3eb773b5056e436da93a1131e8e6df | Automatic changelog update | Irrelevant | — | Generated changelog only.
1500 | d4a32ce50292540e8024664aed44065f4369c149 | Mild Entity Effect/Condition Cleanup (#41059) | Deferred | Medical, Chemistry, Interactions | The entity-effect and condition cleanup spans many polymorphic effect types and reaction prototypes and should follow the target effect framework.
1501 | eaf6441103556c0db97ee8c6967c7c6abc33eea9 | Fixed Mime Lizard Plush going "weh" when colliding with something or being eaten (#41063) | PortCandidate | Interactions, Physics | The plush interaction sound fix is a small prototype correction, subject to the retained CMU plush hierarchy.
1502 | 61b1ef3ca0db2e7d92cffabd1570acd9712984a6 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1503 | 9b6485094a0d1e7659923c871c90ac2117aa07ad | Gas recycler tweaks (#39212) | PortCandidate | Physics | The gas-recycler prototype and guide additions are self-contained, subject to RMC atmos balance and guide structure.
1504 | 8600286ed3dc94983e76593b9fb210da6ab5d140 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1505 | 7a811c229bd5260d8a1cdb1fd9dce3184f48b013 | PAIs will no longer get uplinks instead of traitors when a player is selected as an traitor (#41069) | Deferred | Gamerules, GameTicking | The PAI traitor-selection fix changes traitor and uplink assignment paths that diverge substantially in RMC gamerules.
1506 | 1894ff80657171cb7da0c8c477d6250b0a13443f | Automatic changelog update | Irrelevant | — | Generated changelog only.
1507 | da5e72d43ef062748f8a1b09337bab055d17d9e5 | Fix wielding two-handed items with only one hand (#40966) | AlreadyPresent | Interactions, Shooting | CMU's RMC-adapted CountFreeableHands already excludes the wielded item, preserving the target one-handed wield fix.
1508 | a97c0d35b1de5c9060d91c037692a44c8e91c1b9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1509 | dd9a1de77ffc68e26d097e4671aa269d4d56e724 | Fix radiation damage being misattributed to radiation receiver (caused artifacts to not be triggered by ambient rads) (#41065) | Deferred | Medical, Interactions, Physics | Radiation attribution changes event ownership across the radiation and artifact pipelines and needs reconciliation with RMC damage sources.
1510 | 9008f776ec52a1718e4b9cb8f466d2de97f69d60 | HOS & Warden Weapon Spawners (#40860) | PortCandidate | Shooting, Gamerules | The HOS and Warden weapon-spawner prototypes are isolated but require CMU security loadout and map-balance review.
1511 | 48e2b2d263c7c1c4fcad1413601311e1c6f2bcb4 | Delete FoodComponent, migrate prototypes to EdibleComponent (#41070) | Deferred | Medical, Chemistry, Interactions | Deleting FoodComponent is a broad nutrition and body-prototype migration that crosses many RMC food, organ, and interaction extensions.
1512 | 9e5495f1d9e4cab367656f1676d4841195e96151 | Fix a single vox jumpsuit displacement pixel (#41080) | PortCandidate | — | The single-pixel Vox displacement correction is asset-only and needs direct binary comparison.
1513 | 8212edaae6a4369f33fac033bffa6d568866bf12 | Delete an Unused Event. (#41083) | Irrelevant | — | The removed server event is unused and carries no standalone gameplay behavior.
1514 | f9e17647b5c0ea7734b71eae79a58c0c24fbb699 | Space Carp are fireproof now (#40820) | PortCandidate | Medical, Physics | The fireproof carp prototype change is small but changes creature damage balance and should be reviewed against RMC fauna.
1515 | 5dc60b4eb3c27502f4ae9ca4e5818363ea0352c9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1516 | 891f5a8f6ba76701dd447d6f33e27cc9029a673d | LaunchOnTriggerComponent (#39871) | Deferred | Interactions, Physics | LaunchOnTrigger introduces a new predicted trigger effect and toy behavior that depends on the retained trigger and velocity APIs.
1517 | c01ec294d015a6f222ae806374f179e9fa9bba08 | Reduce Triggers Boilerplate. (#41086) | Deferred | Interactions, GameTicking | The trigger boilerplate reduction rewrites nearly every shared trigger system and must be integrated with the full RMC trigger extension set.
1518 | c9fdac9364e16a3697481ffe469b11231e94f518 | `BaseSpawnEntityEntityEffect` scales its spawned entities by default (#41091) | PortCandidate | Interactions, Physics | The default spawned-entity scaling behavior is a small effect-contract change, subject to auditing CMU effect consumers.
1519 | 59aab967b6d1774ef589a7e1d26886749d7c51cd | Automatic changelog update | Irrelevant | — | Generated changelog only.
1520 | 39aada2018da32df380d66147347a731042667b2 | Backend vault-freezer cleanup (#41097) | PortCandidate | Interactions | The vault-freezer fill cleanup is isolated prototype data and can be reconciled with CMU head lockers.
1521 | 8d8af1bab7fc482b4b859426bc2f80995ff820a3 | Stack System Cleanup (#38872) | Deferred | Medical, Interactions, Physics, GameTicking | The large stack-system rewrite touches storage, hands, construction, healing, materials, cargo, tests, and many RMC extensions.
1522 | 12cd8100ec16e20c5a0c300e02006a2c4ce85914 | Purges uses of TransformComponent.Coordinates.set (#34937) | Deferred | Physics | The transform-coordinate setter purge is small but depends on the target transform API and should be reconciled with RMC buckle positioning.
1523 | 9591314ac42d0777e9118e8c38160cf5cde06f83 | Fix Lathe Cooling Guide Typo (#41100) | PortCandidate | — | The packed-map guide typo is a one-line resource correction, subject to confirming CMU ships the same guide entry.
1524 | 9a10a190721fe0d2394a025529c2f6cf777afde3 | Fix eating the whole stack of uranium. (#41092) | Deferred | Medical, Chemistry, Interactions | The uranium-stack ingestion fix changes nutrition and stack event contracts and should land with the target stack cleanup after RMC reconciliation.
1525 | 07629f271b6fc992ffb11cdd4be95bf15d27e07d | Automatic changelog update | Irrelevant | — | Generated changelog only.
1526 | d5535583973b0cfc69789a12c53c8518801ce2c4 | Update Credits (#41109) | Irrelevant | — | Upstream contributor-credit metadata is project-specific.
1527 | 53bb27fef71c6b6bf107864b7befe060c69d2f62 | Xenoartifact: Fix teleport effect (#41049) | PortCandidate | Interactions, Physics | The xenoartifact teleport correction is localized but should be checked against RMC transform and artifact behavior.
1528 | d1f159d31b88370b7d2e905bf285d79670b0e409 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1529 | d212f3cdae4a65186d6c330c5a4bda96765e0cb8 | remove a bunch of redundant IsFirstTimePredicted checks (#41119) | Deferred | Interactions, Physics, GameTicking | Removing prediction guards across singularities, portals, inventory, and species systems assumes target prediction semantics and needs per-system review.
1530 | 60f6527a11ab7031679cedc8082f3aacdb6cb1a1 | Set newplayerthreshold cvar for the development serverconfig (#41099) | Irrelevant | — | This changes only the upstream development server preset and has no CMU production behavior.
1531 | 4b24d2959e2d1281ce618270c72df64643cc8ef2 | Internals: prioritize gas tanks over jetpacks (#35068) | Deferred | Medical, Interactions | Internals tank prioritization changes shared body and inventory selection logic that must preserve RMC equipment conventions.
1532 | 03d8ca461b7b9580d0d0b3a79e621dbdd5fca075 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1533 | eb625a5b50724755a7179d008d59e7ab3989315a | Add Crazy Lube to the Toy Box. (#36292) | PortCandidate | Interactions, Physics | The toy lube feature is mostly prototypes and assets, subject to RMC slip behavior, cargo balance, and item inheritance.
1534 | 9683337de578cce3bbe115a5d63967847b4b8f9e | Automatic changelog update | Irrelevant | — | Generated changelog only.
1535 | 737a4f308eddb26b5bcbadef859c6f310feedb80 | Fixes .50 Uranium projectile sprite (#41068) | Ported (CS-0206) | Shooting | CS-0206 selects the existing animated, unshaded uranium sprite state without changing projectile mechanics.
1536 | c0839f35adbb4c24aa0b2f16430a08135bab963c | Automatic changelog update | Irrelevant | — | Generated changelog only.
1537 | 6046687a29b52cf197bc63fd60e2aacdbd7e4fb5 | Remove IsFirstTimePredicted from Ninja systems (#41127) | Deferred | Interactions, GameTicking, Gamerules | Removing Ninja prediction guards assumes the target action and prediction lifecycle and should be reconciled with RMC antagonist systems.
1538 | 3bd09630869d0e06c4de81780fe1fe3d6d9d7c8c | Fix DeltaPressure serialization spam (#41131) | PortCandidate | Physics, GameTicking | The delta-pressure serialization guard is small and isolated, subject to CMU atmos component serialization.
1539 | 5084fe456f706c7e0efd22f5948368a815648c2b | Nanotrasen is a word (#41124) | Irrelevant | — | This only updates the IDE dictionary and has no runtime behavior.
1540 | 82ab14da3ac73e5edd61a4cc8090a96174ddaa89 | Admin alerts now link players with tpto (#40472) | Deferred | Interactions, Gamerules | Linked admin alerts change shared chat, logging, explosion alerts, localization, and client parsing around RMC admin tooling.
1541 | 7dbf084940e3178d4f1acc754ceeac92976121a1 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1542 | 3a9bcf1a8380f145ed08f1923734f2dcfd28e027 | Damageable/Destructible Benchmarks (#41064) | Irrelevant | Medical, Physics | This is benchmark scaffolding and helper exposure with no standalone gameplay delta.
1543 | 32d6d7d4611e16014e5e3e98918a1aee98601522 | Fix delivery-spam.ftl tag typo (#41140) | PortCandidate | — | The localization-tag spelling correction is isolated.
1544 | 7c0ba70b74453e1eda62abcf09f900c4fb5d3313 | Fix TryProccessRadioMessage Typo (#41139) | Irrelevant | — | This is a coordinated method-name spelling cleanup with no standalone behavior.
1545 | 91292522b504b82187846e72a5d3d086a533cf7a | DeltaPressure Predicted Examine (#41135) | Deferred | Interactions, Physics, GameTicking | Predicted delta-pressure examine moves state into shared code and changes atmos update contracts that require RMC atmos reconciliation.
1546 | 7427bf79714e33ca6c98d026fe0df06e0d1557b0 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1547 | 4e716a64b80fa617bf55f79a37af56448e210bea | Document tags: M-Q (#41141) | Irrelevant | — | This is tag documentation and declaration-order cleanup with no standalone gameplay delta.
1548 | 41f6bcf00e21f6d8251c5b955d14663ecb387075 | Discord Command Arguments as List (#41113) | Deferred | Interactions, Gamerules | The Discord command-argument API change affects external command parsing and should be reconciled with CMU's retained Discord integration.
1549 | 85f607f1e67e398df169e21f5b27a1ec4e1daabd | make water cup spill when worn (#41148) | Ported (CS-0207) | Chemistry, Interactions | CS-0207 adds the retained SpillWhenWorn component so a filled water cup drains when equipped on the head.
1550 | cf66dd7b350dc394ba3afc1252b636ac96c9a93d | Automatic changelog update | Irrelevant | — | Generated changelog only.
1551 | cdbe92d37d65b6adc89d45f66825ad502ad4549f | Update DamageableSystem to modern standards (#39417) | Deferred | Medical, Shooting, Interactions, Physics, GameTicking | The 157-file DamageableSystem modernization changes a foundational damage API and every major RMC combat, medical, and destruction consumer.
1552 | 66ae600f43f8cf8922fdefe1908a3d4b46f59284 | stable merge 2025 10 27 (#41155) | Deferred | Shooting, Interactions, Gamerules | The effective merge mixes a projectile cleanup, the final pen hotfix, stylesheet changes, changelog data, and wizard roundstart removal; split and reconcile its constituents.
1553 | ce6daea5fb978e14d9f06f88a8430157eafc5f82 | More-generic bar flask name/description (#41144) | PortCandidate | — | The flask name and description generalization is isolated prototype text.
1554 | abd9aec8bd6ead47428cde8d1a202fc14a8daa6f | Implemented parenting and minimum default for loadout groups (#40861) | Deferred | Interactions, Gamerules | Loadout-group parenting and minimum defaults change shared preference APIs and must preserve CMU's divergent jobs and loadout prototypes.
1555 | db05016fe0dd744cd91faacba8c6ed78dad16eaf | Fix chemical explosion scaling. (#41153) | Deferred | Chemistry, Interactions, Physics | Chemical explosion scaling changes target entity-effect scale contracts and should land with the deferred entity-effect migration.
1556 | 3cd377df8e952b2e190816f4c6498996325edf6e | Automatic changelog update | Irrelevant | — | Generated changelog only.
1557 | b4e2e6862812d9f5a2737cb1697417b5e317e61d | Resprite and refactor wall dispensers (fuel, cleaner) (#36251) | PortCandidate | Chemistry, Interactions | The wall-dispenser prototype and sprite refactor is self-contained, subject to CMU map usage and solution-container inheritance.
1558 | ba14275fc85d4172c0ae45cc45717689af4bf30e | Automatic changelog update | Irrelevant | — | Generated changelog only.
1559 | 39fc0052a44c0fb6a3aeab628b7791c772a6d66e | Xenoartifact: Fix phasing effect (#41160) | Ported (CS-0208) | Physics, Interactions | CS-0208 resolves and softens fixtures on the physical artifact rather than on its effect-node entity.
1560 | 9526d2092aacdddab3ec11488da737f3634a9490 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1561 | 5ee1fa3e2c6094b8c0b8c9974e3b033828447b91 | update support email (#41166) | Irrelevant | — | The upstream support address and project-policy files are Space Wizards-specific.
1562 | c8b26adb38473aa83c11c5a337b25b3e573583eb | Diphenhydramine drowsiness maximum fix (#41169) | Ported (CS-0038) | Chemistry, Medical | CS-0038 adapts the target fix to the older status-effect API so repeated diphenhydramine no longer accumulates unbounded drowsiness.
1563 | b942c8aa138bb6b75707b915b5a2774b3770307c | Automatic changelog update | Irrelevant | — | Generated changelog only.
1564 | f039432aa5dfdd66b71daf922916663b53af4f88 | Stable merge (#41171) | Deferred | Chemistry, Medical, Interactions, GameTicking | The effective merge combines botany effect scaling, status-duration subtraction, solution-drag cleanup, and a Phalanximine threshold fix across divergent APIs.
1565 | 87a3b4fa5647753de6ff18473d356ed81dc2e855 | Rename kira special to the orange-lime soda (#41167) | PortCandidate | Chemistry | The drink rename spans prototypes, recipes, localization, migration data, and assets but is behaviorally isolated.
1566 | dcb761610728bb4dcea059e6f7b9f45645f06806 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1567 | 3f5bd8b565363565a83c6305f197c3aabeba8bf4 | Widen Ammo UI (#40570) | PortCandidate | Shooting | The ammo-counter width and layout changes are client-only, subject to RMC's retained gun-status UI.
1568 | b5fb76d629a8038c76bb6e6f60971a599a9c8b38 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1569 | 7e828cae028359181f007b67091f0e807007a5ff | Department heads can now approve the use of their departmentally-restricted items (#40565) | PortCandidate | Gamerules | The Space Law wording change is isolated, but CMU policy text must be reviewed rather than imported blindly.
1570 | 052b59172ebc16bad368245e6fa1f188407eb258 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1571 | 932d3948eb4d13b9b55080c1c0674f0c507c70a2 | General touchups to antagonist flavor text (#41184) | PortCandidate | Gamerules | The antagonist flavor-text edits are localization-only and should be adapted to CMU's available antagonist modes.
1572 | 519bc389cc916a73ce6ed61697b0d2a52be8fe0e | Automatic changelog update | Irrelevant | — | Generated changelog only.
1573 | e74e0b5c03009bacdc6581573e740081cf022d5b | Readd CutWireVariationPass with Cvar (#41191) | Ported (CS-0210) | GameTicking, Gamerules, Interactions | CS-0210 registers the existing CutWireVariationPass on standard basic roundstart variation without inventing the reverted CVar gate.
1574 | 37f7df66bf6e7cff504ea8add89dffa6dcf47019 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1575 | 5cbc1cba48ba12067b7a3053393f9cdc4654331b | Rejuvenating Resets Item Charges (#41165) | Ported (CS-0209) | Medical, Interactions, GameTicking | CS-0209 routes rejuvenation through ResetCharges so limited charges and their recharge timestamp return to configured maximum state.
1576 | 040ceb162b033b72227fb03fcc86e1e729f3abb0 | TemperatureSystem Base Class Initialization (#41196) | AlreadyPresent | Medical, Physics | CMU's server TemperatureSystem no longer derives from SharedTemperatureSystem; the shared subscriptions run in their own system, so the missing target base call is not applicable.
1577 | a181063eafcc8ed7dd9c27d4fb891b24f646ba84 | Remove warnings in Pow3r (#41195) | Irrelevant | — | This only suppresses warnings in an auxiliary upstream tool.
1578 | 1cc726a6097ecda5560993a7ffed5e66585d8212 | Allow pacifists to use disabling modes of energy magnum and energy shotgun (#41029) | Deferred | Shooting, Interactions, Gamerules | The pacifist fire-mode exception changes combat-mode, weapon-mode, and prototype contracts that diverge across RMC guns and pacification.
1579 | 6d53307711eb00b9f17f75c8168a90cce00af962 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1580 | 49860b820cb5fe9953bcff21206c6a2388a4126c | Change the recipe for licoxide to not require lead (#40991) | Ported (CS-0029) | Chemistry | CS-0029 preserves the lithium-for-lead Licoxide source correction while the standard reaction file remains intentionally dormant under RMC's ignore policy.
1581 | d671b3f64a40ee7942c98b7613e555c2733a6385 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1582 | ed86c8bad705b34393929f1058c2ae3a67eb5968 | fix typo in migrations (#41203) | PortCandidate | — | The prototype migration typo is isolated data cleanup, subject to confirming the same migration is retained by CMU.
1583 | 036fef8054ce390540d86d9e417e0cbe88bc137b | Fix the ethanol guidebook entry (#41192) | Deferred | Chemistry, Medical | The ethanol guidebook fix also rewrites metabolizer organ prototypes and must be reconciled with RMC's divergent body and metabolism setup.
1584 | 3f5067693f38d3266cd56489f45cec10ed429fa3 | Remove the remaining CheckButtons (#41073) | Deferred | Interactions | Removing the remaining CheckButtons depends on the target StyleNano and sheetlet migration from index 1438.
1585 | 07072bbf39ec3f33f4b2a277b968c06772bd475b | Rules tab in changelog (#40257) | Deferred | Interactions, Gamerules | The rules-tab feature changes changelog structure and policy presentation and must be adapted to CMU's own rules content.
1586 | b873eabc16c5560330e11e6d402444566914c43f | Fixed formatting for space law section "Major Punishments" (#41206) | Irrelevant | Gamerules | This only reformats upstream Space Law policy text, which is project-specific and cannot be imported as gameplay parity.
1587 | 8c406cbe386d3138b90dbc03c0ae4a61fd96cc99 | Remove unused includes in Ninja (#41207) | Irrelevant | — | This removes unused namespace imports and has no standalone behavior.
1588 | d0ac7d0b39134a9a7d847d375f875a041d849bd1 | Add a new gas React() benchmark (#41202) | Irrelevant | Physics | This adds only a gas-reaction benchmark and no runtime gameplay behavior.
1589 | ec8ada33889f1c7d7d44556d0a1efe037a7156a8 | Fix all ignored recipes in material arbitrage (#41134) | PortCandidate | Chemistry, Interactions | The material-arbitrage fixes are localized recipe and test data, subject to RMC lathe recipes and active prototype sets.
1590 | bde62ca0f28a63fa0df6a9d8c8ab54fd6cdde9a1 | Fix client crash in MeleeWeaponSystem (#41121) | AlreadyPresent | Shooting, Interactions | CMU's client melee effect loop already uses TerminatingOrDeleted, a stronger guard than the target's deleted-entity check.
1591 | ae2067c5beac43e4e43269ede6dd639393fa9618 | Add 2 New Reagents (Felinase and Caninase) (#41136) | Deferred | Chemistry, Medical, Interactions | The two reagents add speech systems, status effects, flavors, recipes, and metabolism data that must be reconciled with RMC species and accent systems.
1592 | c30321d886c398dc158788227144ea2c87838ac2 | Remove inaccessible code (#41209) | Irrelevant | — | This removes unreachable code and has no standalone behavior.
1593 | dd61991b1c213d8018ffd88e07ac85b900cb3810 | Add multi-job exclusion support to objectives, and add more appropriate job restrictions to certain thief objectives. (#40065) | Deferred | Gamerules, GameTicking | Multi-job objective exclusions change objective component APIs and thief/traitor prototype policy across divergent RMC gamerules.
1594 | 050dae8aa79be582e40118fea3b5551f0ce4b953 | Remove unused dependencies (#41213) | Irrelevant | — | This removes unused dependency declarations and has no standalone behavior.
1595 | ce920e6f0d3e5e9c6765e407a938cd0050b1c34f | Remove double includes (#41211) | Irrelevant | — | This removes duplicate namespace imports and has no standalone behavior.
1596 | b6c674ac7797dbd3c0e7a4ec7258f953468582e1 | Manual changelog push | Irrelevant | — | This is a manual upstream changelog publication with no standalone gameplay delta.
1597 | ed47827d56a0756ce943ad957451f5209cd58466 | Fix changelog part 2 (#41221) | Irrelevant | — | This corrects only upstream changelog metadata.
1598 | 79035cd023c629743b30367abdabaa123ec92f57 | Fix Assumption of Nullable to have value (#41220) | PortCandidate | Shooting, Interactions, Physics | Nullable projectile provenance prevents invalid assumptions during embedding and grappling and is a focused fix to reconcile with RMC projectile extensions.
1599 | d893cda971cffe231e921da8d99298a4d534bbda | Fix for Tesla Twins Miniboss (#41199) | Deferred | Physics, Gamerules | The Tesla Twins miniboss fix changes singularity-generator lifecycle state and should be reconciled with RMC Tesla and event behavior.
~~~

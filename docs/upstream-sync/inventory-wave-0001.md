# SS14 upstream inventory: wave 0001

Audit date: 2026-07-20

- Pinned baseline: `59633f6dc50e77dda8cefa344d87c7b01e06a810`
- Pinned target: `40ca2c7f90d11d27be5457d177c133f0947d1c08`
- Range: ordered first-parent commits, zero-based indices 0000 through 0199
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
0000 | 326712faca1555f637947bd0f857242bb35a6963 | Predict RepairableSystem (#38886) | Dependency-blocked/deferred | Interactions, GameTicking | Prediction crosses RMC repair interactions and needs a focused reconciliation.
0001 | 00826aaad69a7c8e66887bbef8dd25cf86a8871b | Replace usages of customTypeSerializer PrototypeIdListSerializer with something that doesn't take 20 separate words to type out (#37959) | Dependency-blocked/deferred | Chemistry, Medical, Gamerules, Interactions | Thirty-four legacy serializer uses require a compile-valid strong-ID migration batch.
0002 | f915157b9638bede47ee814c96e218c6b675225f | Fix yaml linter and misc errors (#37444) | Dependency-blocked/deferred | Shooting, Physics, Interactions | This mixed lint and content bundle needs behavioral changes separated from obsolete API cleanup.
0003 | 0484b7f07e32e7b985f4c243ec1a5aa387a020e5 | Add VV button to the solution editor (#38889) | Port candidate | Chemistry | CMU's solution editor lacks the target-final view-variable action.
0004 | 5e9b9a55eb45fbf48a367f5af252dabd0604dc58 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0005 | 80c6650730d3d019b318ca0af8ed1269431746f8 | fix: wide swings with resistanceBypass now bypass resists (#38496) | Ported (CS-0071) | Medical, Interactions | Wide swings now honor resistance bypass consistently across all struck targets.
0006 | 5fbbb6fd0b6d25bca071c808852c7fc4add0976c | Allow pAIs to emote like a borg (#38425) | Port candidate | Interactions | The pAI emote permission retained by the target is absent downstream.
0007 | f574990b11db5936de3635a4273d5eb26217882e | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0008 | 415ba2e2749b16102dda9b3c90932e5ca4032766 | reduced motion flash effect version 3 (#37824) | Dependency-blocked/deferred | Medical | Upstream later replaced this implementation with newer per-effect flash controls and status APIs.
0009 | 9b85def0a79a694b6557a3f7cb455fd223bd8d04 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0010 | decaa58dfe09f3e17c3f6f798013d5b8a6fc703a | feat: allow admins to interact under subfloors (#38813) | Ported (CS-0055) | Interactions | Admin bypass interaction now ignores the subfloor obstruction marker.
0011 | 3896dbb375e287727f939c9016be7372467771f7 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0012 | 5a3368b0fa40c15aabcc74267cc1aa4055b15e04 | Operation Remove Gun Bloat (#38104) | Dependency-blocked/deferred | Shooting | The broad weapon-content and balance removal needs explicit fork review.
0013 | 5427d386ce5731882b832ef6783e190e114943f9 | Minigun inhands + HMG multihand and slow move speed (#35344) | Port candidate | Shooting, Movement, Interactions | Target retains the multihand, movement-speed, and in-hand updates missing downstream.
0014 | b7f31ac482cc20455c4984f29fa7dacbd36da207 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0015 | 91841a7853b228c924b4ecdcf33a35b37990b10b | Add Bolas to SecTech vendor (#38902) | Port candidate | — | The retained SecTech inventory addition is absent downstream.
0016 | d071b4dab638472b5c6c06b126122fbb3bae1e03 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0017 | 97fcebb92b85c73b59fc806665c70512ebd976b2 | Kobold/monkey AI holograms (#38888) | Port candidate | — | The retained hologram content is absent downstream.
0018 | 0d9659e8101d8c6bf4b87723945bdea6ff3b67f7 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0019 | cce239dd93b31735707e40713cae038f9d34deb3 | Fix localization error when trying to change hair on someone with a hat (#38907) | Ported (CS-0082) | Interactions | All blocked mirror paths now supply the covered target's identity, including the retained target-final grammar correction.
0020 | a97223bc70471140a5507e2523a9b4e7ad0df291 | change Identity.Name to Identity.Entity for delivery popups (#38909) | Ported (CS-0083) | Interactions | Delivery open and unlock popups now pass the entity-aware recipient value used by Fluent selectors.
0021 | ecbff409b6ef7b54a0033afb816a5ba43b0862a4 | Replace `AdvertiseComponent` with `DatasetVocalizerComponent` (#38887) | Dependency-blocked/deferred | GameTicking | The old advertising system has more than fifty consumers and needs a coordinated ownership and prototype migration.
0022 | 615c4afbcc38fcf8c520adac82b97b271cde615b | Bagel genpop (#38829) | Dependency-blocked/deferred | — | The map change should be reconciled against the target-final Bagel snapshot.
0023 | 8026f7fd508b4241428d646bea9d1807adb3e70f | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0024 | 773299bd07278b05b6952063c3e4c38ddeb72966 | fix clones not getting the thieving skill (#38914) | Ported (CS-0084) | Interactions, GameTicking | Clone settings now copy the retained thieving capability into replacement bodies.
0025 | 685156c08f2d30e4aa4b67b698e56f79cbd19b66 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0026 | 9ad99cfa64e0b2803b9c3f00736a0bdd7d5d3848 | Make more objects spray paintable (Reviving #31328) (#37341) | Dependency-blocked/deferred | Interactions, Physics | This feature depends on the unported Paintable architecture, UI, prototypes, and assets.
0027 | 6633a18d6243600a3a616dff90ce932a22cbfe8e | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0028 | a64fe298d0550f1988c9b54b1254ac028e3ec031 | Add Serializable, NetSerializable attributes to DecalPaintMode (#38921) | Dependency-blocked/deferred | Interactions | The paint-mode serialization contract should move with its related spray-paint batch.
0029 | a268a4aaccc702a4e068d13a31f06eddaaa87741 | Rotated turret wall panel sprites (#38464) | Port candidate | — | The target-final turret sprite correction is absent downstream.
0030 | ac895a0db4f9478999940353f5359b976fc3e3f8 | Stun and Stamina Visuals (#37196) | Dependency-blocked/deferred | Medical, Movement | Missing visual components and animation fields make this a complete behavior and presentation feature.
0031 | 1117cac96d9b0fd194109207ba993570f21c7140 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0032 | a22826cd90ad797d17eeccc3ec331aa0d6a3d016 | Fix AddHandCommand not working on aghosts (#38866) | Dependency-blocked/deferred | Interactions | The admin-hand command fix needs review against RMC ghost and inventory behavior.
0033 | 05436d3dcc03e0638c8e36cf9142d62792d6a246 | Component for clothes to suppress emotes and scream action in general, and the muzzle to suppress vocal emotes in particular (#32588) | Dependency-blocked/deferred | Interactions | The component and prototype bundle overlaps RMC emote and action behavior.
0034 | c10c8eff4f9c051834c3348283ced6e4a86ff1e6 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0035 | 7845417d107036ea2be5483c359c79d6bc70cd1c | Tweaks to ShowRulesCommand structure, localization, and autocomplete. (#38855) | Port candidate | — | CMU still has the older unlocalized command without target autocomplete behavior.
0036 | 597e484f1c9cef63fd5b73e8968c5dc2a9f1305a | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0037 | 44b101977460b014f84cce62e26e275f865f2510 | Convert Locker/Closet fills to Entity Tables: Part 2 (#38254) | Dependency-blocked/deferred | Interactions | The broad storage-fill schema migration needs prototype-wide reconciliation.
0038 | adcdcb39dcc238405ee18d8f15910e4679e00265 | Fixing a singular pixel on the frame of the AI (#38936) | Port candidate | — | The retained one-pixel asset correction is absent downstream.
0039 | 88ebad06ea579880061d0b98f999380d54993bef | Bottle Drink Inhands (#38937) | Dependency-blocked/deferred | Interactions | More than two hundred assets and prototype edits form a large presentation bundle.
0040 | ad34d88a493d317abbb29f0802d5281d5dafdc68 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0041 | dbfe05d5cc90c825411fef9129e431733af35e2d | refactor: rework the new status effect system to use containers (#38915) | Dependency-blocked/deferred | Medical, GameTicking | CMU contains a partial older status architecture requiring target-final migration and metabolism review.
0042 | 5ba95f8c5bdaf573b2d08b7b77426dbd44c5f62f | Stable to master (#38944) | Non-code/no-op | — | Merge synchronization has no coherent standalone target-final behavior to port.
0043 | cfe825b0e3d4fea6d63251a22003820873cff343 | fix: don't divide by zero in FragmentIntoProjectiles (#38946) | AlreadyPresent (CS-0052) | Shooting, Physics | RMC already supplied a slightly more defensive total-count guard.
0044 | 367ff79006615494f501bde74abcb6f6e5c0057f | Accents Event to Shared (#38948) | Dependency-blocked/deferred | Interactions | The event ownership migration needs reconciliation with RMC speech systems.
0045 | cdf049038f4309b611ecc7504bf090dd66450d03 | Update Credits (#38955) | Non-code/no-op | — | Credits metadata only.
0046 | f3ce4281656b120fa2f4f7ee48ffaaf09e329e55 | SharedGunSystem spread bugfix (#38960) | AlreadyPresent (CS-0051) | Shooting | The exact maximum-angle comparison already arrived through RMC.
0047 | dd87e7ef644fa2e0ed3d2003151e1fbcaf0afcbb | Fixed error thrown when examining indestructible plastitanium windows (#38950) | Ported (CS-0061) | Medical, Interactions | Indestructible plastitanium prototypes now suppress damage examination cleanly.
0048 | 8b3232f305024876427c1c73ccbc4d14c8bdda07 | Fix extra dollar sign in admin log for machine toggle (#38961) | Ported (CS-0063) | Interactions | Machine-toggle admin logs no longer emit an extra dollar sign.
0049 | 27dc59a40bf7120f30487495a420d960a793298b | Don't compile EF Core designer files on release builds (#38927) | Dependency-blocked/deferred | — | Build-only conditional compilation was later reverted and is not a runtime port.
0050 | 3d9dab1d52b0211a7415d922d096b6801988e1e2 | Hats (and glasses) for pets - Part 1 - Ian and McGriff (#38634) | Dependency-blocked/deferred | Interactions | The pet inventory and asset feature needs a dedicated content reconciliation.
0051 | 545ca7136729f96bab81f7d8621d0bd48234a17d | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0052 | 31c84eaf20b3cea8af6b49336489a0c9e2b4ee27 | [BUGFIX] Stops scurrets from suffocating in crates (#38951) | Ported (CS-0064) | Medical, Physics | Scurret crates now preserve the intended breathable containment behavior.
0053 | acfb331cbe9e630e9c4f844ae9c803d70eae0de9 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0054 | 0c92478e0ae2ab20ce8f420a355fd1c17bc1da23 | Cleanup warnings: CS0649 (#38797) | AlreadyPresent | — | The unused CancelRegistrations field is already absent from CMU.
0055 | bf1b55e22f11cde064bba01dbad2ca159e62a824 | make ocarina small (#38971) | Ported (CS-0065) | Interactions | The ocarina now has the retained small-item size.
0056 | 259575ca768f364e9623fa9fd3668755cf14c043 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0057 | 7fd74b08df3f99fda5c0b184d07dd7b485a50d25 | Add contraband parent to war declarator (#38972) | Ported (CS-0066) | Gamerules, Interactions | The war declarator now inherits the retained contraband classification.
0058 | 106cbe0e196b7b994aea13b56de4dda92d5ca6fc | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0059 | 45fe7d5093636949b6d060b48d7850d0d00d8438 | Remove prototype caching from `ZombieComponent` (#38979) | Ported (CS-0067) | Medical, Gamerules | Zombie state stores prototype identity and resolves current prototype data instead of caching it.
0060 | d55a2b830a81edf135015fe6eafacc833ca5111a | Vox customization additions (+eyeshadows) (#38906) | Dependency-blocked/deferred | — | The broad species customization and asset bundle needs visual-content review.
0061 | bd2212beff3a4f57cdebf4d3ab4d6e8067a655fc | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0062 | 4f766f199c7db48908a1b7143980c744ef29bab7 | Refactor ExaminableDamage (#38978) | Dependency-blocked/deferred | Medical, Interactions | The broad examination API refactor needs reconciliation with RMC damage presentation.
0063 | 229f33f22a0c619a2a9aef41b45f06cc23367966 | Voltage enum to shared (#38964) | Dependency-blocked/deferred | Physics | Moving the power contract shared crosses the downstream power implementation boundary.
0064 | bd853b60de27889e56033370eb82bdba5266d7db | fix: ghosts shouldn't see whisper obfuscation (#38202) | Ported (CS-0068) | Interactions | Ghost listeners now receive unobfuscated whispers.
0065 | c60910dfa68bbed56a4cad4b0739b532f8930006 | Fix devices in terminal mispredicting power state (#38647) | Ported (CS-0060) | Physics, GameTicking | Terminal-contained devices now preserve their predicted powered state.
0066 | d9545dd3803333e2865a43b476466fb9eddc4a1c | make biogenerator not accept low-nutrient plants (#38427) | Ported (CS-0062) | Chemistry, Interactions | Biogenerators now reject produce with no positive nutrient yield.
0067 | 7c7aeffde28baf6cb857914690a6563d92ab1537 | Make RunVerbAs take and return EntityUids (#38155) | Ported | — | The isolated admin verb command now pipes entity IDs directly without unnecessary network-entity conversion.
0068 | f535a312974780f28e2f69314e81aaa870de0faf | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0069 | dac2537f9c872e177818a9e755d5fe8e091d497a | Buff parrot learn rates and radio chatter (#38984) | Port candidate | — | Parrot memory and radio probabilities retain the older balance values.
0070 | 2e6549a308f8838fd5fc41981970a806f1d3d9ad | Remove prototype caching from `TransformableContainerComponent` (#38988) | Ported (CS-0058) | Chemistry | Transformable containers now store reagent identity and resolve fresh RMC reagent snapshots.
0071 | 6053509cebc740a09188d9c18b67c4c2ac38b1ca | New holy books (#38986) | Dependency-blocked/deferred | — | This content and loadout bundle must be reconciled with the immediate Qur'an rollback in #39000.
0072 | 34cc49ccf01b06ca00edfb1d7ed8ef71a8c94dfa | Made the Mosin bayonet usable. (#38295) | Port candidate | Shooting, Medical, Interactions | The Mosin lacks the target melee injector, solution, visuals, and fill-state assets.
0073 | 3122adbd344ff26495869ad80e4ca17205d2f1c9 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0074 | f8322d548e6c4306911e4a5d3fcaef49418ae03e | Adjust throwables hitboxes to match sprites (#38985) | Port candidate | Physics, Shooting | Most affected throwable prototypes retain fixtures that do not match their sprites.
0075 | 5dbef8a924854440ccd352da491d4576be2ed2cc | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0076 | b18a7ccdd3e3a4cafc165cdac249dd4a5bb09a61 | Moproaches (#38700) | Port candidate | Gamerules, GameTicking | The mob, crafting, event, localization, and asset feature remains absent.
0077 | b5ad346fd87b929bb5bc66d9d2d64afda664b487 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0078 | 1d4c94556143cc60d70de2c41ef8e48250b9d1a7 | Remove the Qur'an (#39000) | Non-code/no-op | — | Runtime prototypes and loadouts are already absent, leaving only unreferenced texture cleanup.
0079 | bdf3c891e78193e7217416d3d4e0799cb5667c9a | Mostly fix reaction sound effect stacking :( (#38999) | Ported (CS-0057) | Chemistry | Reaction audio is now server-authoritative while reaction effects remain shared.
0080 | 604435d807301f5c219457e704ed6300217056a9 | Convert some voice samples to mono (#39002) | Port candidate | — | Fifteen voice assets retain their older channel encoding.
0081 | 377c9bfceafe938059faf80a2ec63f11805a2def | PressureEvent removed (dead code) (#39004) | Non-code/no-op | Physics | The unused event remains but has no subscribers or runtime behavior.
0082 | b9ffd060d6a4f2b09f99afec1b16170f785952b4 | Predict DevourSystem. (#38970) | Dependency-blocked/deferred | Interactions, Medical, Chemistry | This shared prediction migration overlaps RMC body, stomach, and reagent APIs.
0083 | 4e59b617490e0709bb6ca496c4eef57bd40b3fb8 | Whitelist extension for tool belt (#35212) | Ported (CS-0059) | Interactions | Remote signallers now carry their semantic tag and fit the utility belt whitelist.
0084 | 988a35bc5abc7c65ec816c6bd36ff98104736dff | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0085 | 76a7b31c1e59a11b5079d20a3e2feb9c0a7836dd | Fix firelocks failing to drop fast enough (#38918) | Ported (CS-0054) | Physics, GameTicking | Emergency firelocks now refresh airtight data before the current Monstermos flood-fill continues.
0086 | f6c8bb9b1626315a51059d713a072617da0b669a | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0087 | 975ebac202deb696d1cfb9e6903e1eed62485786 | Fix Mjollnir throw while on delay (#39018) | Ported (CS-0085) | Physics, Interactions | Per-throw hit and cooldown state now prevents Mjollnir knockback while its use delay is active.
0088 | faa8152bf692f98935ec804d892a2e50580a16cc | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0089 | 2a496bf93f56b7fb3b765840ad5bc1112f9e2843 | Give admin bags explosion resistance (#38384) | Ported (CS-0086) | Physics | Admin ghosts and their dedicated holding satchels now ignore explosion damage.
0090 | 86093a548cc97363aaa68999c6305f4cbb08c08a | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0091 | a921594f1ecfadbfbd36607e50f94df2c73d7d9c | Inconsistent Produce Inhands Fix (#38860) | Port candidate | — | Almost the entire produce in-hand asset correction remains absent.
0092 | 43d04a44feb4ffc462e3f8dd5f2c9f64d3f54fd8 | Re-id 'Medical Doctor' guide entry to 'MedicalDoctor' (#39029) | Already present/equivalent | Medical | The guide entry and all current consumers already use MedicalDoctor.
0093 | f7c64ab86c35fbd23dc05ac26002678e45b00a21 | Make diagonal windows prevent electrocution (#39032) | Ported (CS-0044) | Physics, Medical | Base diagonal windows now retain Window identity for electrocution obstruction checks.
0094 | 8c6a43fe72b35ef1a170ce81f405d77680fd4c40 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0095 | 93e04de36bc965b51bc086cd88fb5a4b332c2782 | Carps Can No Longer Suicide (#39033) | Ported (CS-0069) | Medical, Interactions | Carp suicide now terminates normally instead of entering a permanent critical loop.
0096 | 89fa7c2914962af6ca3a43194d9d3f46a25c6b09 | Wearable banana peels (#38868) | Port candidate | Physics, Interactions | Wearable and chameleon peel behavior plus matching assets remain mostly absent.
0097 | affcc2278481572f012407c278c055077f02bff0 | NPC spiders sometimes spin webs 🕷️🕸️ (#38319) | Port candidate | GameTicking, Interactions | Spider AI lacks the timed web-spawning component and behavior.
0098 | 2715933a460414e910594dd9eb485451ea0caa14 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0099 | 535646aefbfe4ed194b60a0877ca939e1e826424 | Fix Respirator Asserts (#38911) | Dependency-blocked/deferred | Medical, Chemistry, Physics | The respiratory event and buffer refactor overlaps RMC atmos and reagent divergence.
0100 | 17559db1c7e172d4e89f283b90c2ee32bda1ec54 | Add supercritical sounds for ALL anomalies (#36425) | Port candidate | GameTicking, Interactions | The component, system, prototype wiring, and audio assets are almost entirely absent.
0101 | 00ce19dfb193b488ef4fb5d0b5808d4653e086a4 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0102 | e87fc850dcecb94f0c9c61923544e1b095d5a42d | Trim vending machines emag inventories (#36839) | Port candidate | Interactions | Twenty-two inherited vending inventories retain older emag stock.
0103 | 38eb07a2ceff31afd1b246d10829c70cc39ad90c | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0104 | c884ecd074e65fe16a1d01fc6b2bdacea1a4f6a4 | Add acolyte armor to chaplain uplink (#36843) | Port candidate | Interactions | Standard chaplain uplink content lacks the acolyte armor bundle.
0105 | 0ba14af9704621fe23d30eb25dc8c5781cc1cd9e | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0106 | ada0e4cb6f97fe54bf17cd94092f2f0c84f33086 | Marble tiles (#38007) | Port candidate | Physics, Interactions | All tile, material, recipe, localization, and asset additions remain absent.
0107 | a093a2dd289c8edeb973f6aca8a4bcc4321efa48 | Fix overlapping popups for entity storages you can't access (#39039) | Ported (CS-0053) | Interactions | Failed click-driven lock attempts now consume activation before storage or UI handlers run.
0108 | 1fc0040d4eefe3721b0d48157aebfa7370698f06 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0109 | c5bd8defb36b42349bbb5b6a987b4ed11c212045 | Reword thief-role-greeting-equipment to mention innate ability rather than gloves (#39045) | Non-code/no-op | — | This changes localization wording without altering behavior.
0110 | 63e22feb727ceda72cbf268bf2256f5c0d001a74 | Adds Estoc DMR magazines to the syndicate ammo bundle (#38413) | Port candidate | Shooting | The standard syndicate ammunition bundle lacks Estoc magazine entries.
0111 | 68831c18a3ce40870c88d312e39b2d10d11e9090 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0112 | 1bc1d71d4253c93950736c2d18cda8a4d2d4f9b3 | Allow GenPop access perms on the AccessConfigurator (#39043) | Ported (CS-0070) | Interactions | Access configurators now expose the inherited General Population access level.
0113 | d8881ad4c6f2c880fd4234546c447ec8dc781b9a | Fix bar mailing unit tag on plasma (#38098) | Ported (CS-0087) | Interactions | Plasma's bar mailing unit now advertises and persists the `Bar` routing tag.
0114 | 04e44aaa70a579d51d7170d976a9103fc19ce2c4 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0115 | 4dd9f06fefd5c705263f38277324dfed23cbbe7c | Remove omnizine from unwarmed honk pockets, honk pockets make you honk (#38152) | Port candidate | Chemistry, Interactions | Honk pockets retain the old reagent contents and lack honk behavior.
0116 | 4e99f0552249b535b01075c12a807b3c3b2d1fee | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0117 | 08a1d573317d7c06fe78e81f363e7224fc0a5d06 | Add empty line between changelogs discord entries (#38170) | Non-code/no-op | — | This only adjusts changelog-bot output formatting.
0118 | d0b798b63fc58138043007f2635b8ac99b80391e | Fix communications console thinking it can announce in the first 5 seconds after spawning it (#38305) | Ported (CS-0056) | GameTicking, Interactions | Communications consoles now publish their initial UI state after applying the announcement cooldown.
0119 | 8673498aef1e6ea7ab87b23bacccee3db88e824f | nerf cheese prices, part 3: misc, last one (#38247) | Port candidate | — | Three inherited item prices retain their pre-fix economy values.
0120 | 157e4efb3538abd9248c9598b607f62d554153ac | Tighten DB shotgun spread, widened sawn off spread (#37731) | Port candidate | Shooting | Standard double-barrel and sawn-off shotguns retain older spread values.
0121 | 006f3abfd70d0595fec48ce304702a46c4f47191 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0122 | 2ce03ad65154e6ca81918114a30385c941f8c136 | Fix the max scanner distances (#39041) | Port candidate | Physics, Interactions | Shuttle scanner range is calculated from display-transformed coordinates instead of grid-local distance.
0123 | e7b68d9722320bc2081ba7dd1feeb03bba69ab49 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0124 | ffbc813179291286dda2dcfdfd58648f909ab1c2 | make name identifier prefix LocId (#39035) | Port candidate | Interactions | Name-identifier groups still store raw prefix strings rather than localizable IDs.
0125 | 2c4251dcdcfbeb41114096547d2dc1e5cd592361 | Revert "Don't compile EF Core designer files on release builds" (#39057) | Already present/equivalent | — | CMU already compiles migration designer methods without EF_DESIGNER conditionals.
0126 | 5ac78ec3148d09b53929361ad03ef8a6620c1ebe | Guidebook changes (#38987) | Non-code/no-op | — | This only changes guide text and layout.
0127 | ee69f4e5b4b21cbb80385f345dd10493f3aae959 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0128 | cd0960fbd760a9eebf51474616437ab7eee73cc6 | Golden plunger Trolley and Bucket Carp (#38494) | Port candidate | Physics, Interactions | The janicart slot, golden plunger, bucket-carp variants, and assets remain absent.
0129 | 89cc8419b12056131c48f86495ee70805f280ed8 | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0130 | 23c190e9bcce030dddeabb04c5d96aeeca588de0 | Dehardcoded Air Alarm's UI window title (#39072) | Port candidate | Interactions | The Air Alarm XAML still embeds its English title instead of resolving localization.
0131 | 2db1ab58e6dc382c536dea7db1238be4b453dcc9 | Fix Bagel Salvage's airlock not being an airlock (#38956) | Port candidate | Physics, Interactions | Bagel's salvage entrance retains the incorrect map entity.
0132 | 1fd202fceaacd078b38a10c1fafaa487b670b98d | Automatic changelog update | Non-code/no-op | — | Generated changelog only.
0133 | fb69a0ae2c922ca1b9d9cfaa584785a390699460 | More atmos devices can be placed on layers easier. (#38672) | Dependency-blocked/deferred | Physics, Interactions | The large construction-graph rewrite must be reconciled with CMU's older construction schema.
0134 | f21803bfd81e6435a2e8a765e1ddf74f7129a71e | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0135 | 41a175636b115b79b551d142c5c97d7d7571fde9 | Fix bug with pipe color (#30645) | Superseded | Physics, Interactions | Target later reverted this in #39135; target-final and CMU both retain the server-owned pipe-color implementation.
0136 | 50302a531bc5d1505d934947fd2ccfa9ac3db9ec | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0137 | cfb0a950359662489cc36115c3de8bad741649f4 | Fix mouse rotator error spam (#39071) | Ported (CS-0072) | Movement, Interactions | Mouse-rotation requests now carry and validate their originating user before changing rotation state.
0138 | dec2d42a1d1b87924c312dce94ba9c60411ab54d | Crawling Part 1: The Knockdownening (#36881) | Deferred | Movement, Shooting, Medical, Interactions, Physics, GameTicking | This 60-file standing/knockdown architecture is absent and conflicts with RMC standing, tackle, cuffs, and legacy status-effect consumers.
0139 | 98ec45d914c801028193fb287794f0793d92d529 | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0140 | 1a66398029de68814be28dfd9b4d2a613ace913e | Update Credits (#39081) | Irrelevant | — | Credits metadata only.
0141 | 6eadc8aeee013c4b618c495059b9aeea8aa1b62e | Stray Pixels on Vox Tails (#39082) | Deferred | — | One changed target asset, tail_big.png, does not exist in CMU's older Vox RSI tree, so this needs a target-final visual asset batch.
0142 | 8e5d70716dfc70d6c327a0fa61da3728385d9a49 | Stable to master (#39095) | Superseded | Gamerules | This merge synchronizes Wizard's Den rules and removes two AI holograms already absent from both CMU and target-final; it is not a coherent standalone port.
0143 | 391dfe4f4aa4be1e4f1e3eab97349949b85f7ebe | Crawling Fixes 2: Salvage Nerf (NPCs can shoot downed targets) (#39085) | AlreadyPresent | Shooting | CMU sets gun.Target = comp.Target before NPC AttemptShoot, providing the same downed-target semantics through an RMC adaptation.
0144 | 267d92a1ea8691f67b4d1e8f99c4122da4ce33fc | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0145 | 99b431cafd3312ff5bdfeb0ecc618b12b5718405 | Crawling Fix 3: OOPS!!! (#39089) | Deferred | Movement, Medical, Interactions, GameTicking | The TryStanding/knockdown-do-after fix requires the unported crawling API introduced at 0138.
0146 | d2ddbcbcda1018725ff3a0698baff38c3df22779 | Implement SmartFridge functionality (#38648) | Deferred | Chemistry, Medical, Interactions | RMC has a parallel RMCSmartFridge, but inherited standard maps still use the separate pre-feature SmartFridge, requiring coexistence and storage-interaction review.
0147 | a69fe53bee5cd4492a1103773847f93fca5b495a | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0148 | 8f7e6096f2de6bb2b21271a71116c668ee4bcadf | Crawling Fixes Part 5: Holy Fuck (#39109) | Deferred | Movement, Medical, Interactions | Its cuff speed/stand-up and knocked-hand cleanup depends on the unported crawling model.
0149 | 2b2b9b11b8801be88a4b9c04ad9e7913c8f6a195 | Fix #38935: Remove empty EnsnaringComponent.cs file (#39112) | Ported | — | The obsolete empty server component file has been removed.
0150 | e85bc1bb8c58152f209c4fda9620744bff2096b2 | Stunnable New Status and Cleanup (#38618) | Deferred | Movement, Medical, Interactions, Physics, GameTicking, Gamerules | This 56-file status migration collides with dozens of RMC legacy status and KnockedDown consumers.
0151 | ed6ed6c5f3c0e96eff44c583c8fa9e151df9f9f0 | P0 BUGFIX: Master doesn't build because of uncaught git merge conflict. (#39116) | Deferred | Movement, Medical | The one-line TryStanding API correction only applies after the crawling/status migration.
0152 | 378fbb0ba91355417750d31ae094b5622649de97 | move parrot name to MobParrotBase (#39131) | Ported (CS-0077) | Interactions | The parrot name now lives on the base prototype so derived parrots inherit it.
0153 | 65b4b41928adca08247227844d376567c13374d6 | Fix RoundEndTest obsolete warnings (#39133) | Ported (CS-0080) | GameTicking, Gamerules | Round-end integration coverage now uses deterministic tick-bounded event waiting.
0154 | 7109c330545676277b2c83a62ee0001b677977ba | China lake rebalance (#39106) | Deferred | Shooting, Physics | Target retains the 30-pellet cluster and reduced non-vacuum explosion, but this is an explicit combat balance change needing fork sign-off.
0155 | 4033089c4685482b6aeb9c6323ba469e66326b70 | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0156 | f16175a6e314e0f8534cda18d4a6eda234468e79 | fix: dirty SSD indicator comp on mapinit (#38891) | Ported (CS-0075) | Medical, GameTicking | SSD indicator deadlines now initialize, replicate, pause, and update on the retained cadence.
0157 | 1ee7dffe6d15289db085def9000f16c8eb0d7c3a | fix: fix non-access checking EntityTargetActions (#38731) | Deferred | Interactions, Physics | CMU has 36 RMC actions using checkCanAccess:false plus lag-compensation and storage exceptions, requiring a full 41-action validation matrix.
0158 | 54b09c711639dae404b333344a4092b63cd9e941 | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0159 | d8c92a790252c97bf37e0635b11fbac3f48c2650 | Add Polly to Bagel station (#39147) | Deferred | — | Target retains Polly, but Bagel has later map revisions; apply against the target-final map rather than this intermediate save.
0160 | 295bb9ebdf7d90dd2921b46e57078972eaf0ae3c | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0161 | ce2ce7d34549d9108914176e6e83f2f180e8be53 | Add Polly to Oasis station (#39146) | Deferred | — | Target retains Polly amid extensive later map changes; import a reviewed target-final map delta.
0162 | dabe993f449674490209a264bc50bb85bd6e91e2 | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0163 | 4932b574b7bd2d25e3d8785dc410bcdba59b745b | Add Polly to Exo station (#39145) | Deferred | — | Target retains Polly, but the map has later revisions requiring target-final reconciliation.
0164 | 646c631440fa8a64c05b85542b00ba89451f52f1 | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0165 | 6fc9ad6cecb41173a119dabf6f8555df627e48bf | Add Polly to Fland station (#39144) | Deferred | — | Target retains Polly alongside unrelated saved-map changes; use a target-final map batch.
0166 | 3ef14be8bdc845c8ae79a4b87884edefcd88cf27 | Add Polly to Packed station (#39143) | Deferred | — | CMU still has a normal parrot and target-final has Polly, but later map revisions make direct commit application unsafe.
0167 | ba1f1bf4acd7b2eb5bc61e7c902412f5c9a3c9e7 | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0168 | 6e26511c1e21773d347ac587158ecfce957aaa29 | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0169 | 0faa9662793bb5948e56968ce03ccaf34f115bfb | Add polly to Marathon station (#39141) | Deferred | — | CMU retains a normal parrot while target-final retains Polly; reconcile against the later map snapshot.
0170 | 8591532f9c731f7afb1e98e278813acfcdf95099 | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0171 | 8e749462cb24fc6c4345a685e23ca0be03310e23 | Add polly to Box station (#39140) | Deferred | — | Target retains Polly, but this save also moves stock parts, clothing, and turrets and is superseded by later map revisions.
0172 | 61b0559f9cb9833baa9928399fccbdf44321cdbc | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0173 | 035811ae27c9d1c8380b441129daa1b1348fb21a | Add Polly to Amber station (#39139) | Superseded | — | Amber station no longer exists in the pinned target, so its Polly edit has no target-final destination.
0174 | d6a1486f480e9862fa93d9e261288a1dbfc9b11f | Minor plasma fixes, add Polly to Plasma station (#39138) | Deferred | Physics, Interactions | Target retains Polly and the map, but the commit contains unrelated pipe, container, turret, and UID churn requiring target-final map reconciliation.
0175 | 94abb90ba418852d06768dbdb6cbb6c8570c8695 | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0176 | 728a9a552a791375b424162b95a83529e63a047c | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0177 | b1c8f02b6b03c180f428e74bf012bff43805282a | Add Polly to Elkridge station (#39150) | Deferred | — | CMU retains a normal parrot and target-final retains Polly, but later map revisions should be reconciled first.
0178 | ca697fe200e3ae807aeb0056eded91d07c3bb61b | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0179 | c3ff6c9184889bf009a27e736cd4639a8d05ef93 | Increase SpawnAndDeleteAllEntitiesOnDifferentMaps test simulation time (#38901) | Ported (CS-0081) | GameTicking | The entity-map lifecycle test now simulates 450 ticks to exercise delayed update loops.
0180 | 0ab0dadb1d4fc4034d3de04f9ab471fdc653b2f7 | Tweaks nukeop elimination announcement to be less wordy. (#39158) | Ported (CS-0073) | Gamerules | Nukeop elimination announcements now use the retained concise wording.
0181 | b5dbedcc48af22444856ddbbb82f820fa7de92c6 | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0182 | eb21b5826abbc4d5cc06e3cf845bc7ef7c8eb7c7 | RT v265.0.0 update (#39132) | Deferred | Shooting, Interactions, Physics, GameTicking | The engine pointer is superseded, but CMU still retains pre-v265 manual emit-sound state handlers and related content-side API debt.
0183 | 83b3e9e15ab10b23dbcae350e62f2aeaa998e76e | Localize makesentient command. Move makesentient method to mind system. (#38565) | Deferred | Movement, Interactions, Gamerules | CMU retains the static command helper across many call sites and needs a coordinated mind-system migration with RMC consumers.
0184 | 24b75d89a501eda4462537c6fbc33c5bcc92c168 | Show customvote title in chat on finish (#39137) | Ported (CS-0074) | Gamerules | Vote result announcements now include the custom vote title for wins and ties.
0185 | c95bbfaf936abfd568e08d992179a1028aa39539 | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0186 | 002afe8056cb3e6e554bec98343506a13eaeacf6 | Properly dispose of stale pipedata chunks for the atmos monitor (#38974) | Ported (CS-0076) | Physics, GameTicking | Atmos monitors now discard stale pipe-net chunks and carry typed pipe colors through state.
0187 | 66bd5be65102eda2d9e79704c0e206663488d5c5 | Standardize and ngooden MIDI music via a good default soundfont (#39142) | Deferred | — | The retained change adds a 27 MB licensed binary asset and needs repository-size and audio-policy review.
0188 | 1b43f6efd41188c88a4744c87024943224b811c2 | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0189 | de576a429d9461badb3e8a2a01721f62e0ed4b2d | Fix some debugging permissions (#39167) | Ported (CS-0078) | Movement, Physics | Debug velocity and rotation permissions now match the retained command set.
0190 | de22bd82bb9f24d2651d733f0bd72bc62e6c94e3 | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0191 | 291f919e8e4b95f25722ccfee55f3d286f67edcd | Crawling Bugfix: Don't drop items when falling. (#39168) | Deferred | Movement, Medical, Interactions | The refresh-argument bug cannot occur with CMU's old overload and must be included with the crawling/status migration.
0192 | 70ad8efac377bc1e36a5c2ce6c62c6180655569b | Emissive engineering & chief engineer's hardsuit helmets (#39153) | Deferred | — | This retained 31-file prototype/asset bundle requires visual validation as a dedicated content port.
0193 | f6386b3a57a87caa90530d68831ea08626914e08 | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0194 | 4739ce71c5f4c29b83e69cf258223b996f5afe93 | Makes cold slowdown less punishing (#36316) | Deferred | Movement, Medical | Target retains 0.9/0.8/0.7 multipliers, but this is a standard-species balance decision; RMC species independently override TemperatureSpeed.
0195 | 3b5faac0505aff878419b739cb53ea4b96e431d7 | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0196 | 01a57c9a1715bd3349fd981a58b01bb6edb5bb04 | Add name to AI eye (#39177) | Ported (CS-0079) | Interactions | Remote AI eyes now identify their owning AI in the entity name.
0197 | 82c0f63d50d30ce62da96b25199c93491e531b41 | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
0198 | b0e1ce7c0c4ef4bade1791107fb5fabe43578e72 | feat: add a component for rejuvenateable status effects (#39025) | Deferred | Movement, Medical, Chemistry, GameTicking | Target retains and expands this model, but CMU's legacy/new event sharing and three RMC xeno immunity subscribers require a full status audit.
0199 | a36c984ba63d56bba0d9ea354d4c8d01f7b975a8 | Automatic changelog update | Irrelevant | — | Generated Changelog.yml metadata only.
~~~

# SS14 upstream inventory: wave 0007

Audit date: 2026-07-20

- Pinned baseline: `59633f6dc50e77dda8cefa344d87c7b01e06a810`
- Pinned target: `40ca2c7f90d11d27be5457d177c133f0947d1c08`
- Range: ordered first-parent commits, zero-based indices 1200 through 1399
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
1200 | 6a3a54535b9e5bd42f3778daa6045a0ff11a52e5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1201 | c54fb47f7c7aacd1132d6634576f1b7d81fc2123 | Wrapped Parcels can be labelled with Papers (#40783) | PortCandidate | Interactions | The parcel label components and sprites are self-contained, but should be integrated with the target-final parcel-wrap chain and RMC container behavior.
1202 | 61f7d3438d884cedcc83818f48a2b3bc9234d6ad | Automatic changelog update | Irrelevant | — | Generated changelog only.
1203 | 72852accbe729a45517c57975265ab10ec14d6f3 | Remove enforcer from fland (#40786) | Deferred | Gamerules | The generated Fland map edit needs target-final map reconciliation rather than replaying an intermediate binary map delta.
1204 | ef0c9ecc6350d4d6bcf9c8ad9013724688dbcae3 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1205 | 8e3243a15648077aad082d5fc299e71d5267defe | Fix changeling blindness (#40517) | Ported (CS-0191) | Medical, Gamerules | CS-0191 clears accumulated eye damage when PermanentBlindnessComponent is removed, preventing blindness from persisting after a changeling form change.
1206 | 6f3f7d86b807780318d18b5686afc32a657ccdbc | Fix terminology (#40792) | PortCandidate | Gamerules | The uplink terminology correction is isolated localization, subject to CMU catalog wording.
1207 | bb3fa43f1f6ff133501bb298b4bb95407427159f | Predict LungSystem (#40729) | Deferred | Medical, Physics, GameTicking | Predicting LungSystem crosses shared body state, gas updates, respirator behavior, and integration contracts that diverge in RMC.
1208 | e3f6b00362baac79679294bc73a5acce94e70fa9 | Stable to master merge (#40798) | Irrelevant | Gamerules | The effective first-parent delta is only a Space Law policy-text edit, which is upstream-project-specific rather than gameplay parity.
1209 | fea8ac45228f18ee7ad213560044a444a2c981e8 | Change whitelist logic for parcel wrap (#40800) | Deferred | Interactions | The parcel whitelist rewrite changes wrapping eligibility and should be reconciled with the later humanoid wrapping and damage fixes as one target-final chain.
1210 | dac2c5212ae1c5f230f04dc31e7ade293dc8dcae | Add generic event listener for integration tests (#40367) | Irrelevant | — | This introduces integration-test listener infrastructure only and has no standalone runtime behavior.
1211 | 33c0c46b5da5a462d05e267a887b8254e95096ce | Add slowdown to nocturine, buff duration and minor delay reduction (#40797) | Deferred | Movement, Medical, Chemistry, GameTicking | The nocturine balance change relies on reagent and status-effect movement APIs that diverge in RMC and are refactored later in the target.
1212 | cc4c7f5adacf7d3ba268f1cba40a5110661312d7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1213 | 3ac94ea7f572b0ce9fbd3ee5cd0937783fd4572b | PRevent the forever sleep. (#40366) | Deferred | Medical, Interactions, GameTicking | The sleep lifecycle rewrite moves wake cleanup to component removal and rejuvenation, requiring reconciliation with RMC status effects and mob-state handling.
1214 | 8dd8a334ae50f3eaab0640201df09f99fbc1a414 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1215 | fda846ac85891752ba51df07ca67975165fb5257 | Move Bulldog Drum to Emag (#40790) | PortCandidate | Shooting, Gamerules | The lathe-pack move is isolated data but changes security and emag ammunition availability.
1216 | dc127d08575784bff17aec6e47bd0a61598c9096 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1217 | e89651c77481eb04b02a95e11260672062d02d12 | Fix masks with flash, eye, and damage protection working while being pulled down (#40331) | Deferred | Medical, Shooting, Interactions | Mask-down protection changes span armor, eye protection, flashes, and clothing toggle state and should land with the follow-up mask visual fix.
1218 | 5185df4072de98ff69d6690206297ccf12786e19 | Add missing uranium glass locales (#40809) | PortCandidate | — | The missing uranium-glass localization entries are isolated resource data.
1219 | 0d97699aaeec677736382b8a225414b193a9bf8a | Migrate revenant and PAI shops to use ActionGrant instead of hardcoding them (#40475) | Deferred | Interactions, Gamerules | Migrating PAI and revenant shops to ActionGrant changes action, store, role, client, and server contracts that diverge in RMC.
1220 | d3d52615d4b0aebf6218cbf881101d0255427eca | Lootpool Tweaks Mail/Maints (#39892) | PortCandidate | Gamerules | The small maintenance and mail loot-table additions are isolated, subject to CMU loot balance.
1221 | accb59b6c720e389bdc31cdf34cf6fbe4997719c | Added more nitrogen canisters to plasma (#40794) | Deferred | Physics | The large generated Plasma map rewrite needs target-final atmos and map reconciliation.
1222 | d4814df43c8f7280b79e32b3dcec21b059b855bb | Automatic changelog update | Irrelevant | — | Generated changelog only.
1223 | 6b4c10264e776e07615ddc26dbffb8c9ec096110 | Fix Officer's Handgun Objective (#40811) | PortCandidate | Shooting, Gamerules | The thief-objective prototype removal is isolated but changes objective and security-weapon policy.
1224 | 566cb710b62a85dc85300b8b78b8fea4d9538b2d | Automatic changelog update | Irrelevant | — | Generated changelog only.
1225 | 9964fe9a6bf83918cdde882b2860ce84cc73c467 | Replaces D&D5e-based paladin lawset with PF2e-based laws (#40343) | Deferred | Gamerules | The paladin law replacement is upstream policy content and must be adapted to CMU's retained silicon lawsets.
1226 | 1fca29a1675629c5910ba53ea91e07d1a16d98a4 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1227 | 766c2b875948851c1944fd22275b267d0b1131d0 | fix singulo generator triggering failsafe when field is obstructed (#39593) | Ported (CS-0192) | Physics, GameTicking | CS-0192 keeps scanning past unrelated ray hits until a containment field is found, preventing false singularity-generator failsafes.
1228 | 3503cb52d28eb44a9c3a1a18b13d1a82e8110d66 | Refactor Crayons to use shared charges system and autonetworking. Adds auto recharging crayon. (#40575) | Deferred | Interactions, GameTicking | The crayon rewrite migrates client/server state to shared charges and automatic recharge, requiring reconciliation with RMC drawing and charge consumers.
1229 | cec2fc7021b8e744ab61ef8c304156818d8174a0 | Packed: Replace duplicate security camera router with sci router (#40819) | Deferred | Interactions | The generated Packed camera-router edit needs target-final map and device-network reconciliation.
1230 | 2ecfb9552a2f16016845845cc8c4bc9f3e019fdc | Add variables to CluwneComponent, allowing for admeme customizing. Also localized two strings. (#40466) | Deferred | Medical, Interactions, Gamerules | The Cluwne customization feature changes component data, outfit replacement, localization, and antagonist transformation behavior.
1231 | a058921b4db31fe7de6140e095362edbd7ed7312 | Packed: Fix brig Megaseed vending machine is locked (#40821) | Deferred | Interactions | The generated Packed vending-machine access correction should be applied through target-final map reconciliation.
1232 | a9dcfcb9fb5d5b1667c5f10a03b53cd0e13f9d3e | Automatic changelog update | Irrelevant | — | Generated changelog only.
1233 | 74ebe585fa4ab51bca3ea5fb28495e1055030fd9 | Packed: Add missing station beacons (#40817) | Deferred | Interactions | The generated Packed beacon additions need target-final map and station-location reconciliation.
1234 | a384640eea9ecc160f8700b896b03739c9070712 | Ninja Bomb Blacklisting: Map Updates (#40727) | Deferred | Interactions, Gamerules | These generated map removals are one half of the Ninja bombing blacklist and must be reconciled with the code and prototype half at index 1236.
1235 | 52139b5cc5504e2e96f239993f2dd425094ded21 | Fix glassbox prototypes (#40667) | PortCandidate | Interactions | The glass-box prototype cleanup is localized, subject to CMU storage inheritance and existing map references.
1236 | ecc0aaaa9fe22d8e1cf9d38748c8f8373540f1bc | Ninja Bomb Blacklisting (#40726) | Deferred | Interactions, Gamerules | Ninja bombing blacklisting changes objective conditions, markers, beacons, tags, migrations, and maps across divergent antagonist systems.
1237 | e35624d1f1a60849776fb2b8be126dbabcc74ecc | Automatic changelog update | Irrelevant | — | Generated changelog only.
1238 | 9ae4068432a432e34a23edc58a68a3dceaee30e2 | add event to dna scrambling (#39862) | Deferred | Interactions, GameTicking | The DNA-scramble event addition depends on the target trigger framework and should land with that broader trigger migration.
1239 | dca80238f0df3b7fb7e50870e84505ff0e1d8989 | Attempt to fix all unlocalized lines (#40284) | Deferred | Interactions | The 29-file localization sweep changes many client and admin interfaces and needs CMU UI-by-UI reconciliation.
1240 | b5c8ed8356443f0d3065d3d40a184cc300df796d | fixed medical cyborgs not getting movement sprites (#39747) | Deferred | Movement, Medical, Interactions | The medical-borg movement-sprite fix changes shared/client borg switching code that diverges heavily in RMC.
1241 | 21460c86b08d0234aacf176d21b2147b85392755 | Mindrole trigger condition (#40323) | Deferred | Interactions, GameTicking, Gamerules | The mind-role trigger condition changes shared role lookup and trigger-condition APIs and should land with the target trigger framework.
1242 | 6f02e6c19c44de1ffe0f2255c67df74924e35cdf | Decouple power sink from tickrate (#40789) | Deferred | Physics, GameTicking | The power-sink fix combines tick-rate-independent charge integration, a battery clamp correction, and major balance-value changes that require RMC power review.
1243 | 8572fdf3ca8df94e853a8e4a2226db07dd04f4cf | Fix Error Logged in Graffana for SharedStaminaSystem (#40764) | Deferred | Movement, Medical, Interactions | The elemental inheritance rewrite removes duplicated mob components and must be checked against RMC mob, stamina, and movement inheritance.
1244 | 648ae755623fbe12caf880845fff72552b7a6586 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1245 | a6dce11566a98819659c6cceefd63e99e4938473 | Predict damage examine (#40168) | Deferred | Medical, Shooting, Interactions, GameTicking | Predicted damage examine moves damage-on-hit and weapon paths into shared code and crosses foundational RMC combat APIs.
1246 | ca5053fe7b6842ea8b268e3b4789aa448f5df746 | Predict artifact crushers (#40180) | Deferred | Interactions, GameTicking | Predicting artifact crushers changes server/shared ownership, component state, and action timing around RMC xenoarchaeology.
1247 | 3df66219d6d3be46e1a167ab18dcd3d47f440637 | Remove holopad projection verb on station AI core (#39937) | Ported (CS-0196) | Interactions | CS-0196 suppresses the impossible self-projection verb on Station AI cores while retaining normal remote-holopad projection.
1248 | 871d26221486a2e9d0417b4d7956bc4de70d0998 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1249 | af05313f37e45103fcaa51f21e654f9a076a4819 | fix NetEntity datafield in JointVisualsComponent (#39987) | Ported (CS-0022) | Physics, Shooting | CS-0022 keeps JointVisuals targets as local EntityUid values while generated state handles network-boundary conversion.
1250 | 04cde9a0ac2757fd55694402d34e026728726206 | Ice (the reagent) now actually does stuff (#40149) | Deferred | Medical, Chemistry | The ice reagent behavior depends on the target entity-effect pipeline introduced later at index 1303.
1251 | 1b1704af0be15ac8360611344ec220a664a2af1f | Automatic changelog update | Irrelevant | — | Generated changelog only.
1252 | bed5e8fd7a675697b10c9d22d65f9fd5d331e946 | Very small Shared Storage Optimization (#39092) | PortCandidate | Interactions, GameTicking | The storage delay calculation preserves behavior while reducing repeated multiplication and is a small shared-system cleanup to adapt.
1253 | bed556051b45827d8cebc49edaf8cb2f2ee67a21 | Fix NetEntity DataField in AnalysisConsoleComponent (#39984) | Deferred | Interactions, GameTicking | The analysis-console NetEntity fix changes generated component-state ownership and device-link APIs around RMC xenoarchaeology.
1254 | 1e911d175059fc0dbbdcd09f8fe904ab5a93a3f2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1255 | 228ed0288c25337697e12d3e25c57e7103037083 | make nobody supervisor a locale key and cleanup JobPrototype (#39102) | Deferred | Gamerules | The JobPrototype cleanup and supervisor localization alter a shared role prototype used by CMU's divergent job set.
1256 | 5b38148651ec2f21fbab27a5b7f01e7e297139ea | Hop console grant all and revoke all access buttons (#39375) | PortCandidate | Interactions, Gamerules | The grant-all and revoke-all HOP console controls are client-only, subject to CMU access policy and retained console layout.
1257 | ad708eec3b0d8795cf305b1ec21403d2e955456e | Automatic changelog update | Irrelevant | — | Generated changelog only.
1258 | d3f85701f74ae8c54e830656f0ed39331c5f9915 | Adds HugBot (#37557) | Deferred | Medical, Interactions, GameTicking | HugBot is a large NPC feature spanning HTN tasks, combat, speech, construction, prototypes, assets, and shared/client/server systems.
1259 | 8bfaccb741c1e11e4522da8663ce2076269fd2f8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1260 | f64291402bb76f61a116cb4174020617508c5a60 | Added more Syndie ammo types to EMAG lathe (#40822) | PortCandidate | Shooting, Gamerules | The emag lathe ammunition recipes are isolated data but require RMC ammunition and security-balance review.
1261 | c5b0fe97651f67c6f786b4051bc609b56c0aff79 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1262 | 5450dea450a0b53576f4fa019c7b60499e33b982 | minor tweak to changelog files, for contributor sanity (#40643) | Irrelevant | — | This changes only upstream changelog authoring metadata.
1263 | 5cb80f05b5cda34a19b1ca16d19f296521d6f680 | Update Xenoarchaeology Guidebook Page (#40621) | PortCandidate | Interactions | The xenoarchaeology guide and linked prototype descriptions can be adapted after checking CMU's retained artifacts and guide structure.
1264 | 0f3b8f37a63f3e992a1d057ddb3582ec6d23b167 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1265 | fab4752a2516ad55b86076ed2feef85496a07b5e | Infectious anom sprites for moths and arachnids (#39508) | Deferred | — | The large anomaly sprite replacement and species-specific injector data require direct asset and prototype reconciliation.
1266 | eaf3c0bd3ab712e2e3ae6ed4836faec08f2a5807 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1267 | 98e5e9a5cb5b811f6550f5d08f739ba4da86902e | hugbox tag fix (#40833) | PortCandidate | — | The HugBot recipe-tag localization correction is isolated.
1268 | b78bfded443ecf4f9f0ee6b6952b1cf4db318133 | Prevent mice etc from unwrapping parcels (#40838) | Ported (CS-0195) | Interactions | CS-0195 guards the unwrap verb with complex-interaction and containment checks so simple animals and packaged actors cannot unwrap parcels.
1269 | 5ac2bc22a171fc7e42667f913aa9c7d8478cdc57 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1270 | 68af461fc73ca20960bdb4152c1483c9565f2053 | Black Gloves Sprite Tweaks (#40825) | PortCandidate | — | The black-glove sprite and metadata corrections are asset-focused and need direct comparison with CMU resources.
1271 | fbc6dd68585f9ee3d463a132d7e2480a017ad4e5 | Durathread can now be printed by autolathes (#40837) | PortCandidate | Interactions | The durathread lathe recipes are isolated data, subject to CMU material availability and fabrication balance.
1272 | c500fd7531615a4fd226a73455327455861b8b72 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1273 | b1fa06d6b0bef78f03e712a4684a52e3e9b142d9 | Don't apply discount to reinforced glass  (#40839) | PortCandidate | Interactions | The reinforced-glass recipe can disable material discounts independently, with the matching arbitrage expectation updated alongside it.
1274 | 0ad48093a200d1d424411f5ffe44ca3757c2c5d0 | Fix visual bug with masks appearing to be pulled down after re-equipping (#40332) | Deferred | Interactions | The mask re-equip visual fix rewrites toggle cleanup and depends on the protection-state changes at index 1217.
1275 | 2ccd4e8ed3232047075a91ec374292f7a4f307cd | Makes droppers printable by autolathes and medfabs (#40074) | PortCandidate | Medical, Chemistry, Interactions | The dropper fabrication recipes are small, subject to CMU medical-fabricator and autolathe balance.
1276 | 84c091195109db7d402d492dcbf4734f26839003 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1277 | a0f0f40526d7356eb597b390e5fb471d93d061e6 | Reorganize tile StackPrototypes and add inheritance (#39491) | Deferred | Interactions, Physics | The 21-file tile stack inheritance migration changes StackPrototype data and every tile-stack family and must be reconciled with RMC construction.
1278 | e9469f07345021be46dca617f42ab9c4a85fa6ec | Adds swabs and an Emag inventory to the biogenerator (#39037) | PortCandidate | Medical, Chemistry, Interactions, Gamerules | The swab and emag-biogenerator recipes are localized data but require RMC botany, medical, and emag balance review.
1279 | 8830491ff08377cff185c3bbf3758d6b2dd73c3a | Automatic changelog update | Irrelevant | — | Generated changelog only.
1280 | d10cfbd875821ea9256ce3695f9d3a3ccc7ca38e | Add Syndicate IDs to the uplink for 1 TC (#38381) | PortCandidate | Interactions, Gamerules | The Syndicate ID uplink entry is isolated but changes antagonist access and economy balance.
1281 | 236609e2368ac82f99da4a536d3804974a32b94e | Automatic changelog update | Irrelevant | — | Generated changelog only.
1282 | a35c3030a65d2bfa0c3c687354c930011fb43f6b | Update Credits (#40841) | Irrelevant | — | Upstream contributor-credit metadata is project-specific.
1283 | 1250cb4d4462b763dc3ce63905a36a8719c055e2 | Organize StackPrototype with inheritance (#38412) | Deferred | Interactions | The 24-file material stack inheritance reorganization changes the base prototype graph used by many RMC resources.
1284 | 772d311b3ce9fa71fc025d99ead24767a2db8a63 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1285 | 31f8f768a06bb4a98889cdeb4fdd0fd5fda28092 | Brand new hair (#39850) | PortCandidate | — | The hairstyle prototype, localization, metadata, and sprite are self-contained assets.
1286 | c8023d791d98c6af6ae4099d5d469368b36c3786 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1287 | 7578f064611547a26a99b83c677b5a29e26e1c91 | Fix train parallax config (#40844) | PortCandidate | — | The train parallax cleanup is isolated configuration, subject to confirming the retained CMU parallax resource.
1288 | 2c1fc92e5c06efd7b3d09d24323435ead45227f3 | Quieter Meat Kudzu  (#39304) | Deferred | Interactions, GameTicking | The quieter Meat Kudzu behavior adds shared auto-emote prototypes and server timing that should be reconciled with RMC spreading entities.
1289 | 20175bffb241be3963048c1113ac007d1e85401e | Automatic changelog update | Irrelevant | — | Generated changelog only.
1290 | beb3db14f0dbad99e806bf2bc47632f4d02c8817 | Flash immunity examine visibility toggle (#40848) | PortCandidate | Medical, Interactions | The flash-immunity examine visibility flag is a small component and prototype contract, subject to CMU species and examine policy.
1291 | 4eaf7526e4a420351d923b7b7f9603b36a983ad6 | Fix patrons in in-game credits (#40840) | Irrelevant | — | Upstream patron credits and the associated dump script are project-specific.
1292 | 02a7f5721dc156fe130b0373d9908f393a73cfce | Automatic changelog update | Irrelevant | — | Generated changelog only.
1293 | 982624f0dd94472f987dc56cbbb6422e6c277f38 | Fix species not being ordered alphabetically in the character customization UI (#39359) | PortCandidate | Interactions | Alphabetically ordering species in the character editor is an isolated client UI correction.
1294 | e92597a4d9f755c87c8e7402c3a72ab90cf8657f | Automatic changelog update | Irrelevant | — | Generated changelog only.
1295 | a803bcca467ae47e5349e1c2ef9521a1e97004f2 | Explicitly attribute each state in organs.rsi, exchange CEV-Eris stomach sprite with /tg/station 13 (#39753) | PortCandidate | Medical | The organ RSI state attribution and stomach sprite replacement require direct asset comparison with CMU body resources.
1296 | 11525673ba352cdbe0edbafd06c3749d39151ed8 | Use PredictedQueueDel in SharedDestructibleSystem.DestroyEntity (#40856) | Ported (CS-0198) | Interactions, GameTicking | CS-0198 routes shared destructible deletion through PredictedQueueDel while preserving cancellation and event order.
1297 | 2696fd7cd50cb1ed097875c4edff00a8f2f61f48 | Automatically add trash tag to spent bullet casings (#40829) | Ported (CS-0024) | Shooting, Interactions | CS-0024 marks spent cartridge entities as trash by default while preserving an opt-out for reusable ammunition.
1298 | e3318ad17ab025e672fab3f0322a743c8f83bfbc | Plasma: reduced highly illegal syndicate shark attack rate (#40855) | Deferred | Gamerules | The generated Plasma map balance edit needs target-final map and event-rate reconciliation.
1299 | 43a0553e3013b58fa9c11189ef35d4d9de480bea | Automatic changelog update | Irrelevant | — | Generated changelog only.
1300 | dd278ab81551095c2a07aec9e298c897e98dd724 | Resprite Maint Hatch + New Syndicate Hatch (#38076) | Deferred | Interactions, Physics | The large maintenance and Syndicate hatch resprite changes airlock prototypes plus 43 texture files and must preserve RMC door inheritance.
1301 | b9254d9ebf79ff28e27f1c54205e89fece209ad4 | Replace all usages of /bin/bash shebang with /usr/bin/env (#40756) | PortCandidate | — | The portable Bash shebang cleanup is tooling-only but can be applied independently after checking CMU's shell entrypoints.
1302 | 4d316ae55334da8623d61e12b57cea5b2fdde4c9 | Stable to master (#40859) | Deferred | Interactions, GameTicking | The effective 12-file merge introduces side-effect-free pickup attempts, cancellable before-pickup events, popup control, lube migration, and instrument changes; RMC has many custom pickup subscribers.
1303 | 4059c29ebc760b9200db768d313b5a41f511b915 | Entity effects ECS refactor (#40580) | Deferred | Movement, Medical, Chemistry, Interactions, Physics, GameTicking | The 289-file entity-effects ECS refactor is a foundational migration spanning chemistry, medicine, botany, atmos, damage, status effects, movement, and hundreds of prototypes.
1304 | aede87c16363546d334aef4ced3f96fa65fa67ca | Stable merge (#40864) | AlreadyPresent | Interactions, GameTicking | The effective 10-file merge is engine-API compatibility cleanup; CMU already uses the static Logger and ISandboxHelper forms required by its newer RobustToolbox.
1305 | a91daa60d4f22c08dd5a3744801da06544228dd4 | Make Nutriment Work again. (#40869) | Deferred | Medical, Chemistry | The one-line Nutriment factor correction lives in the new entity-effect hierarchy and must accompany index 1303.
1306 | 35c783ecb19c15463a93f335b4289e9423f01e24 | Fix Plant Mutations (#40870) | Deferred | Medical, Chemistry | The two botany field corrections live in the new entity-effect hierarchy and must accompany index 1303.
1307 | 4aab1319adadd2eb28996bd5878670dc4a330f14 | NonSpreaderZombieComponent prevents infection of crit mobs (#40857) | Deferred | Medical, Gamerules | The critical-mob zombie infection change depends on RMC's divergent zombie and mob-state behavior.
1308 | 378acf97a03dae13e04742f6a72942ae64d98d16 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1309 | b0ddb872e611bd030f0bd4648b6a2c111a0ae59d | Plasma: add inlet pressure regulator to TEG burn chamber (#40874) | Deferred | Physics | The generated Plasma TEG map rewrite needs target-final atmos and map reconciliation.
1310 | 15e349401df4d09254eef475ddd52b5a94b94fa7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1311 | ed547468fc1733a2f4d8f90b88bad5bfd905278e | food box.yml cleanup (#40873) | PortCandidate | Interactions | The food-box prototype cleanup is localized but should be checked against CMU container inheritance and contents.
1312 | 4f3bd1fa5029ec993e2efa2a2e140d592b1fbb38 | Changed Soviet soda vending machine name (#40850) | PortCandidate | — | The Soviet soda vending-machine name correction is isolated prototype text.
1313 | 6aa0812fa25fde4d20d4287132308a406aa0ab0b | Fix barber scissors cancel errors (#40329) | Ported (CS-0194) | Interactions, GameTicking | CS-0194 clears the stored magic-mirror do-after identifier in every callback path, preventing stale cancellation errors.
1314 | ace8acde5691b41593876fbb7feada31bc3ce3d8 | Adds a guidebook reference table for silicon lawsets (#38225) | Deferred | Interactions, Gamerules | The silicon-law guide table adds client controls, shared prototype fields, references, and policy content that must match CMU lawsets.
1315 | 772f7890f8394d6d67da76d7dc729325ce7b98eb | Automatic changelog update | Irrelevant | — | Generated changelog only.
1316 | 6491cd1fca8088aa9e0cfadada206c2b2031bb0f | Stable to master without breaking anything (#40881) | Irrelevant | — | This merge has an empty effective first-parent tree delta, so there is no standalone change to port.
1317 | 3ed206887e4a07f1d44a5c1361445d6eac86c111 | Predict DestructibleSystem, Part 1: IThresholdTrigger (#40876) | Deferred | Medical, Interactions, Physics, GameTicking | Part one of predicted destructibility moves threshold triggers and behaviors into shared code and is prerequisite to later destruction migrations.
1318 | 931a3dd8ddba39bedf95d9be457f6f1f89bf1408 | Make SmartFridges airtight (#40196) | Ported (CS-0199) | Physics, Chemistry | CS-0199 adds airtightness to the standard SmartFridge while intentionally leaving the separate RMC smart chemical storage unchanged.
1319 | 7e9a7eeda6bb0570a67f57366749a81a27fa4ba2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1320 | 0a8268c6e4ffe19a85129e17183babc9090c687b | Remove high pitched buzzing noise from spray2.ogg (#40877) | PortCandidate | — | The spray audio replacement is a binary asset correction requiring direct listening and attribution review.
1321 | 6b379a584bff4c19aadbc6be8aad4a0057171b7c | Rename "trash" reagent to "reprocessed material" (#39761) | PortCandidate | Chemistry | The reagent display-name change is isolated localization, subject to CMU chemistry terminology.
1322 | 774468ad7173ddbea2ad211d1b5860c3cddc7619 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1323 | ee9d1032bb199e641098b670a34e99ea47d36c72 | Move ChatSystem.Emotes to shared (#40866) | Deferred | Interactions, GameTicking | Moving emote dispatch into SharedChatSystem changes 28 chat, speech, inventory, effects, mob, and server call sites including RMC-sensitive paths.
1324 | 9a05b1111f56688414129fa3e695f72f42e46f34 | Fix AI radial on objects without access (#38444) | Deferred | Interactions, Gamerules | The Station AI radial access fix changes shared AI control and held-entity behavior that must preserve RMC access semantics.
1325 | df6950aafb2d0476ea6c1eca47fb9cea8144c261 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1326 | 597560a27639adb46e5c5977d41d8168a0e47c13 | Amber Station Updates (#40717) | Deferred | Physics, Interactions | The generated Amber map rewrite requires target-final map, atmos, access, and power reconciliation.
1327 | cd55bda4d27272e1ddc7f8d9e36846b51fdb021a | Automatic changelog update | Irrelevant | — | Generated changelog only.
1328 | ce0ddc92d52b06f9f97efda2dc6c80825d64d97d | Change GeneralUser-GS soundfont to be full version (#40888) | PortCandidate | — | The full GeneralUser soundfont replacement is a binary asset update requiring size, attribution, and playback verification.
1329 | b4486a57630b6cb07814642c6f9c8110b27ef67d | Automatic changelog update | Irrelevant | — | Generated changelog only.
1330 | c455386ce3b33d023ab7334a4c1dcbd5ffe7cdbc | Fix Cryptobolin and make it use NewStatusEffectSystem.  (#40675) | Deferred | Medical, Chemistry, GameTicking | The Cryptobolin correction migrates speech effects to the new status-effect system and depends on the broader medical and entity-effect chain.
1331 | d3898dd162b1e6b3460d8f400a170e044158a735 | slime guidebook change (#40842) | PortCandidate | Medical | The slime guidebook correction is isolated user-facing documentation, subject to CMU slime mechanics.
1332 | bbadd4d483407fe3a9286975658c565c7292047f | Automatic changelog update | Irrelevant | — | Generated changelog only.
1333 | 9766c380328d8cb74f5f3919d4800e2e4dfe82a2 | Update engine to v267.3.0 (#40899) | Irrelevant | — | This obsolete engine-pointer bump is superseded by CMU's newer pinned RobustToolbox and the engine submodule is outside content-port scope.
1334 | 4b9a9be7bb976a7ebf98c8d36f314193c2656d01 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1335 | 4439e42347bcf0e78b9e0e81bd2b7b730b96a50b | Rollerbed & Bodybag tweaks (#40880) | PortCandidate | Medical, Interactions | The rollerbed and bodybag prototype tweaks are small but should be checked against RMC medical logistics and storage behavior.
1336 | 0842f9f979e7e7d1015fad1292067f29e6c30776 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1337 | 8bd4b58474c9dd891e992b4a4c22a588ba77ae80 | Document tags: G (#40898) | Irrelevant | — | This is tag documentation and declaration-order cleanup with no standalone gameplay delta.
1338 | 246a72f2f8c9d6de91af9cc380f4464b40bbec17 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1339 | fdbcd3fdc5ed05026b820118a663f345424d1036 | Add date picker (#40660) | PortCandidate | Interactions | The DatePicker client control and localization are self-contained, subject to the target UI framework.
1340 | b96ef5c10430c269e953e6a24f9cfcf244f5c08a | Singularity eats carpet (#40896) | PortCandidate | Physics, Interactions | Adding a non-hard fixture lets singularities detect and consume carpet; the isolated prototype change should be checked against RMC fixture layers.
1341 | 6be5e5aacd90a9252667f178bdbc2e6dcb881c71 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1342 | c77c4abe5fa77bbc29fec04ea653f8f3e17b4773 | Give destroyed machine frames outlines (#40892) | PortCandidate | Interactions | The destroyed-machine-frame outline is an isolated prototype usability improvement.
1343 | 96347a78af8049fdc8d9c74282f1ba0315890566 | Fix zombie locked rotation (#40812) | PortCandidate | Movement, Gamerules | Removing forced zombie combat mode fixes rotation with a small code delta, subject to RMC zombie control behavior.
1344 | 607498997248e26636eebd25cadca845556e5202 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1345 | 1e14b94da66b1a7f75e1015f8bf58581bac0f41b | fix special scrubber/vent tags (#36326) | Ported (CS-0200) | Interactions, Physics | CS-0200 moves AirSensor and ForceFixRotations tags from the shared base to the concrete sensor so vents and scrubbers do not inherit sensor-only identity.
1346 | 957e3ac081923c8af6e555adcb4f29b87725d937 | New botany poster (#40908) | PortCandidate | — | The botany poster prototype, random-spawner entry, metadata, and sprite are self-contained assets.
1347 | 7a3fa2c6750e2153414b03de1e49cd1a425f1150 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1348 | a6938a64421a115c0bc2f956f37fffffbf58695b | Fix generating migrations with USE_SYSTEM_SQLITE (#40910) | PortCandidate | — | The SQLite migration-generation guard is an isolated database tooling fix, subject to CMU's current migration setup.
1349 | 3fb57679beb46519a13f129a5776a3b5013c9360 | Prometheus exporter for github repo stats (#38607) | Irrelevant | — | The GitHub repository statistics exporter is upstream project tooling with no game or build dependency.
1350 | d81fba01cea3e2cf24e97ce01993a0f53ff30aca | Improve IPIntel reasons (#40071) | PortCandidate | Gamerules | The IP intelligence reason strings are isolated connection-message localization.
1351 | 1a5be55c70090a6dcdf86f96304ee592c008afac | Clean up TitleWindowManager.cs (#36327) | Irrelevant | — | This is client title-window cleanup with no retained standalone behavior to integrate.
1352 | df6ce7f473a5533981b7aede1c0383383ed95bf2 | Fix Atmospherics dP not trolling partially airtight entities (#40435) | Deferred | Physics, GameTicking | The partial-airtight delta-pressure fix changes atmos queries, component state, update processing, and regression contracts.
1353 | cb9a4bb67aa587de0db92be956e4b31fe9f12182 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1354 | 52a9f9b576f5f75f4ad40c2b824b52bddf1e8825 | FTL Fixes (#39040) | Deferred | Movement, Physics, GameTicking | The FTL correction changes shuttle transition timing and state handling and must preserve RMC dropship and shuttle extensions.
1355 | 9fa52ed90a2b2ba49e42c6437c27d31667763588 | Improve lying trait grammar (#39370) | PortCandidate | Interactions | The lying-trait grammar rewrite is localized to accent replacement data and speech strings, subject to CMU wording.
1356 | 96712acb08654c7305e8bbc7127d47b6f9b9292e | Automatic changelog update | Irrelevant | — | Generated changelog only.
1357 | f6972de87d5b8c6c0e9d6710239bc3c575e42869 | Don't add ImplicitRoof to grids with roof component (#38551) | Deferred | Physics, GameTicking | The implicit-roof fix changes shuttle grid initialization and shared roof-component contracts around RMC maps and dropships.
1358 | 111805c03bd6a557e3d359ecc0699a5516b13935 | Fix sericulture zombies (#40279) | PortCandidate | Medical, Interactions, Gamerules | The sericulture zombie correction is small shared logic, subject to CMU zombie and species-component behavior.
1359 | 2f09b117ca07d543aeaee31f48d4e8ef87265d78 | Fix emag sparking animation on doors (#40350) | Deferred | Interactions, GameTicking | The door emag visual fix adds networked animation state across client code and many airlock prototypes that diverge in RMC.
1360 | 41bdf00a3512241f83136c9bb3c4b3ca5a031f7c | Automatic changelog update | Irrelevant | — | Generated changelog only.
1361 | e92b48c1fa90b95bf694feeba2d2bc97618f2efe | Logging for turret controller (#40884) | Ported (CS-0201) | Shooting, Interactions | CS-0201 logs authoritative deployable-turret armament and access-exemption changes without altering turret behavior.
1362 | c83b8c2cf866d5353977c8b4183d3aebaf61295a | Undo effect logging changes (#40919) | Deferred | Chemistry, Interactions, Physics, GameTicking | The effect-logging rollback changes the new entity-effect API from index 1303 and must be carried with that migration.
1363 | 3ac4816723cca0ef2c512a94eaf2c4f2f3685cf7 | Resprites and keeping consistency for forgotten figurines (#40889) | PortCandidate | Interactions | The figurine resprites, pools, prototypes, localization, and migration data form a self-contained content update requiring asset comparison.
1364 | 6fbcc6d0fb797651fba29ded7ad126b8aeb7176f | Fixed votekicks putting you on a one hour vote cooldown (#40622) | Ported (CS-0193) | GameTicking, Gamerules | CS-0193 applies votekick.timeout in seconds to both initiator and same-type votekick cooldowns.
1365 | bd1cbabea89b326f46ce7d97a86e99354e796ed7 | Add admin ui tests (#40914) | Irrelevant | — | The commit primarily adds admin UI integration-test hooks and regressions without a standalone gameplay change.
1366 | 5ddcfc528533469c294f878a0eff64973606b4ec | Ashtrays can contain ashes and matches (#40926) | PortCandidate | Interactions | The ashtray tag and counter additions plus Burnt ash tagging are isolated prototype corrections.
1367 | 704521d8dfc426b372b19373dd31384990449a51 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1368 | 68f9d748a2f4398a9d60bc42e19e272b7162c358 | Fix ResearchSystem threading exception (#40917) | Ported (CS-0197) | GameTicking | CS-0197 returns a fresh research-server lookup set per call, eliminating shared mutable enumeration state.
1369 | 71bbe926a086835a0c197393c36bdd24c6d383a2 | Add bank toolshed commands (#40614) | Deferred | Interactions, Gamerules | The bank toolshed commands rewrite cargo banking APIs and administrative command behavior around RMC's divergent economy.
1370 | 7755009fe32fb5e18dc65b89e1d88efe8e1f39ff | Automatic changelog update | Irrelevant | — | Generated changelog only.
1371 | 9562e009bf0e06d5d94742e2cd9de0512bad4262 | Make parcelwrap able to wrap humanoids (#40911) | Deferred | Medical, Interactions, Physics | Humanoid parcel wrapping is a large shared component, interaction, prototype, localization, and asset feature requiring RMC containment review.
1372 | f8ea0e98c0356e0efc64a64a12020c5fd05dcb4f | Automatic changelog update | Irrelevant | — | Generated changelog only.
1373 | 61cab596d84c891f2078b89a5f370ea501f2c67a | Don't preload purple_nebula.png parallax sprite (#40936) | PortCandidate | — | The parallax sidecar prevents unnecessary texture preloading and is isolated resource metadata.
1374 | b84931dd390f29e388dbf496922ddedf9be50b87 | Grenade penguin htn (#34935) | Deferred | Shooting, Interactions, GameTicking, Gamerules | The grenade-penguin HTN feature adds NPC tasks, combat behavior, containment checks, uplink data, and prototypes.
1375 | 7ad68e701000db2113e19c080d54d6a251cc8724 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1376 | 3a0095ba424813eda3002e1931b89b3fbe33d684 | Reorganize ID card sprites to use job icon sprites directly (#40414) | Deferred | Interactions, Gamerules | The 104-file ID-card sprite reorganization changes job icons, card prototypes, objective targets, emag visuals, and many assets that diverge in CMU.
1377 | 0def0bf5645ec2b0f437fe6782c6011e433bcecb | Automatic changelog update | Irrelevant | — | Generated changelog only.
1378 | b10dd2edca91c99c6590ff974edca2414a7d5b36 | Fix power sensor looking at wrong electrical network (#40934) | Ported (CS-0202) | GameTicking, Interactions | CS-0202 reads power statistics from the sensor's selected cable node group instead of traversal-order-dependent reachable networks.
1379 | 5757fc95e06a10c0b90981e4191f3fe5281d5ae5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1380 | f0512d0e0f512aac824aee72984dee7ec01c73a0 | Bring sky blue carpet in line with other carpets (#40867) | Deferred | Interactions | The sky-blue carpet parity feature spans construction graphs, recipes, curtains, tables, lathe data, localization, and many assets.
1381 | eabb5e44db1bf89fd2882240697abf8f69b04bb8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1382 | 98ee5be3d4c5bac0bd52d9191be7aae7f0cbfb9e | Fixes parcels and parcel wrapped humanoids being invulnerable (#40940) | Deferred | Medical, Interactions, Physics | The parcel damage threshold fixes invulnerability introduced by humanoid wrapping and must accompany the full parcel chain.
1383 | 993b65ed5d9d9aae64666df14a418e6eec0a4b53 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1384 | e9ff240f84a1274d0f9a25e1f88b32c13e7f4c9c | Fix custom MIDI instruments sounding incorrect; add two more microphone instrument options (#39210) | PortCandidate | Interactions | The MIDI fixes update instrument prototypes and a soundfont asset, subject to CMU playback and PAI/instrument inheritance.
1385 | 5a277268b988f7c0724442b42148836d01f9d799 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1386 | e362ee121f36a31bf2c78c63e36f887b5aeb282f | Add "Reset to default" verb to `TriggerOnVoice` (#35636) | Deferred | Interactions, GameTicking | The TriggerOnVoice reset verb adds state mutation, localization, and prototype behavior atop the target trigger framework.
1387 | 853bb1d3c026f9b2cfd2299fcce331a9971249a6 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1388 | ee33792b33ea1d2efd67c467a4a4c386870dd2a9 | Event based lock access (#40883) | Deferred | Interactions, GameTicking | Event-based lock access is a broad shared access, delivery, fingerprint-reader, component, and lock-system API migration with many RMC consumers.
1389 | 5a244ed63af26d103b33a6184eb825836cd5e59c | add the diona typing indicator to the FloraTree entity (#39103) | PortCandidate | Interactions | The Diona typing indicator is a small FloraTree prototype correction.
1390 | a958f3ea1b09a7da37909ceb1fe18d8176de61ab | Remove two obsolete buttons from the Admin UI (#40904) | PortCandidate | Interactions, Gamerules | Removing obsolete teleport and player-action admin buttons is isolated UI cleanup, subject to CMU admin tooling.
1391 | 132d655ceb972e6e27eef2a07a63aa5daf342cd2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1392 | 58218a58dd6bc01177e3e479e945eec3b04843ba | Fix some food recipe categories (#40949) | PortCandidate | Chemistry, Interactions | The food recipe category fixes are isolated guide and cooking prototype data.
1393 | 584825ec19b1e4d0c2c78ef77d4bb49e73fa5800 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1394 | 8d47febe1d7eef8bb5f81423704a85ddbc9456a8 | Added Vox Chitter and Clicking (#40878) | PortCandidate | Interactions | The Vox speech emotes add two attributed audio assets and small species and voice-prototype data.
1395 | eec7fb6cf1565bef42f005966e4b23cc9a278e2a | Add GenpopLeave and GenpopEnter to Security accesses (#39515) | Deferred | Interactions, Gamerules | The Genpop access additions touch security job prototypes and must be reconciled with RMC's divergent access and role definitions.
1396 | 6a532699cd5f600fbd08e8e37cd10662730c425b | Automatic changelog update | Irrelevant | — | Generated changelog only.
1397 | d8c2ca1b41069345508b2fb68ff88a9943980252 | Arrival signs on fland (#40942) | Deferred | Interactions | The generated Fland arrival-sign map edit needs target-final map reconciliation.
1398 | 7a2206d011ec9605679295f8dd0047ccf60258dd | Fix recharging spray painter (#40953) | Deferred | Interactions, GameTicking | The spray-painter charge fix relies on the target-final limited-charge and spray-painter implementations, while CMU's current shared/server split differs.
1399 | 492a1aa9c3a751233be6fc21980daa85cd38bf42 | Hitscans are now entities (#38035) | Deferred | Shooting, Physics, GameTicking | The 21-file hitscan-as-entities rewrite replaces a foundational ranged API and must be integrated with RMC guns, projectiles, prediction, collision, and effects.
~~~

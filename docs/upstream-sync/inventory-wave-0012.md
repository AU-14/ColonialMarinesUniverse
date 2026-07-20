# SS14 upstream inventory: wave 0012

Audit date: 2026-07-20

- Pinned baseline: `59633f6dc50e77dda8cefa344d87c7b01e06a810`
- Pinned target: `40ca2c7f90d11d27be5457d177c133f0947d1c08`
- Range: ordered first-parent commits, zero-based indices 2200 through 2399
- Columns: index | full SHA | exact upstream subject | disposition | core-system areas | rationale

`Ported (CS-####)` links an accepted core-system change to the durable audit, while
plain `Ported` identifies accepted non-core work. `PortCandidate` retains target
behavior that still needs integration. `AlreadyPresent`
means CMU already has equivalent behavior. `Deferred` preserves behavior pending
focused reconciliation. `Superseded` means another target or local architectural
change replaces the commit. `Irrelevant` identifies commits with no standalone
behavior to port.

~~~text
2200 | 4eab48fe355dd6bcc6c0e41254732c58ca3eaff1 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2201 | 1801f474184dbfdf2672567990c8a0cce5991260 | Fix broken FTL references (#42181) | AlreadyPresent | Medical, Interactions | CMU's later injector localization already resolves both popup references to valid retained keys.
2202 | 540f4e4c61dd4f135014f1c428ac2cd0a013d2f6 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2203 | e9932ec0ea4f3e2954db734c2078e0545d7734a5 | Happy 2026 (#42186) | Irrelevant | — | License-year metadata only.
2204 | 24005e3e936ac7e1256f7a6ad2a5b22ca6c0e64d | Jet Injector Tweaks and Cleanup. (#42158) | Deferred | Medical, Chemistry, Interactions | The nine-file jet-injector behavior, prototype, locale, audio, and asset cleanup needs RMC injection reconciliation.
2205 | 67da176eb98b54bf62402653671d20b3f4d76ce0 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2206 | 6e55a7bac48d78617694ac2b470e8cedf3f99751 | Make some HeatContainerHelpers methods byref (#42197) | Deferred | Physics | The by-reference HeatContainer API migration changes central temperature helpers and should land with its dependent guard fixes.
2207 | 445d1b673ba7212eeec6d0d9f2d453842fd98735 | Fix RCDDeconstructableComponent filename (#42180) | Irrelevant | Interactions | Filename-only source rename with no runtime behavior.
2208 | 4920c9e9079fd0cf45db4258b3c29943efdf3123 | Update (MOST) instances of `EntityUid, Component` in GunSystem to `Entity<T>` (#41966) | Deferred | Shooting, Interactions, Physics | The 58-file GunSystem Entity<T> migration crosses heavily divergent RMC firearm, NPC, turret, and client code.
2209 | 74d482c5b26807c1e05729d90ff087998d98e318 | Revert "Exo - Exomas Version (revertable)" (#42203) | Deferred | — | The large Exo map revert needs target-final CMU map reconciliation.
2210 | 6de41e8051e0d31a1e6653368ee7c3bc606828e5 | Revert "Christmas-ifed Packed Station!" (#42202) | Deferred | — | The large Packed Station seasonal map revert needs target-final CMU map reconciliation.
2211 | 4f1a1118b14228c7fe7a63ee8a5e2df5eeaae094 | Update RT to 270.1.0 (#42198) | Superseded | — | CMU already pins a newer RobustToolbox generation; the engine submodule is explicitly outside content-port scope.
2212 | e1b790eecdd88d5696ac403321ed593080df6194 | Make xenoborg thrusters anti-easy-sabotagge (#42201) | Deferred | Movement, Interactions, Physics | Xenoborg thruster sabotage resistance crosses retained borg, shuttle, anchoring, and damage policy.
2213 | e5d8800b42a3fb9670c622a558f34817b791f617 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2214 | 4b9ef4749c26f47a9a07230a531fd85890bb918b | Snowball fixes (#42124) | Deferred | — | The generated Snowball map rewrite belongs to the deferred Snowball map cluster.
2215 | 732db1921b48dea0f7a957fa2257ed2055a76774 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2216 | c796eb372f40b6c6de3281f5dd839769aa8ae523 | Guard against div/0 for HeatContainerHelpers (#42213) | Deferred | Physics | Division guards depend on the index-2206 HeatContainer API migration and need temperature-behavior tests.
2217 | 9754944f1138acd5873c00422431b7a3a7ada6ca | expanded FillLevelSpriteTest test and fixed found issues (#34165) | Deferred | Shooting, Interactions | The expanded fill-level integration test also changes several prototypes and binary weapon assets, requiring target-final asset reconciliation.
2218 | 856ad1164050bc23afbedcc7194de242c2c0def7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2219 | d366c67baf62b4aab9addf2de63e6d70e8c29291 | Fix style classes used on monotone labels (#41969) | PortCandidate | Interactions | Two isolated stylesheet class corrections can be adapted independently.
2220 | bdb710270ab7a93d5bbff3dd0bf152b7a584c9e1 | Intercom resprite (#41962) | PortCandidate | — | Self-contained intercom sprite and metadata refresh.
2221 | bdbc1480549b546469af9cd666f97f7b01beaa23 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2222 | fc995820df3d72a0040300ecd9c51be26096b5d7 | Ironsands Structures (#39793) | Deferred | Interactions, Physics | The 77-file Ironsands tile, wall, door, recipe, research, and asset feature needs a dedicated content integration.
2223 | 269bd56844590d8566d7f2683f91ca73a6c82a7c | Automatic changelog update | Irrelevant | — | Generated changelog only.
2224 | da4a488197e6f5d79a495e7f7a0cf599a38bbe18 | Melee weapons animations upgrade (#41425) | Deferred | Interactions | The melee animation contract changes shared attack effects and component state across RMC-divergent melee code.
2225 | 4d19496dbdaa990899b7bc989ef45dd19bbd15bc | Automatic changelog update | Irrelevant | — | Generated changelog only.
2226 | d7219bd499959bf0c34cf284c8056f8eac6391d8 | Update Credits (#42228) | Irrelevant | — | Upstream credits metadata only.
2227 | fe6a2f07089a3ed64f4aaf7daff0ce0b64fd1f59 | Stable to master (#42238) | Deferred | Movement, Shooting, Interactions, Physics | Effective first-parent delta is non-empty at 24 files, +136/-263, mixing research, projectile penetration, movement assertions, chat, UI, and prototypes; reconcile its target-final behaviors separately.
2228 | fa7c2be1640f27f3ea79b68d1f55fdaeb75cb34f | Dragon rift no longer deletes all rifts when destroyed (#42234) | PortCandidate | Interactions, Gamerules | The focused dragon-rift fix stops one destroyed rift from deleting every active rift.
2229 | d3d35000e1a7c4b29849abea223b8e9066b45b13 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2230 | 62c1302a55d6688e1e2e4649e2dbfca0ced7f17b | fixed typo/duplicate adjective (#42249) | PortCandidate | — | One dataset adjective typo and duplication correction.
2231 | e572d75f045e6b801614966381657e3804118ce9 | STABLE -> MASTER (#42251) | PortCandidate | Interactions | Effective first-parent delta is one file, +1/-1, correcting starting-job localization in the admin name overlay; reconcile with index 2246.
2232 | 71c3fa8fd732bc7b4f4444713ee12934d330d8e2 | Predict thieving beacon (#39610) | Deferred | Interactions, Gamerules | The thieving-beacon server-to-shared prediction migration changes foldable and objective state ownership.
2233 | 122feda215f253767addae329d7fd6de9f6b7856 | Msg Toolshed Command (#41936) | PortCandidate | Interactions, Gamerules | The bounded admin Toolshed message command and prayer hook can be reviewed independently.
2234 | d3137c2d381085895b485beadf2a4c273b9c6741 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2235 | 54d7f2b7365c754e63474e7c2c9aabffbdb55ccf | Cleanup Toolshed Locale (#42259) | Irrelevant | — | Toolshed localization-key organization only, with no standalone gameplay behavior.
2236 | 4b7aaa3a46c594c7f8522f0f2e6d09df3daeaa47 | jugs closeable, move chemistry entities into chemistry directory (#29413) | Superseded | Chemistry, Interactions | Index 2242 reverses the closeable-jug behavior, while later target and CMU chemistry layouts replace the remaining path-only organization.
2237 | 19b1f4787f03a0c6b3417fdad4ac73ea93a28be5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2238 | 2a71253f57daf469b60960b407c20a62090cfef8 | Move some miscellaneous random spawners to entity tables (#42245) | Deferred | Interactions, Gamerules | Six random-spawner prototype rewrites depend on the wider entity-table migration and retained CMU content pools.
2239 | ae414ac94bcb14ce001abbea138bee1cd5598074 | Fix da rulez (#42264) | Deferred | Gamerules | Upstream server-rule wording is policy content and must not overwrite CMU rules without review.
2240 | a287d5c3f7c367bb1d1d57b99c80087880cdcdc1 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2241 | cdd990ba56cf65d35543ba56101d83dea48db75c | Adds sky blue curtains/tables to their respective spawners (#42266) | PortCandidate | — | Two small spawner-table additions for existing sky-blue furniture.
2242 | e2baaa1c313ebb63fdbcef245391886e56d87c0e | Revert Closable Jugs (#42267) | Superseded | Chemistry, Interactions | The revert cancels index 2236's jug behavior, and CMU's retained non-closeable jug architecture already replaces this intermediate pair.
2243 | f92ed8418b2f8841aedb6f2a77687c3afb3c0d06 | [FEATURE] More icons (#42200) | Deferred | Interactions, Gamerules | The 25-file job, antagonist, PDA, ID, icon, and corpse asset bundle crosses the RMC role roster.
2244 | c81e671a742d868f79e69ec4514403ddfa703962 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2245 | 2176f00f19bc806cb46336a86e2da62ba94ab28a | Replace recently added StorageFill with EntityTableContainerFill (#42269) | PortCandidate | Interactions | One emergency-box fill migrates cleanly to the existing entity-table container contract after substrate verification.
2246 | 279dabd8899ea84acc7b6e9fa9d18186196e7567 | Merge stable into master (#42274) | PortCandidate | Interactions | Effective first-parent delta is one file, +2/-2, correcting admin-overlay role-name localization; pair it with the index-2231 overlay delta.
2247 | de672944e002481fd7ccaf87e690771a3fa07ec7 | Guarantee glue and lube in toybox (#42146) | PortCandidate | Interactions | Two prototype entries guarantee existing glue and lubricant items in the toybox.
2248 | 4ed0d37efc151dc46cf822c5c9f57c479d87dc2f | Automatic changelog update | Irrelevant | — | Generated changelog only.
2249 | 590dc948ee87b1c1229235342cfe9bea2378aa2f | Chameleon Projector Battery, Price Decrease (#42271) | Deferred | Interactions, Gamerules | The nine-file chameleon-projector battery, visual, borg-module, and uplink rebalance depends on the deferred predicted-power cluster.
2250 | e3419b159ec5bb2a9c14ef7678e266e8c1941519 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2251 | 20d1b2c6cb47d0c201c8461e0432a4b8b76669ed | Fix attributions for /Resources/Audio/Misc/ (#42230) | Irrelevant | — | Audio attribution metadata only.
2252 | a8469ca509d5409282083b96aa886dcaa0dd4bef | Predict Rotting Examine (#42254) | Deferred | Medical, Interactions | Rotting examine prediction moves server state into shared components and systems and needs RMC corpse-state reconciliation.
2253 | d65aa07a843836e6629b50761ae18075012d5d8f | Grappling gun rope visual change (#42207) | Deferred | Shooting, Interactions, Physics | The grappling rope and hook visual change depends on the earlier deferred grappling feature and local tether behavior.
2254 | 41f91a920799a136e38b1c16013cfffe3ebf25e6 | Xenoborg camera monitor now shows xenoborgs names (#42205) | Deferred | Interactions, Gamerules | Showing Xenoborg names on camera monitors changes surveillance state and retained borg identity policy.
2255 | 66c1a989fdebb4d06383cb4b4ba868b98ba29c92 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2256 | 03b7788774b6af979ab674cdcd596fbfea3cf3ac | Vox now say they become fried chicken upon taking enough heat dmg (#42280) | Deferred | Medical | The Vox burn-body behavior and species prototype change needs CMU species and destruction-policy review.
2257 | 8be191ab8c0f18ba5f82e03d7eb52aa6dc4db82e | Automatic changelog update | Irrelevant | — | Generated changelog only.
2258 | 8b9801a5bbc6be39b399be7c4bda7a2c4275ef50 | Reorganize and clean Fun yml (#42184) | Irrelevant | — | Large Fun prototype file reorganization and cleanup with no standalone behavior to port.
2259 | 95bdc66f1036af60baf44b41c2a1c0a01b13207e | Automatic changelog update | Irrelevant | — | Generated changelog only.
2260 | b267bad9901b951349465c32a4e1df6ab2bd3945 | Ninja bomb planting tweak (#41208) | PortCandidate | Interactions, Gamerules | Small Space Ninja bomb timing and guidebook adjustment.
2261 | 3633cdb537365f3943d97a9ce435fe85d88d4239 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2262 | a9b953cdfe7cb695cf5ea93ea3d5ccf05b3ca3ff | Add origin member to class (#41250) | AlreadyPresent | Medical, Shooting, Interactions | CMU's DamageModifyEvent already exposes the originating entity through its retained RMC damage-event contract.
2263 | 74ead53ceb9e25c9db547e63011b8f3c556dbffd | Remove yaml'd non-existent components + test for that (#38878) | Deferred | Medical, Physics | The stronger unknown-component integration test and prototype removals must be adapted to CMU's current component-registration and RMC prototype layout.
2264 | da7bbe5918754a6c11e18e28ba76122324af7825 | Warden Suit Tail Fix (#42276) | PortCandidate | — | Isolated Warden hardsuit reptilian sprite correction.
2265 | 142ce2a59b2fc3f1e28c31bc538bf9cce1bfc28a | Fix Capitalization on HoP's Fountain Pen (#42300) | PortCandidate | — | Two capitalization corrections on the HoP fountain pen prototype.
2266 | 350c67c73ee0188e948da9de70d675c1d7d82784 | Fix Internals Sounds not working.  (#42304) | Ported (CS-0230) | Interactions, Physics | Connecting now stops the outstanding disconnect stream and disconnecting stops the outstanding connect stream before predicted playback, preserving rapid internals-toggle audio.
2267 | 7aba244b389d869b3fe0e1044e6922b6bb413666 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2268 | e27ae3d42866ba254975b194c700e1a987e2e836 | Goliath Hardsuit Fixes (#42303) | PortCandidate | — | Small Goliath hardsuit prototype and Vox sprite correction.
2269 | 80d38c51b376f9185eb1e8a8d0f5b96f03d53ec5 | fix electrify sound effects being reversed (#42294) | Ported (CS-0231) | Interactions, Physics | The shared sound fields now name the matching on/off assets and Station AI selects by the resulting enabled state; CMU's older door remote has no electrify mode to adapt.
2270 | 28e830f8b4343183ca6981a42fcff892650cdea1 | Fix forced vaping checking if the user's mouth is blocked instead of the target's. (#42311) | AlreadyPresent | Medical, Interactions | CMU's older IsMouthBlocked API is already called target-first, so forced vaping checks the target rather than the user.
2271 | 019268b0561f3bb86e7eadc50ab1e57c7a440a89 | Remove battery from the handheld health analyzer (#42292) | Deferred | Medical, Interactions | Removing the handheld analyzer battery also changes borg modules, lathe recipes, and migrations and belongs with the power-cell cluster.
2272 | e4ac948dec4a7e9e2c95a4650f3351cc7dc60af4 | fix: respect AllowedSlots for gogo hat (#39189) | Deferred | Interactions | The inventory-slot helper API and voice-controlled storage fix need reconciliation with RMC inventory semantics.
2273 | a42eb5695cc23d8004f2e2c5af2d7c0f09553e78 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2274 | 5d5c61fefc13c36f1d4d14101edb23fa129c83a0 | Bring back shrug sanitization in a different form (#41236) | PortCandidate | Interactions | A small chat sanitizer entry restores the shrug shorthand without broad chat-system changes.
2275 | acc95fae5eaf9aba67171667dae3665142bc35e5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2276 | f8ff3a92aa97a5a13d32296c7606698cb464769e | Fix broken state when attempting to escape a locker while cuffed (#42313) | Ported (CS-0232) | Movement, Interactions | Locker resistance now marks the user as resisting and shows its start popup only after the escape DoAfter starts successfully, avoiding a permanent stuck state when setup is rejected.
2277 | d0e981626150b27e78aedb44cf9ee7e3fff5f429 | Fland: Fix atmos right side apc (#42314) | Deferred | Physics | Generated Fland APC and atmos map delta belongs to the deferred map and powernet cluster.
2278 | b406193372b3216f7f2853fd5b6c804bd914d1ac | Automatic changelog update | Irrelevant | — | Generated changelog only.
2279 | 8ec4669bf93155bbc81aa365ad2143ad62989ab2 | Allow items spawned in the smart fridge to show up as an entry. (#42268) | Deferred | Interactions | The five-file smart-fridge state and UI change begins a cluster completed by index 2285.
2280 | 386aca70c73d1f1f6d340f520696819fab4b8950 | Add craft for bonfire and bonfire with stake (#42211) | Deferred | Interactions, Physics | The bonfire-and-stake construction feature adds buckle ignition systems, recipes, prototypes, and assets requiring focused integration.
2281 | 22682fccc3bdb33db800d9e5aea9d4f590e70640 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2282 | ec024001e715faaebb60d2df9a1c262504d81cf9 | Increase shuttle FTL cooldown to prevent FTL spamming (#42209) | Deferred | Movement, Physics | The shuttle FTL cooldown and new CVar materially change retained shuttle timing policy.
2283 | 16c9cfe8999c67a920a015c00f941e1642ff7799 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2284 | 51e7a39bade7aad1657b033116e79f1900c490fa | Predict DrainSystem (#41711) | Deferred | Chemistry, Interactions, Physics | The DrainSystem server-to-shared prediction rewrite needs RMC fluid behavior and the later drain tests.
2285 | 78343b2dbb4f4706c80245a0fb061f7b7c7a115d | feat: allow removing empty smart fridge entries (#39195) | Deferred | Interactions | Removing empty smart-fridge entries depends on the index-2279 shared state and UI migration.
2286 | 7586c8017aa79e6782d801435b082fb5c754ba5c | Automatic changelog update | Irrelevant | — | Generated changelog only.
2287 | 85b3dcc9cce30214399266f57b071c570d401d89 | Stake Admin Alert (#42324) | Deferred | Interactions, Gamerules | The admin-alert addition is an incremental part of the deferred bonfire-stake construction cluster.
2288 | 96d23393450a42c239582fd1107f166159c790d4 | Fix projectile deceleration (#42320) | PortCandidate | Shooting, Physics | A small projectile prototype fix replaces zero damping with tile-friction immunity, pending RMC ballistics validation.
2289 | 72b022349f7dbab98fbdfde684358dc5f344a436 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2290 | d7fcb033369c76cdc1509e89fd504daee90fece9 | BUGFIX: Cabbage placed on taco shells no longer turns into a carrot (#42326) | PortCandidate | Chemistry, Interactions | One food-sequence prototype correction prevents cabbage on taco shells from resolving as carrot.
2291 | 9256f3f2a1b272898a351d3e6fcf17e89ddfcff8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2292 | a7fc17dfc48beca3807155d9cc443f6f707a66d5 | Add the Syndicate Delivery Console + Corpsman Medicine Bundle (#41201) | Deferred | Medical, Chemistry, Interactions, Gamerules | The Syndicate delivery console, catalog, medicine bundle, UI assets, and store data form an eleven-file antagonist feature.
2293 | 32dafcf2ea7567cf7745e7b18abe99810bd2bdd3 | Foldable wig on clowns mask (#42208) | PortCandidate | Interactions | Self-contained foldable clown-mask prototype and sprite states.
2294 | 4ebdbff86b03749dcfd0d60bd63be4b6ac460dca | Automatic changelog update | Irrelevant | — | Generated changelog only.
2295 | 319617f6ba923f31c8a14b5cc12e0a0f42d0c23d | Use NextByte to properly construct colours (#42335) | PortCandidate | Interactions | A three-line client color-generation fix uses byte-valued random channels.
2296 | c3d7652620cf85c1a0c591d96790462ac48c4a02 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2297 | 5d9371931a00e9c2811f6f0869ec2b23e1099015 | Predict Mind State Examine (#42253) | Deferred | Medical, Interactions, Gamerules | Mind-state examine prediction moves SSD and examinable state across seven divergent client, shared, and prototype files.
2298 | 46e86149e9111ce278a9a9dc713a77777b540043 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2299 | f0ae5896b7584de986888fe7deca250285f3098b | Update Credits (#42352) | Irrelevant | — | Upstream credits metadata only.
2300 | c0fbaf1228f04acd6d2a150854461a3ca95178ca | Fix warning spam from ShortKeyName (#42351) | AlreadyPresent | Interactions | CMU's BoundKeyHelpers already matches the pinned target and returns the resolved short key name without a second localization lookup.
2301 | 5025e0d28695f01fcb049dc61ddaded2dde72a8b | Janiborg Module Cleanup (#42330) | Deferred | Interactions | Janiborg tool and module cleanup must preserve RMC borg modules and local tool qualities.
2302 | 3a0049e5349b7f3d377b0c40611414f278a3fca0 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2303 | b707110dea2fb4cbb049a5a2ec4654573e55cb93 | fix: clear health bar/icon overlay damage containers on update (#39288) | AlreadyPresent | Medical | CMU already clears health bar and health icon damage-container state on refresh and deactivation, including its RMC HUD extension.
2304 | 716e5ace87e4c0d44015e767adfd413057f477a7 | Fix action tooltip warnings (#42361) | PortCandidate | Interactions | Two redundant action-tooltip localization calls can be removed independently after checking CMU metadata semantics.
2305 | 9338834b1b8d21c78b4159bc3b9086919fcf9f6c | Add admin logs for connecting/disconnecting players (#42363) | PortCandidate | GameTicking | Focused connection and disconnection admin logs add a durable GameTicker audit trail.
2306 | a92702e780c3052ad7708a463246a64d6eb9de45 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2307 | 4fafb55477b04eb12900068c1bae3f1d9ef524c5 | Predict BarSignBoundUserinterface (#42364) | Deferred | Interactions | Bar-sign UI prediction moves client visual and selection state into a shared system across five files.
2308 | 435b7d5cf89897476ee60995a14944c6ed8dd1a1 | Add the ability for station maps to track grids they are not on (#41248) | Deferred | GameTicking, Gamerules, Interactions | Tracking grids outside a station map crosses NukeOps rule state, pinpointer UI, shared components, prototypes, and assets.
2309 | 98647f1f0f1d2c7bcf79c3adfa30504fe8214108 | Admin: fixes description for "help osay" (#42368) | PortCandidate | — | Two-line admin help-text correction for the OOC command.
2310 | 71040149a192a01de8aa604b5b8913f77ad315ab | Automatic changelog update | Irrelevant | — | Generated changelog only.
2311 | 94071a63508ed4d187652bb60d444ccd027258dc | Fix GenpopLockerBoundUserInterface prediction (#42365) | PortCandidate | Interactions | Two focused GenPop prediction corrections use a predicted BUI message and preserve the acting entity for locking.
2312 | 06a962559adcf258d76b67f23d9a1c8d137f16c8 | Fix holywater locale string usage. (#42378) | Deferred | Chemistry | The corrected holywater label targets a chemistry-bottle layout absent locally and must follow the chemistry prototype migration.
2313 | c9ec5e81f0605649fbb13845715d49743fe7387e | Medical Cyborg Modules Rework. (#42123) | Deferred | Medical, Interactions | The medical cyborg module, recipes, research, tags, and migration rework crosses RMC borg and medical policy.
2314 | 11f308729d3f73bd6421207d86f6ea9d603df8c5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2315 | 360bfd6e1c9cdec8b4eeccdb4b4b380072448252 | Spray bottles with visible reagent contents (#42155) | Deferred | Chemistry, Interactions | Visible spray-bottle reagent contents span server behavior, shared state, prototypes, and 28 binary sprite states.
2316 | 45b3609b8a80a3a26d05e5ac93df6d76cfc8b6a0 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2317 | 7f4bc8f7d1055c2c1e58be2d29799fffccbd2717 | Reworks destruction Space Law to include Silicons (#42317) | Deferred | Gamerules | Space Law is server policy content and its silicon crimes must be reconciled with CMU rules.
2318 | d06b18a8f048e8b4b934781051f449b5fa8faf7b | Allow late join from arrivals to be considered for antagonist. (#39837) | Deferred | GameTicking, Gamerules | Late-arrival antagonist eligibility changes authoritative selection timing and arrivals state.
2319 | 84a21039d62d2804a37a55d460a14eba09b7b70c | Automatic changelog update | Irrelevant | — | Generated changelog only.
2320 | de10a3a948630fe491d464bd34289d040510dd10 | Allow the admin door remote to toggle overcharge (#42370) | PortCandidate | Interactions | One prototype flag lets the admin door remote use the existing electrification-overcharge mode.
2321 | de9d8334d1efb25bcf1e3aeaf4739c50b79af88e | Automatic changelog update | Irrelevant | — | Generated changelog only.
2322 | 4cd5d115bfe373e021458105859f46ce3db94b8c | Balance swing at Vestine  (#42302) | Deferred | Medical, Chemistry | Vestine reaction, botany mutation, reagent effect, and balance changes belong to the deferred stimulant chemistry cluster.
2323 | f3c40aa46ca8cc0b64e21d88eecec816f33191d7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2324 | 6cbd19adfa47c96c3eeecb3523a5cdff318705db | Lower hyperzine injector cost (#42383) | Deferred | Medical, Chemistry, Gamerules | The hyperzine injector price adjustment is only valid with the deferred stimulant rebalance.
2325 | f24e1dba620f5a0df90d8864239f7c908988905c | Automatic changelog update | Irrelevant | — | Generated changelog only.
2326 | 29c68e467af2471be20279a7d8f680baa8ba1ced | Add the Syndicate Delivery Console to the Nukie planet + target station maps (#42337) | Deferred | GameTicking, Gamerules | The generated Nukie planet map rewrite depends on the deferred Syndicate delivery-console feature.
2327 | ea131f73682417f12b69fc9eb5452d155e7a0182 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2328 | 4d16565c2aaac55e9ac088358039fd4cd711e3bb | Lower smuggler's satchel price to 1TC (#42381) | PortCandidate | Gamerules | Small uplink price adjustment, pending CMU economy-policy confirmation.
2329 | 69330e5752d023c743e9e0af3b86970c21017b32 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2330 | 04bda3ad5912ef25af1b3878dda1c86704a0d8cd | Role time tracking support for admins (#31776) | Deferred | GameTicking, Gamerules | Admin role-time tracking adds server lifecycle behavior, a CVar, and development configuration that need privacy and RMC policy review.
2331 | 738f55c45644e00a0a186be6009d0befac1e0cc5 | Adds EMP Resistance component, gives it to ninja suit and headset (#42334) | Deferred | Interactions, Gamerules | The new EMP resistance component and Ninja consumers begin a contract refined at index 2350.
2332 | 53607b8ca1d586ded6c478b3a19a23f3e27dbc81 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2333 | 418b2b70b0cade0d73b46d4c4230dfcbba6abced | Allow station tiles to be placed on solid ground and other platings. (#38898) | Deferred | Interactions, Physics | The 15-file tile-placement and RCD rewrite changes maps, explosions, tile history, CVars, prototypes, and integration tests.
2334 | 69d2ddd8bf2b10754d054196afd681d5c13262cd | Automatic changelog update | Irrelevant | — | Generated changelog only.
2335 | f0f08716098721cda30a6752a1d0b39c9332e60c | WYA to Where you at (#42350) | PortCandidate | Interactions | Two small accent replacements expand WYA to its intended phrase.
2336 | 4ae961babb4da730796c50bbca080a2275dab3af | A handful of typo fixes (#42396) | PortCandidate | Medical, Chemistry | Three isolated player-facing typo fixes across alerts, medicine, and uplink locale.
2337 | f8a6a7992891b3d60c0a5b229cfe8844da3cb3c7 | Buff throwing knives kit (#42391) | Deferred | Shooting, Interactions, Gamerules | Throwing-knife kit contents, damage, description, and balance need RMC combat and uplink-policy review.
2338 | d7230548605976c0819c1614b3295cbe7570a5ce | Automatic changelog update | Irrelevant | — | Generated changelog only.
2339 | a18fc337242c3f4a9e06facee39c6092edc94ec1 | Fix scram allowing you to bring someone along (#42393) | Deferred | Movement, Interactions, Physics | The one-line pull-detachment correction is retained, but CMU lacks the upstream ScramOnTrigger substrate from the earlier deferred teleport rewrite.
2340 | 4d1843f5e4757d171829a2f9a25aadbbf9bb4c8d | Automatic changelog update | Irrelevant | — | Generated changelog only.
2341 | c860502e66b4fb71b79e9053d12e7ccfc40a3417 | Viper High Capacity Ammo (#42392) | Deferred | Shooting, Gamerules | The Viper magazine, pistol, uplink, localization, and sprite rebalance needs RMC firearm and economy reconciliation.
2342 | 3cec0aa47602e7b84443d059db782ec4608b3d45 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2343 | 7540c8f152670f152da75b644bfb2faff979f88d | Pry open critical Borgs (#42319) | Deferred | Medical, Interactions | Prying open critical borgs adds a six-file mob-state lock-bypass feature crossing retained borg behavior.
2344 | 0b27da57f47fbd861be25c059cef6d17b648f2c2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2345 | c7e4f20f02871641bb5cc00da7dbc4d7fe3c0d12 | Fix tritium fires breaking conservation of mass (#41870) | Deferred | Chemistry, Physics | Tritium fire mass conservation changes core atmos constants and reaction math and begins a multi-commit reaction cluster.
2346 | ac1870a25f2c90e90e68c37d78336e16223020bc | Automatic changelog update | Irrelevant | — | Generated changelog only.
2347 | 60e172e12883fc10135a4cf09d42bdfcd0ba0026 | AirtightSystem Tests (#42190) | Deferred | Physics | The 600-line Airtight integration suite also changes server airtight APIs and must land with the later initialization optimization.
2348 | b5f0dd81fc177507b2867ea551cdda9096be229b | Increase trit-to-frezon ratio from 1:8 to 1:50 (#42400) | Deferred | Chemistry, Physics | The tritium-to-frezon ratio is a major atmos balance change tied to the reaction cluster.
2349 | 1fdc70aa3decfb3b8c2c8e47235da1bf2710477c | Automatic changelog update | Irrelevant | — | Generated changelog only.
2350 | 2399b61ca7721fd77615672cdd30a237e238ebbd | EmpResistance cleanup (#42402) | Deferred | Interactions, Gamerules | EMP resistance cleanup changes the new component contract and consumers introduced at index 2331.
2351 | 6cae5d9c4ae533f460088a09aa864fdeef851f53 | Fix TritiumFireReaction low fuel limiting behavior (#42407) | Deferred | Chemistry, Physics | The low-fuel TritiumFireReaction correction depends on the index-2345 conservation rewrite.
2352 | 0af56cefcb461b40890802269d14750789ad023f | Automatic changelog update | Irrelevant | — | Generated changelog only.
2353 | fb133494cc3bedf82e9b60a434574e9029e04244 | Decouple gibbing from the body system (#42405) | Deferred | Medical, Interactions, GameTicking | The 37-file gibbing extraction rewires body, blood, destructible, round-end, borg, magic, kitchen, and test behavior.
2354 | 9979a08225e084d3c44a85a29be1f481f4b566d8 | Maid uniform sprite change. (#38335) | PortCandidate | — | Self-contained maid and mini-maid uniform sprite refresh.
2355 | 7ebca1d8cce8b455b00240e379b01a726df0440e | Automatic changelog update | Irrelevant | — | Generated changelog only.
2356 | 91dd9f7be2dde8ecc10adb825d9be9ddf871eca1 | Add a target station map to the LoneOp shuttle (#42376) | Deferred | GameTicking, Gamerules | The generated Lone Operative shuttle map change belongs to retained Nuclear Operative map reconciliation.
2357 | 95496c8d2c205da96f2dcd67b47a75e7b92ad88c | Automatic changelog update | Irrelevant | — | Generated changelog only.
2358 | b5fb3d4bdb84349056b503446ddbf65407d1895d | Replace the Reach DoorRemoteAll with DoorRemoteCustom (#42385) | Deferred | Interactions | The generated Reach map access-remote replacement needs map and access-policy reconciliation.
2359 | b22063127851f61cbf9ac1c39f4b6c55782f0678 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2360 | e5ce73a4711b9b7182fddb892ce4cc2dcf36d254 | Xenoborgs now drop pieces of pinpointer (#42295) | Deferred | Interactions, Gamerules | The 14-file Xenoborg pinpointer-drop and repair feature crosses retained borg prototypes, recipes, tags, and assets.
2361 | 6bc617ca07b896baff8627098a7f4bfa38f0b3b9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2362 | acdeac6172b3c436f0609eee83573703d29821bf | Make lathes refund materials when recipe gets cancelled (#42416) | PortCandidate | Interactions | The focused server lathe refund path retains materials when current or queued recipes are cancelled, pending RMC accounting validation.
2363 | dc47295d24fa2d39053861b55ef5138d696348c1 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2364 | d857acfc078098dd09b0f28d47c13444161c530e | Fixed Containment Generators not updating pointlight correctly (#42289) | PortCandidate | Interactions, Physics | One visualizer hook refreshes containment-generator point lights whenever connection state changes.
2365 | 48cbd020a870f0fc90e2cfa3333fa5926c6fe58a | Automatic changelog update | Irrelevant | — | Generated changelog only.
2366 | 14b867dbe1f18db1f5f9bce63bc1bb8b9b230fd3 | allow shuttle to Scan for Objects while FTL is on cooldown (#42283) | PortCandidate | Movement, Interactions, Physics | A focused shuttle UI gate permits object scans during FTL cooldown without enabling FTL itself.
2367 | f702dc8f2d8e24feb30199d49d0c0b5cf7133043 | Atmos GasSpecificHeats in shared (#42136) | Deferred | Physics | Moving gas-specific heat state into shared Atmos systems changes eleven server, client, shared, benchmark, CVar, and test files.
2368 | 1f80b6a95d72ba199e805934f216ba13fff17040 | Fix TryAllReactionsTest reacting early and not checking priority (#42412) | Deferred | Chemistry | The reaction-priority integration-test fix depends on the deferred shared ReactionMixer architecture.
2369 | d6377862c1f8fa7539320a2229fc0486679d5564 | Reduce unnecessary `ComponentInit` work for airtight entities (#42390) | Deferred | Physics | The airtight initialization optimization changes component data and server startup behavior and should land with index-2347 tests.
2370 | e196d378415d9ad5d246ee783bce19145efafd62 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2371 | 4219bca74bf1859a31e15563b7b4ac910029973a | Put arrows on all the single-directional pipes (#42408) | PortCandidate | Physics | Binary pipe sprites and metadata add direction arrows without changing atmos simulation.
2372 | 5d929533fc8e7e1c055520ee49e0b1c650b65225 | Move artifact random spawners to entity table spawners. (#42422) | PortCandidate | Gamerules | One artifact random-spawner prototype migrates to the existing entity-table form after pool verification.
2373 | 75321710903fc0ff7cedc0a82746207d4b3d05e4 | Increase TEG power generation by 75% (#42421) | Deferred | Physics, Gamerules | A 75-percent TEG output increase is an atmos-power balance decision requiring CMU policy review.
2374 | 17997984ac749be6f5fe0d6f0fdd9aee75e4129f | Automatic changelog update | Irrelevant | — | Generated changelog only.
2375 | 07076a5a32f1e45913c669ee21db50c6a406122d | Cleanup warnings: CS0414 (#42429) | AlreadyPresent | Medical, GameTicking | The dependencies removed upstream are actively used by CMU's retained map-migration, body, and mind behavior, so the unused-field warnings do not exist locally.
2376 | aa8a61b6aefbdf2fe5246e31909fd882ae027258 | Make cancer mice actually hurt (#42298) | PortCandidate | Medical | Two prototype values restore the intended damage output of cancer mice.
2377 | 49204049940ecca352a439d548a20828eb1aa3a8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2378 | bd096a044b2b7871ab85f8f8786200422174dc25 | Make heavy xenoborg able to "swim" in space (#42415) | Deferred | Movement, Physics, Gamerules | Giving the heavy Xenoborg space-swimming movement must be reconciled with RMC borg mobility policy.
2379 | 241b0930bc8a0317009f49015ed4fe3e693ed861 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2380 | 499e9f9a0fa9b5387174cc91fa7c97c9437b2cf0 | Predict TransferAmountBoundUserInterface (#42358) | Deferred | Chemistry, Interactions | The transfer-amount BUI prediction rewrite moves state across five divergent chemistry client and shared files.
2381 | 5cda60f2f97610037e01f4504875244cc5d0a43c | Predict defibrillators and add an integration test for them (#41572) | Deferred | Medical, Interactions | Defibrillator prediction and its integration test rewrite central client, shared, and server medical behavior.
2382 | 4f997f2069c5625bf3b0de9e3cf6f0c71a3b9ac9 | Cryo pod UI (#41850) | Deferred | Medical, Chemistry, Interactions | The 17-file cryo-pod UI and analyzer refactor is a large medical presentation and state migration.
2383 | 766f429fd9a0604e5cc82d27ee829b27f542a541 | Make chemicals not react inside pills (and stomachs) (#41457) | Ported (CS-0233) | Medical, Chemistry | Standard and RMC pill-base food solutions are now non-reactive, preventing stored medicine mixtures from transforming inside pills while leaving metabolism after ingestion unchanged.
2384 | b723d7e49e2b52cc01a1fd6a630d392e162f8e00 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2385 | 28a4a548b615bc717021082e54c32c1dfb6a6b2a | Add integration test for drains (#41190) | Deferred | Chemistry, Interactions, Physics | The drain integration suite depends on the deferred index-2284 DrainSystem prediction migration.
2386 | 619672a089d5ee3e7943b755ed7385acf3a07c3f | Improved Health Examination Coloring (#38231) | PortCandidate | Medical | Health examination color markup can be reconciled independently against CMU damage groups.
2387 | 610881db82f5a8c723ab5c119484fd74b725c5e4 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2388 | fdeb5a736d0c32cc83cfef81bd01fccedab86f9c | Rebase vials to DrinkBase, closeable vials, mini vials (#36132) | Deferred | Medical, Chemistry, Interactions | The 37-file vial inheritance, closeability, mini-vial, cargo, recipe, borg, migration, and asset feature needs dedicated integration.
2389 | ab2a4ebd938c594b28aada2d70a81bbcbb70973d | Automatic changelog update | Irrelevant | — | Generated changelog only.
2390 | 84ca0ebe9cae20e1767f3fda7bf2c68572aa6980 | Add attribution to Tippy.rsi (#42346) | Irrelevant | — | Texture attribution metadata only.
2391 | c7e8bbbf873deb9e341facf73c443b0b39936f6f | Add Paper Centrifuge (#42040) | Deferred | Chemistry, Interactions | The paper centrifuge adds ReactionMixer behavior, construction, recipes, audio, prototypes, and assets and depends on deferred chemistry architecture.
2392 | cd6c521b37e23a7d36e0ed49971b72a87e35aa63 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2393 | 7d58e42ade391a61183145d271cb4e76b683bc22 | Fix RCD light spam, bypass of indestructible tiles and some plating fixes (#42432) | Deferred | Interactions, Physics | The RCD light-spam and indestructible-tile fixes depend on the broad tile-placement and RCD cluster from index 2333.
2394 | 8fb3e138a9cc0976ea7a9e657bb5d87a89bf08a6 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2395 | b14964398b7f80fa1710ca7c6a536f6bbe166b17 | Camera map (#39684) | Deferred | Interactions, Gamerules | The 12-file surveillance camera map feature changes UI, wire behavior, server visibility, shared state, and prototypes.
2396 | 6df3ed9682b2e545b77342ddd21a80d07f45d928 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2397 | 897a2d40bc2ec2ba595f6a60f64b12f0b5304010 | Add Mortar and Handheld Juicer (#42019) | Deferred | Chemistry, Interactions | The 42-file mortar and handheld-juicer feature rewrites grinder systems and adds recipes, audio, prototypes, and assets.
2398 | 57ac7bbe4f8b4dc5c1dcc4797c8cb5084e8e079e | Automatic changelog update | Irrelevant | — | Generated changelog only.
2399 | d2ac15c76f714144b6ffc583f87b3b097610fb0f | Fix flatpacker exploit ignoring board costs (#42445) | PortCandidate | Interactions | The five-file flatpacker cost validation closes a material exploit but must be adapted to CMU machine-board requirements.
~~~

# SS14 upstream inventory: wave 0002

Audit date: 2026-07-20

- Pinned baseline: `59633f6dc50e77dda8cefa344d87c7b01e06a810`
- Pinned target: `40ca2c7f90d11d27be5457d177c133f0947d1c08`
- Range: ordered first-parent commits, zero-based indices 0200 through 0399
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
0200 | 3ce7d37b14be208ae6f4f97d6d4d3b42dfe034d9 | bagel update (best med update) (#39187) | Deferred | — | Bagel map-only update; target-final contains many later map edits, so import and validate the final map rather than this intermediate revision.
0201 | 1afb37669d4c508f10aec73d496453314cc79178 | fix: don't apply Sleeping during prediction reset (#39061) | Ported (CS-0099) | Medical, GameTicking | Sleeping status application now skips prediction-state resets while preserving RMC's PainNumbness ordering.
0202 | ff7713eceaac2b9439528643f41c69ce4c243a8d | Admin logs for batteries UI (#39208) | Ported (CS-0107) | Interactions | Battery input and output breaker mutations now log the actor, target, and selected state.
0203 | a8db9df2812d06b60f4f61fccfad4f6c54f057a5 | Change potassium-water explosion scaling (#37924) | Deferred | Chemistry, Physics | Current has the old 0.5/100 scaling; target's 0.25, total-100, max-intensity-7 values are an explicit balance decision.
0204 | ae276eb237cf25f0c9adab0c4f52e11e5b943a68 | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0205 | d3cdae5a9271f1de3355e2e22b0aa423502c5230 | Change smoke/foam/explosion chemistry reaction order & energy transfer (#37915) | Deferred | Chemistry, Physics | Target-retained terminal-reaction and priority changes alter compound reaction outcomes; queue chemistry simulation coverage before porting.
0206 | f501b1b57fe1881fca3f908edcf0308595ee9a2a | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0207 | b4e81cb8f22b4856a42f3fe4f85e895a57bea02e | Admin Tool: Observe entities in an extra viewport (#36969) | Deferred | — | Current lacks the feature, but this is a 19-file admin UI/EUI addition requiring an isolated integration and permission review.
0208 | 236a3b2818a1a46a96055febce05b7356ac64f21 | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0209 | fc5d3dd4315d3192761b7df7ff1eb7966894532b | Fix pinpointer screen rotation solution 2 (#38657) | PortCandidate | — | Current pinpointer images are old; the changed image blobs remain in target-final, although its later-expanded meta.json should be imported atomically.
0210 | 2be968ccb1851a58dd5db858138b56d0c282f3d3 | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0211 | d0c104e4b095b834aa6344f336a45023f66a8e41 | Added Kill Tome (Death Note). (#39011) | Superseded | Medical, Interactions, Gamerules | Explicitly reverted upstream by 2a72e30e0ef917032d89974f00cadeb6a54a64e7; target-final has no Kill Tome implementation.
0212 | 444180c20dd4f758e2a9311a7e0ba1a65402a9fe | Optimizations from server profile (#38290) | Deferred | Movement, Physics, GameTicking | Mixed 14-file optimization and metrics bundle intersects RMC mover and physics-sensitive code; split and profile each retained optimization.
0213 | 21d47364c0ceb8745e36db4c53638b24d3c89fc1 | Some wallmount .yml cleanup (#34329) | Deferred | Interactions, Physics | Broad 36-file inheritance and placement cleanup needs target-final prototype import plus CMU map and prototype validation.
0214 | 43b3250e26959227c71917322bfedc16bcd67879 | Replace bad changelog entry (#39229) | Irrelevant | — | Changelog correction only.
0215 | 6aa278a709b0f2e0816457940919a0fe1c338114 | Update Credits (#39232) | Irrelevant | — | Fork-specific upstream credits snapshot should not replace CMU credits.
0216 | 540703588c48aa64804ac0a7eb46d24eade1e5fb | Make the cherry pit tiny (#39230) | Ported | Interactions | TrashCherryPit now has the retained Tiny item size.
0217 | 7852b52f85215ebb0ff1ba1a531c5384481a09a9 | Added utility belt function to scrap armor (#39233) | Superseded | Interactions | Explicitly reverted upstream by 978c51e73db8ca7f15a4300a3b2c14f47889ab4a; current CMU parent list already matches target-final.
0218 | faf15e7933ee24d51297ab6b0e9b689de3a957cd | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0219 | 8fdfb9deaeb985731c2375d473a84f37fe0dfeaf | Add admin logging to Wireless entertainment cameras (#39239) | Ported (CS-0108) | Interactions | Wireless entertainment camera renames now log the actor, camera, and new name.
0220 | 688c91b5979704a2a4feaf1b25f3450c3fd99aa9 | Add scaling filter option (Nearest/Bilinear) (#39111) | PortCandidate | — | Current has fixed viewport behavior; target retains the CVar, options control, localization, and viewport application.
0221 | a789341b2f1adaee49c16f8119399d55610179e9 | Hot Potato Sprite Fix (#39193) | PortCandidate | — | All three current RSI blobs differ while commit and target-final blobs match exactly.
0222 | 2ac9948ba0415691c09a755d03ad3070078a03b0 | Handle inventory template updating V2 (#39246) | Deferred | Interactions | Current only overlaps the serialization preparation; the inventory updater architecture remains old and intersects heavily divergent RMC inventory behavior.
0223 | 45cef10bad7938792b98ea0540eb0c95a9fe219b | update bagel (fix button connected to doors) (#39216) | Deferred | — | Intermediate Bagel map edit; port from target-final map state with map validation.
0224 | 5f52a3ae17d70c51afc7ccfc5a042b7da50ba0d0 | [Mald PR] Plushie sound 1984 (#39250) | Deferred | Interactions | Changes inherited plushie use and attack delays and sound parameters, including RMC descendants; requires content and interaction review.
0225 | 005203227b2cb7ca9fe7368055679c10a86e164b | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0226 | fedc355f20539e4bc955c58bac754b1043522c61 | fix foldable clothes not working while worn (#39257) | AlreadyPresent | Interactions | Current has non-null hide-layer sets and Count checks, plus an RMC-specific unfold path that clears and dirties empty hide layers.
0227 | c3cab577f6337f84cc1c616a97f968f937bcc70f | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0228 | 3c76b5a8aa7d15413eaa50f13fef0bca7a51d1e9 | rolebriefingcomponent bugfix (#39261) | AlreadyPresent | Gamerules | Current MakeTraitor already uses EnsureComp<RoleBriefingComponent> and updates the returned component.
0229 | 901cef43c96ce97c0ab6a43e312a8cb4fb619473 | last words error fix (#39245) | Ported (CS-0100) | Medical, Interactions | Last-words callbacks now return safely when the critical mob was deleted before dialog completion.
0230 | 8b104d30d5428682acf4edf15547b033625554e7 | allow janibelt to hold golden plunger (#39213) | Ported | Interactions | The janibelt whitelist now accepts the retained GoldenPlunger tag.
0231 | b77b533e1f5ced17b9ec8e46552352ba84e58f4f | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0232 | e2d96f1f4921d229ef92a3b6dc7850ce720b9748 | Make BoozeDispenserEmpty actually empty (#39067) | PortCandidate | Chemistry, Interactions | Current Empty prototypes inherit filled StorageFill data; target retains empty-as-base and filled-as-child inheritance.
0233 | b0825c102cec17285ceff01cf912e9e21ef5af18 | Added a network configurator to the Warden's locker. (#39254) | Ported | Interactions | The Warden locker fill now includes the retained NetworkConfigurator entry with CMU's surrounding fill structure preserved.
0234 | cb9b8c001dff54c2f9a4e839a8b58c0e83cc48a6 | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0235 | a52bf2a7c8ec52e23a8a27f6975c2376ffcfab26 | Convert a few debugging commands and the mapping setup command to LEC. (#38589) | PortCandidate | — | Current commands remain LocalizedCommands and one shells into showsubfloor; target retains LocalizedEntityCommands with direct injected systems.
0236 | 4c24db9d9c137471cb5be9453c18e89d14fa286c | Predict mimepowers (#38859) | PortCandidate | Interactions, GameTicking | Target retains the server-to-shared prediction migration and no RMC mime override was found; medium-risk prediction regression required.
0237 | 4a7576a7a63906240ad8492bfb5dcae95884cb8a | Several Vox Sprite Displacement and Layering Fixes (#39219) | Superseded | — | Target-final removed the original Vox prototype path and subsequently revised the displacement images; this intermediate asset patch is not the final import set.
0238 | 13ac52d21bd193c9093e08b6fb11754847aec91d | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0239 | 60cf54840f7cacdbac08fbcb894df539028b42f1 | Quartermaster job and ID icon change (#39259) | Superseded | — | Target keeps the job icon but later removes the standalone idquartermaster.png asset and changes RSI metadata; port only the target-final asset model.
0240 | 990940071b9fcb8747590833797cd00d9ca49d70 | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0241 | 9be68a6846f6c529e39ce0e51d6d15d107f892c1 | Fix a logic error in Protectedgridsystem (#39271) | Ported (CS-0097) | Interactions, Physics | Protected grids now allow edits inside their captured footprint and cancel edits outside it.
0242 | 3f41b47d2e478dfd0f402b9e4e019f0d0a87872b | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0243 | d4e77423caf57cc8e3bf34ef4912bc4a467e6c66 | Make RemoveReagent return a FixedPoint2 (#39266) | Ported (CS-0102) | Chemistry | RemoveReagent now returns the actual removed quantity while existing RMC callers may continue ignoring the result.
0244 | 0606ed585140c2e23b860421f7d5e2fb7bd18f1e | Retry of Advanced Chem Tweaks (#38811) | Deferred | Medical, Chemistry | Explicit medicine recipe and effect rebalance; later upstream chemistry changes should be reconciled before choosing CMU values.
0245 | 2f64e105d489bfe92e8837a8251e0db693e13749 | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0246 | f6475bd26419cd46a7eb3fe553ac0262f15f2909 | EntityEffectConditions changed to be inclusive of min/max (#36289) | Ported (CS-0103) | Medical, Chemistry | Entity-effect conditions now include exact minimum and maximum boundaries for temperature, damage, and hunger checks.
0247 | a8b65f2da762139aeb359a43eec33863dce43cd5 | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0248 | 68ba22548d7a77febef12eebd3b79ac3e13225ea | Predict GlueSystem (#39079) | Deferred | Interactions, Physics, GameTicking | Large server-to-shared migration also networks UnremoveableComponent, which several RMC attachment, parasite, powerloader, and gun systems manipulate.
0249 | 271e271cc92e7e5336dbf13e17f2d33f523566fc | Predict passive welding fuel consumption (#38876) | Deferred | Chemistry, Interactions, GameTicking | RMC repair, barricade, vehicle, sentry, and tool systems heavily depend on SharedToolSystem and WelderComponent; integrate as a dedicated core port.
0250 | e2b08dba1fb68fa91e23e9b50cc3cde2d36d1227 | Cleanup of scurret/role.ftl (#39281) | PortCandidate | — | Target retains the locale relocation and deletion while current still has the older split strings.
0251 | 9b0a17174397893cfd2ad5b988e79d72f0f113f2 | New recipe: Cotton Cakes (#39222) | Deferred | Chemistry | Eleven-file recipe, prototype, localization, and asset feature should be imported and visually and behaviorally validated as a content unit.
0252 | c2aede7963472ed499ce51735a80d4dbf3b41d31 | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0253 | f3aa14200b7d1310fdca90d04cb9986af898bb67 | Change the description of barefoot drink. (#39285) | PortCandidate | — | Target retains the corrected user-facing description; current locale remains old.
0254 | 31773e64f428c570a05c05f883123863bdd767c4 | Adds Wizard's Den (Replaces Wizard Shuttle) (#37701) | Deferred | Gamerules | Large map and content replacement requires an explicit CMU wizard-mode and map-integration decision.
0255 | c3374d86e477acc5e63021dfb4f61ef60661d55c | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0256 | 66f64bc9523228e338071dc530926a4ff99dac92 | Allow EmoteSoundsPrototype to have parents (#38890) | AlreadyPresent | Interactions | Current already implements IInheritingPrototype with parents and abstract fields and additionally preserves RMC AlwaysPushInheritance behavior.
0257 | 623ea3dd63ae2c1196c2723a9f3dbaec3e3ccf6b | Make VendingMachineInventoryEntry a data definition for post-init savegrid (#38406) | Ported (CS-0104) | Interactions | Vending inventory entries now participate in data-definition serialization for post-initialization grid saves.
0258 | 12d2ed6cb6756baa22ffec71acd9f6c21070c78b | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0259 | 45f6c1db73ab8233faa95a99da2012c6b2d8a165 | Exo - Major Sec changes, and more! (#39295) | Deferred | — | Twenty-eight-file, roughly 28k-line map and asset change; use target-final Exo and conduct full map validation.
0260 | 142b57599cc4f5b9efb25efeedc25c9f661168ff | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0261 | 392f4ea8f6080fed9cd5af76ed3de529263ed7f6 | Fix variantize command not respecting tile rotation (#39314) | Ported (CS-0098) | Physics | Variantize now preserves each tile's rotation and mirroring while selecting a new variant.
0262 | a942ce21931804a02fe442723df010bd7a4f41d9 | Renames slugcat jelly-donuts to scurret jelly-donuts (#39308) | PortCandidate | — | Current retains the old prototype ID and mapped references; target's migration preserves old-map compatibility while renaming the entity.
0263 | d805704a1f176707923d53d1db26ff869f4b2a51 | Predict EmitterSystem ExamineEvent and GetVerbsEvent (#39318) | PortCandidate | Shooting, Interactions, GameTicking | Target retains shared predicted verbs and examine behavior and networked BoltType; no RMC emitter override was found, but component-state regression is required.
0264 | a99615992a02b3a17e1a6a1a14b193c4c9f19d34 | Predict ExamineEvent for CryoPodSystem. (#39322) | PortCandidate | Medical, Interactions, GameTicking | Current examination remains server-only; target retains the same handler in SharedCryoPodSystem with no direct RMC override.
0265 | c376e695184ec53f3d0a7e0966aad1bfa2eee013 | Fix tabletop grids rarely spawning on top of another (#39327) | Ported (CS-0101) | Physics | Tabletop sessions now start at the first one-based spiral coordinate and use the corrected ring calculation, avoiding overlapping grids.
0266 | 0c446e05b3ee3b7bade08da7e0035a84a026d50b | Automatic changelog update | Irrelevant | — | Upstream automatic changelog only.
0267 | fbed76e0671cf51b2a18232893a4f819d7df610e | Staging to master (#39328) | Irrelevant | — | Merge commit has no first-parent tree delta.
0268 | c444db0e58d5d4ab200fc83cc2d05324c05b75b1 | Add test of `StaminaComponent` crit vs animation thresholds (#39249) | Irrelevant | Medical | Test-only commit introduces no runtime behavior to port.
0269 | e307fd69b0153f0172f77e5003c4446077236a6f | HumanoidCharacterProfileFix (#39333) | Ported (CS-0089) | Gamerules | Humanoid profile equality now uses typed memberwise comparison without recursive object equality, including RMC-specific profile fields.
0270 | a476abe772cdd6853ac605294642619a46827907 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0271 | e82dc13bf936f700e588e683240cc48abfd27288 | Fix StoreTests EventBus usage (#38489) | Irrelevant | Interactions | Test-infrastructure-only change has no production delta.
0272 | 90f4f365dfa99209aa76b1d4b1daa737c80907d4 | Don't purge note buffer when starting/switching MIDI songs (#39335) | Ported (CS-0094) | Interactions | Opening or switching a MIDI song now preserves pending cleanup events instead of clearing the note buffer prematurely.
0273 | b86b0c7fe828fb69cccfd784de26b07bb729ac0c | Berry Delight (#38881) | PortCandidate | Chemistry, Interactions | The retained cake, recipes, flavors, spawners, localization, and sprites are absent and can be adapted to CMU's pre-ingestion-refactor prototypes.
0274 | c7efdb8be65dc15f5ca0c43d016dba1e9f09107e | Automatic changelog update | Irrelevant | — | Generated changelog only.
0275 | 615f63e13bb03f14befba9866169d9e4958cf28e | Fix horizontal space men in replays (#39338) | Ported (CS-0095) | Movement | Missing replay appearance state now defaults rotation visuals to the neutral vertical orientation.
0276 | 21eb662377ed0d267744287c870b0c9916444211 | Fix ActionsSystem.IsCooldownActive always returning false if curTime is null (#39329) | AlreadyPresent (CS-0025) | Interactions, GameTicking | Ported and documented as CS-0025 with regression coverage.
0277 | 6c9368dc602dc944238d8dbc8f842f4a9144ef02 | Make dirt non-compressible (#39220) | Deferred | — | Target retains the RSI compression flag, but CMU still represents this dirt tile as an older PNG asset layout rather than the target RSI.
0278 | c538d7fb2b0dbdbddaed3df7d079e8a438c5c56e | Predict anomaly synchronizer (#39321) | Deferred | Interactions, GameTicking | The large server-to-shared prediction migration is absent and should be reconciled with later target-final anomaly APIs.
0279 | 9d3edeb6413e87a98322ccaef4c39704fe17ca8e | parrotMemory is onGetVerbs now in shared (#39341) | Deferred | Interactions | This five-file server/client-to-shared ownership migration needs the later parrot state and verb chain audited together.
0280 | 06581a004541e12caabe49164fd62533d8ab47ee | Fix rotate verbs not being predicted (#38165) | Deferred | Movement, Interactions | The retained 492-line rotatable and flippable prediction migration intersects CMU's older rotation systems and RMC rotation consumers.
0281 | 8c317838555705b1ca005e6d124cbfb8a4681b70 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0282 | 577c10d8584da41fff321a5cbc595643b43aaf84 | Update Credits (#39343) | Irrelevant | — | Credits metadata only.
0283 | bd3d5cff19d962f13e6763d5c0e64b4cccecadd8 | Advanced Clowning Module (#35797) | Deferred | Interactions | This retained borg module, projector, cannon, research, recipe, and asset bundle needs reconciliation with fork borg content.
0284 | 18b2b958b2566d18dc4ffdd3880ff104b439f91b | change bagel genpop biocube fabricator into biogenerator (#39313) | Superseded | — | Numerous later Bagel revisions make this intermediate generated-map snapshot unsuitable for direct import.
0285 | bf6581972d8c90e0b8c51d2953458bdaa732e827 | Hardsuit helmet text fix + CBurn Vox Fix (#39345) | PortCandidate | — | Cosmetic comments are unnecessary, but CMU's CBURN helmet still lacks the retained Vox clothing visual states.
0286 | 819e342a4f4dfef0953ebfc76a030a196765ce0c | Localize Refund Button (#39346) | Ported (CS-0096) | Interactions | The store refund control now resolves its retained Fluent key instead of hardcoding English.
0287 | 777e89ab3edeac9386d243554ad365df13eadcdc | Make wallmount screen, telescreen, and signal timer destructible (#39340) | PortCandidate | Physics, Interactions | CMU's restructured telescreen is already destructible, but its standard screen and signal timer still lack the retained damage behavior.
0288 | 2c40a950f788058d8665de9fc68454d4388d33e2 | Trigger Refactor (#39034) | Deferred | Shooting, Medical, Chemistry, Interactions, Physics, GameTicking, Gamerules | This 7,925-line architectural migration touches nearly every trigger consumer and heavily overlaps RMC explosives, weapons, and interaction behavior.
0289 | 61d13ce40d07a2d518bacdbf02a064b6c96583f7 | Stable to master (#39352) | Irrelevant | — | Merge-only history boundary has no direct portable delta.
0290 | 53e64c3a24a541c3f3bfaa99e3ce5d2c6ec1f2b0 | Xenoborgs part 4 (#36935) | Deferred | Shooting, Interactions, Gamerules | This 31-file feature depends on the preceding Xenoborg series and conflicts conceptually with RMC silicon and faction content.
0291 | 6d50fb03d6096184600682236821c4324aadfa2c | fix: auto-update mailing unit + gas canister UIs on state (#39289) | Deferred | Interactions, GameTicking | CMU already has both UI state-generation fixes but retains the removed follower after-state handler, so the mixed commit needs a focused follower-state audit.
0292 | 93a03111a5c7169510da76d297eaaa3d4189c8f9 | Updated syndicate throwing knives description (#39374) | PortCandidate | Shooting | The armor-bypass explanation is absent, but the port should use target-final's later eight-knife wording rather than this commit's count of four.
0293 | f6737d4a574f6dcadfe137f2e6a7ee605e2e5374 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0294 | e996fb62f1fd9ae434e60d3e783c24e147f0768e | Revert "Fix bug with pipe color" (#39135) | AlreadyPresent | Physics, Interactions | CMU never carried the reverted shared pipe-color implementation and already matches the target-final server-owned behavior.
0295 | 312f81d58ad5e11e2a7510b2bcc30e1a49390159 | Move `HeadstandComponent` to Shared (#39377) | PortCandidate | Interactions | CMU retains separate client and server derived components while target-final uses one networked shared component.
0296 | 14c2a1fa9266a3dcd692eb7fdbd44fb76b364d4a | Fix head mappers codeowners (#39378) | Irrelevant | — | Repository ownership metadata only.
0297 | a80b31e1cd1543580853fb0b72dfa19b3ec9ea2c | Fix vox inhand displacements (#38507) | PortCandidate | — | The corrected binary displacement asset is retained upstream and CMU's asset hash differs.
0298 | 32ef32d5a0a761c9a050162facc0e3cab455d6fa | Add Offset Canes + Trinket Canes Group (#39272) | Deferred | Interactions | This 35-file prototype and sprite bundle should be reviewed as an asset-heavy content import.
0299 | 1d07b77707aaf8f77fd09dbedff774a4745cfdc3 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0300 | 2c933c8de77df74bc9a1bce617db18512a923016 | add: air alarm scrubber select all gases button (#39296) | PortCandidate | Physics, Interactions | Target-final retains the select-all and deselect-all controls while CMU lacks both UI actions and localization.
0301 | ff97512a6d32a76acf0345ed9c45f58a4b990e17 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0302 | 1599a6b2713ec8824d81a96c432ff4b59fa2a5c1 | Fix ATS Anchor (#39389) | Ported (CS-0093) | Physics | The trading outpost now uses the retained indestructible station anchor at the affected map entity.
0303 | 96d25402c7ee9a5f10f60bd3dfb006815792a0a9 | fix: hide timer trigger's cycle time verb if DelayOptions is empty (#39388) | Ported (CS-0091) | Interactions, GameTicking | Timer triggers no longer expose or execute delay cycling when their option list is null, empty, or singular.
0304 | 75748153a155ef47fe7ee1084cab57be9e0505bd | Automatic changelog update | Irrelevant | — | Generated changelog only.
0305 | a2c9612e29d270197bf9045d452a56e7c739f8b1 | Removes ItemToggle from Cryo Pods to prevent a latent event ordering bug (#39197) | Ported (CS-0092) | Medical, Interactions | The standard cryopod no longer carries the conflicting ItemToggle component.
0306 | 36967f3e7ddb6c76e6638c07aa58d84910d710cb | remove space from Sleeping Carp.png (#39369) | PortCandidate | — | Target-final retains the space-free state and filename while CMU still has `Sleeping Carp`.
0307 | d7f8614c3598b270a58c04725906238a25f0dabc | localization support to air alarms, wire panels and more (#39307) | Deferred | Physics, Interactions | This 13-file UI localization conversion should be reconciled with later target-final UI and profile-editor changes as one batch.
0308 | 47bddb70b156a4e5ceebf77b2df9185631efb15d | Merge Stable into Master (#39404) | Irrelevant | — | Merge-only history boundary has no direct portable delta.
0309 | d4c025567a866c5ce7dc1fb51f6d4179e7099358 | Predict warp point location examines. (#39402) | PortCandidate | Interactions | CMU retains the server-only warp examine system and target-final retains the small move to shared ownership.
0310 | 5df467c9c6c2376aa4629070d4d355a891c93482 | Reduced SalvageStructureComponent to atoms. (#39400) | PortCandidate | Interactions, Gamerules | The obsolete component and examine text remain in CMU, have no RMC consumers, and are absent target-final.
0311 | 7de5002123b4e5f03f4e621c241d659a13e312f1 | Predict Nav Beacon Examine (#39408) | PortCandidate | Interactions | CMU still performs configurable beacon examination server-side and has no RMC override blocking the retained shared subscription.
0312 | 4a466c5dbe5885c9d80decadc290725581bff4e4 | Add guard to unbuckling to help it to not act upon terminating entities (#39410) | Ported (CS-0090) | Medical, Interactions | Bed unbuckling now skips wake and action mutation for terminating entities while still removing the bed's healing marker.
0313 | 87b0ec090f4f45bf286860c07060adaaf3e05bc5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0314 | 2a4f36422b10c973f9eac25d9db89f7b049560db | fix: properly respect AllowRepeatedMorphs (#39411) | PortCandidate | Interactions, Gamerules | CMU checks the incoming morph instead of the active morph and lacks the retained administrator override field.
0315 | 63b2979e73c0ed50eaede402ab438b66737f9b2c | Predict cryopods (#39385) | Deferred | Medical, Interactions, GameTicking | The 530-line server/client-to-shared migration must be reconciled with target-final cryogenic APIs and CMU's separate RMC cryostorage integration.
0316 | 053c5f64a018f0df3edb5f5129b55de3f3db1a10 | feat: properly perform predicted porta pottys (fix toilet prediction) (#39394) | Deferred | Interactions | This five-file prediction migration changes ownership, component state, prototypes, and interaction flow and should follow broader prediction prerequisites.
0317 | 92d6e7904008a4053f61fdd3bc4783ea6c3ddbff | Remove redundant return statement in InventoryUIController (#39381) | Irrelevant | Interactions | Non-behavioral cleanup only.
0318 | 983cebb69d9c555a466ce6ad20bfd01123dd767f | Update attributions for lightning audiofiles (#39395) | Irrelevant | — | Attribution metadata only.
0319 | 99336a33fbc7d5fa4ac3ca832d4dbede7d3a81b5 | Predict PickRandom verb (#39326) | PortCandidate | Interactions | The retained two-file move to shared ownership is absent, and no RMC system overrides the standard pick-random component.
0320 | e122e02c86f60afcebc410c70e4061d6e44a055e | Adds infinite debug power APC, substation, SMES (#39317) | PortCandidate | Physics | Target-final's empty and auto-recharging debug power prototypes are absent from CMU.
0321 | 5181219f89711c30e9c3f6f688e1ae6ab125fcdc | Status effects disable light occluding (1-line PR) (#39418) | Deferred | Medical, Physics | The retained one-line fix depends on the unported `StatusEffectNew` container architecture.
0322 | 3d0a506f6dc596caf9583d7e5cb8b84fca17986c | MessyDrinker for dogs (#38852) | Superseded | Chemistry, Interactions | Its server-side drink-event implementation was immediately replaced by the unified ingestion system and later moved fully shared.
0323 | d16e13e13c9fa4da9608e4ef78fc6386e650c719 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0324 | 3996b35606eb6c7f0ef14cd91dcb662dda5ffc84 | Predict EMP Examine (#39419) | PortCandidate | Physics, Interactions | CMU still subscribes server-side while target-final retains the examine subscription in SharedEmpSystem.
0325 | ca18576625ec149a5b9bc444b35e963653dac23d | Predict base and damage examines of cartridge ammo. (#39401) | PortCandidate | Shooting, Interactions | Shared examine ownership is absent; porting must preserve CMU's later MarkSpentAsTrash cartridge field and RMC ammunition prototypes.
0326 | c4016b97c5f4df1877ff63246a67a99af44a717c | fix DoAfter DistanceThreshold (#39276) | Ported (CS-0105) | Movement, Interactions | DoAfter now distinguishes the default 1.5-tile threshold from an explicit null that disables distance cancellation.
0327 | 8c181eb51ae128456146e9d5d50599a05da778fd | Update RT to 266.0.0 (#39421) | Irrelevant | — | RobustToolbox is independently rebased beyond this engine point and is outside content-edit scope.
0328 | 52a886e70e32ac51b9f162dfe3f6948e22cd8b59 | Automatic changelog update | Irrelevant | — | Generated administrative changelog only.
0329 | 8ef212a3382142e99cf2e3576f432e2906b4f6ff | convert dwarfs to use ScaleVisualsComponent (#39422) | Superseded | Movement | Target-final later replaced dwarf sprite scaling with displacement maps and removed this entity-prototype layout during Nubody.
0330 | 49c4aab4896c90c492f5436e0489e7b39cc38321 | Move solution examine subscription from DrinkComponent to ExaminableSolutionComponent (#39362) | Deferred | Chemistry, Interactions | The retained behavior spans 25 files and must be taken from target-final after the later solution and ingestion refactors.
0331 | 02382045ab6928572a5bc1ae3df7bac6ee4bda90 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0332 | 91854e077624e19c698268b028a0dd4bd706121e | Debody Food and Drink Systems, Combine Food and Drink into One System. (#39031) | Deferred | Medical, Chemistry, Interactions, GameTicking | This 3,163-line domain refactor intersects RMC ingestibles, metabolism, mobs, inventory relays, and many later target-final nutrition changes.
0333 | 4821bff9415bf027fdde9f9149f629a7bfe6aca1 | Fun with cardboard! (#37363) | Deferred | Shooting, Physics, Interactions | The retained 83-file feature adds a collision group plus weapons, armor, construction graphs, audio, and assets requiring a dedicated compatibility batch.
0334 | 96dcfa9b9427beb3873828cc994c89abbb8ea7cb | Automatic changelog update | Irrelevant | — | Generated upstream changelog only.
0335 | fdf39dbffb8573dfe3014f0e55eb3879142ee203 | add scale:multiplyvector toolshed command (#39424) | Deferred | — | Retained upstream, but CMU lacks the base ScaleCommand and toolshed command being extended.
0336 | ffccef2358100a88ad476adee8655a21bea9b6b4 | Automatic changelog update | Irrelevant | — | Generated admin changelog only.
0337 | 2e0b11ea51aec184c0f1db40108fe32e261e8e46 | fix repeated scale visuals removal/ensuring (#39432) | AlreadyPresent | — | CMU ResetScale already removes `ScaleVisuals.Scale` appearance data before raising the reset event.
0338 | 9872a28d7f9c877ac3368be528cb11605b444636 | Miscellaneous Body Decoupling (#38958) | Deferred | Medical, Interactions, Physics | Broad body and mob-state decoupling across brain, storage, disposal, pricing, butchering, and magic needs an RMC-specific body audit.
0339 | 534553dddfb0628b41cf955135d50769a7c35851 | Turn some implants into triggers (#39364) | Deferred | Interactions, Physics | Large implant, forensics, teleport, and cuff migration assumes the later shared trigger architecture absent from CMU.
0340 | f76e3b63b73adeb461b095c73222d072379dff06 | Changeling devour and transform  (#34002) | Deferred | Movement, Medical, Interactions, Gamerules | Major antag, cloning, identity, storage, movement, and UI feature absent from RMC; requires a dedicated feature port.
0341 | 6b6bb2e319f63eec70c652c794a3b292400f1d29 | Fix inventory flickering and missing InventoryTemplateUpdated event (#39379) | PortCandidate | Movement, Interactions | CMU already has client initialization and the update event but still dirties InventoryComponent after equip and unequip; target-final omits those redundant dirties.
0342 | dc3eb188cd61f60a41d09467b4ce4dc87aa5f5a9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0343 | 66daa1e6db3cd4211d0c2d4f6cbd64dd81fc9458 | Fix showvalue Ui for melee weapons (#38703) | PortCandidate | — | Retained target-final melee-stat display, localization, and wielded and structural damage reporting are absent in CMU.
0344 | 88a86be5004fcbd4076ff7a6fae0992d44b37641 | docs: update comment on config saving in tests (#39438) | Irrelevant | — | Comment-only clarification.
0345 | 556097eed4ab312c2428b6c08ed7c68e8b9da971 | Ingestion Bugfixes (#39436) | AlreadyPresent | Interactions | CMU's older FoodSystem avoids digestibility popups during verb probing, and OpenableSystem already cancels transfer only while closed.
0346 | 864fee5bd0c9c2402cab08b75bb8e28bc13378e5 | Bloonion mutation  (#33375) | Deferred | Chemistry, Interactions, Physics | New hydroponics, reagent, and explosive content plus binary assets needs prototype and balance reconciliation.
0347 | 15e024fae9a4b85683cc32b775830187035dabb8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0348 | 5444908a79265dab3f87c07e8cc1a01eea01d69a | Fix title2.ogg attribution (#39435) | Irrelevant | — | Attribution metadata only.
0349 | 7e676a03b6c0803142affda8386f6599599d1ed3 | Resized baseball bats to be more realistic (#38392) | Deferred | — | Sprite and item-size content change requires RMC inventory and art review.
0350 | ff0d45d8b0a6ea84a1a50e70368aa3f4c1b7b8e0 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0351 | 2abe4a8a0244b8b514c9c1c908d11c82d2e040fe | Fix Ingestion Localization Pop-ups (#39437) | Deferred | Interactions | Observer and self popup split is retained, but target uses EdiblePrototype and IngestionSystem while CMU still uses FoodComponent and FoodSystem.
0352 | 9b5d2ff11b8f19fafbf97d6ceab238028ca6dfeb | place stored changeling identities next to each other (#39452) | Deferred | Physics, Gamerules | Performance workaround depends on the absent changeling identity-storage feature.
0353 | ff5ce315f9d58f46da739330a9ccbc2c42a1b237 | Fix changeling typing indicator (#39454) | PortCandidate | — | TypingIndicator state generation is absent; RMC species define specialized indicators, so base inheritance needs validation.
0354 | 5eb9dc2475909ebc7f9887dc1642ffa1ba3207df | give paused maps from polymorph and cryostorage a name (#39453) | PortCandidate | GameTicking | CMU has unnamed paused polymorph and cryostorage maps; the changeling-specific portion should be omitted until that feature exists.
0355 | 9528fc4e2604554715a645469aa1b19a2ddf6b31 | Automatic changelog update | Irrelevant | — | Generated admin changelog only.
0356 | 3638b2f44e52dbe4e8c20812a9ea98a98b9a9c04 | fixes items with complex shapes failing to insert sometimes (#38896) | AlreadyPresent | Interactions | RMC storage placement hardcodes `ItemStorageLocation(Angle.Zero, ...)`, already providing the target behavior despite a stale unused start-angle calculation.
0357 | 6bb7610110abd9f88134a690c864e09e9b797056 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0358 | c32ee100a144092a9af0a2dff6e066a4730199c3 | Add changeling briefing sound (#39465) | Deferred | Gamerules | Audio and prototype addition depends on absent changeling gamerules.
0359 | 3d9e1f64a93b7937dccb88c780941f5f6a60cdbc | Disable changeling fixture cloning (#39467) | Deferred | Physics, Gamerules | Clone whitelist adjustment is specific to the absent changeling transform flow.
0360 | 1374ceea4758db71c7c3cbff9d4b128f38662b82 | Move some Station methods into shared (#38976) | Deferred | Physics, GameTicking, Gamerules | Large station API and component relocation touches many RMC rule and shuttle consumers.
0361 | 1d21e133602fefb5e49ba12cf2d335ec552bcfdf | make objectives use yml defined mind filters (#36030) | Deferred | Gamerules | Major objective and mind-filter framework replacement requires RMC antag and role reconciliation.
0362 | c3555af82104c2258d3830c65ff498d06041be29 | Sentry turrets - Part 8: AI notifications (#35277) | Deferred | Shooting, Interactions | New turret and AI notification framework and prototypes need the preceding sentry feature chain.
0363 | 1e3cf38c2102c14cbacfccb78d5191618164d89c | Automatic changelog update | Irrelevant | — | Generated changelog only.
0364 | ca4f6d5e8bfa82d2de2b34c8b5b460093654715f | Starting glasses for Captain and HoP (#35531) | Irrelevant | — | SS14 job-loadout balance is not applicable to RMC roles.
0365 | b60df574a6ae29c54e0a37b31606268769a8f35d | Automatic changelog update | Irrelevant | — | Generated changelogs only.
0366 | 49fe34f78a6cd05d6201cf9c21a61c34c42f30ad | fix: fix emote wheel icons (#39481) | PortCandidate | — | CMU still addresses RSI states as standalone PNG textures; target-final uses RSI sprite specifiers.
0367 | b38aba78125c4ab1714da39b279e339997401b75 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0368 | 5cf8662f3c0f36289cd175fa128a3022d20801f2 | Remove NamesGolem (#39478) | PortCandidate | — | Target-final removes the dataset; CMU references it only from its own now-unused prototype and locale files.
0369 | 2b8145ce8772e57478151a40f1838f52ff228ce6 | Cleanup Base food and drink a little (#39485) | Irrelevant | — | YAML field-order cleanup with no behavior change.
0370 | ce7b7c1adfa15fdfca45b5e24cd1405dd242b675 | Fix Whoopie Cushions from lagging the game. (#39194) | PortCandidate | Movement, Physics, Interactions | Retained zero-duration status guard and no-op friction suppression are absent from CMU's older status and slippery implementation.
0371 | 168186f75b41b30a5934087025d5ec178314c643 | Fix bar and base signs (#39487) | AlreadyPresent | Physics, Interactions | CMU's older hierarchy already gives base signs non-colliding physics and bar signs their explicit wall, power, and machine behavior.
0372 | 6dceb7b8e0aeb9bd6ad3db4fe6aba6b4917fdb64 | Merge Stable into Master (#39489) | Superseded | Movement, Shooting, Medical, Chemistry, Interactions, Physics, GameTicking, Gamerules | Huge aggregate merge, including forbidden RobustToolbox movement; constituent retained content is classified individually.
0373 | 68eb43f0d81ddb84bc89b4302cb6a180f5af079e | fix mind role filter (#39499) | Deferred | Gamerules | One-line inversion is required only after the absent YAML mind-filter framework from 0361.
0374 | 450ff99bacf96b732872f203fe80b55a2a8701a0 | Fix: Water Bottles Verb Priority and Prediction (#39482) | PortCandidate | Interactions | Target-final priority and predicted closed-state behavior are absent; requires adaptation to CMU's FoodSystem-era blocker layout.
0375 | a4e5d1b211e6f30a6facf031122013f1be14bf7c | Network StationMember properly (#39509) | PortCandidate | Physics, Gamerules | CMU StationMember remains manually networked without generated state and AddGridToStation does not dirty it.
0376 | 3f9d303cfc33d929a9b82a6992214e25bbffa774 | Mapping - Box station - Tie the RD's disposal bin to the disposals system. (#39507) | Irrelevant | — | SS14 Box map is outside RMC map usage.
0377 | 3ffa3ea9d462e4f9b1fbc089b506e2af5f38d505 | Moth displacement map fixes (#39174) | Deferred | — | Binary species displacement assets need comparison with RMC's customized moth art.
0378 | 23e2f997a96434317d5b008b8f367732c45ea1bc | Automatic changelog update | Irrelevant | — | Generated changelog only.
0379 | 4e29107c89e4ba80efa6fa83c415dd75a99fa0bc | Update Credits (#39512) | Irrelevant | — | Upstream credit snapshot only.
0380 | 55335cce0f3bcd166ee3ac095967b635ed5ea3b7 | Crawling Fixes 1: Dragons and Borgs can't do the worm. (#39084) | Deferred | Movement, Shooting, Interactions, Physics | Large crawling, knockdown, and stun-on-collision rewrite conflicts with RMC combat, xeno, and movement behavior.
0381 | 80299e863a8baf0266eb6e85cc270ab293e49d37 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0382 | 80375370f8572aee61e1db536f5e5adb4ddbe93c | Add voice locks to various hidden syndicate items (#39310) | Deferred | Interactions | Broad secret-lock, trigger, and UI migration assumes later shared trigger and lock architecture.
0383 | 2aca7f62dc611e6d4b0e6f2a310118644043e3cc | Automatic changelog update | Irrelevant | — | Generated changelog only.
0384 | 7825d30562bb20a589849f1309713a39d8796718 | Fire stacks trigger (#39530) | Superseded | Interactions | Immediately renamed and expanded by 0398; do not port the intermediate FlameStack types.
0385 | 458e2d222c4c9cb133a46f59df0346b49c038b5a | Status Effect Alerts and Time Bugfixes (#39529) | PortCandidate | Movement, Medical, Interactions, GameTicking | Target-final end-time semantics are absent; CMU uses the older SharedStatusEffectsSystem, requiring a localized adaptation.
0386 | 46a0cd9057810e0eefdb617285eabeb880fa5ba3 | Adds rare Hamlet variant: Fragile Hamlet (#39531) | PortCandidate | — | Small retained YAML feature; CMU's older trigger systems already provide all referenced component types.
0387 | 936831bfe0456d249a2146f316582d83872cd9b9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0388 | ad5fe5678c5415e5d3fbbce9e56871324058d578 | Trigger on round end (#39545) | Deferred | Interactions, Gamerules | CMU still has the old server Explosion TriggerSystem and lacks BaseTriggerOnXComponent and shared trigger systems.
0389 | 3654fcf5ddb194aa749dd6ab9b324a8934e0f70f | fix: reform dionas via SpawnNextToOrDrop (#39505) | Ported (CS-0106) | Interactions, Physics | Diona reform now uses safe adjacent-or-drop placement before mind transfer and old-body deletion.
0390 | 8d0a174b43d5efc0962c929aeeb835272671c556 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0391 | d7295d1ae4644fb129ac704dcec9c9cdbad0aa83 | Actions examine (#39558) | PortCandidate | Interactions | CMU action tooltips still hard-code charge text instead of raising ExaminedEvent for extensible descriptions.
0392 | 9ecb8333f27acf05d1221d303532b83ca7816e0e | Predict suitsensor system (#39325) | Deferred | Medical, Interactions | Significant server-to-shared and client prediction migration touches RMC medical monitoring and EMP and equipment flows.
0393 | 2a46fb474f65c0f159af350004020e7aebf1c741 | Oasis: Add atmos network monitor (#39331) | Irrelevant | — | SS14 Oasis map-only change is outside RMC maps.
0394 | 6ae19340aba19f482228df7e2a8851a46ae3ff3e | Automatic changelog update | Irrelevant | — | Generated map changelog only.
0395 | 47d7db0665f31ac55f9b8eb6d7227fecbf495f9a | Base changeling objective(s) (#39562) | Deferred | Gamerules | Objectives and preset depend on the absent changeling and mind-filter feature chains.
0396 | ed6f906e6f5d917061dd69f88dfd4731d2b2b5dc | Better robotics console (#38023) | Deferred | — | Multi-layer borg health, brain, control, and UI change needs RMC silicon and console reconciliation.
0397 | 85708cad7fc5fe00aa320108ada6705f77a28ce7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0398 | a5351b8c770435dc502cd3b69aec05e174bd9812 | ExtinguishOnTrigger and TriggerOnInteractHand (#39537) | Deferred | Interactions | Retained final trigger feature, but its BaseXOnTrigger and shared TriggerSystem architecture is absent from CMU.
0399 | 3d71ddd1de194889e555a24cf68f8e871e516f9e | Merge stable into master (#39572) | Superseded | Movement, Medical, Interactions, Physics, Gamerules | Aggregate merge duplicates the individually classified stable changes from 0388 through 0398.
~~~

# SS14 upstream inventory: wave 0015

Audit date: 2026-07-20

- Pinned baseline: `59633f6dc50e77dda8cefa344d87c7b01e06a810`
- Pinned target: `40ca2c7f90d11d27be5457d177c133f0947d1c08`
- Range: ordered first-parent commits, zero-based indices 2800 through 2999
- Columns: index | full SHA | exact upstream subject | disposition | core-system areas | rationale

`Ported (CS-####)` links an accepted core-system change to the durable audit, while
plain `Ported` identifies accepted non-core work. `PortCandidate` retains target
behavior that still needs integration. `AlreadyPresent`
means CMU already has equivalent behavior. `Deferred` preserves behavior pending
focused reconciliation. `Superseded` means another target or local architectural
change replaces the commit. `Irrelevant` identifies commits with no standalone
behavior to port.

~~~text
2800 | 219f562601037a93c9b84cae1431872d54ec7ef0 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2801 | c3d57545d13462c5c528aad9590d379620691fb1 | Change Bad Chembottle Suffixes (#43064) | Deferred | Chemistry, Interactions | The affected filled-bottle prototypes are absent from CMU's retained chemistry-bottle layout and belong to that deferred migration.
2802 | c4d6e5b2cdc0170bcd24d61ce924023706925454 | Reparent White Gilgamesh (#43065) | PortCandidate | Chemistry | White Gilgamesh still inherits BaseDrink locally, so it misses the intended alcohol behavior.
2803 | ed0b178976b72b45c1b76b4fab611854691681bb | Throwing Croissant Incorrect Inhand Removed (#43066) | PortCandidate | Shooting, Interactions | The weapon croissant still inherits an invalid food in-hand sprite; the null Item sprite override is isolated.
2804 | 34d162bf58e365e5db950841cc2233a003122932 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2805 | b5b21c025a9a48f40012743de846b3d78f60f80a | Fix Makeshift Juicer Crafting (#43075) | Deferred | Chemistry, Interactions | CMU lacks the makeshift juicer introduced by the earlier deferred grinder and juicer feature.
2806 | 1751e417d0d3b8796c64257ce2da401a308b8702 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2807 | 3c75e8204097d05d440d1615aef7458b69dd76a1 | Add xenoborg gun sprites (#43013) | Deferred | Shooting, Interactions, Gamerules | The Xenoborg gun prototype and asset set diverges with CMU's retained borg content.
2808 | 6c33441203f80e201a29c02bc4a4715afa62da9f | Automatic changelog update | Irrelevant | — | Generated changelog only.
2809 | 6828abaa2896acde7c3b788da6d4944614829948 | Remove unused noise texture (#42970) | Irrelevant | — | Deletion of an unused texture has no standalone behavior to port.
2810 | 99d275f4035777b25dd6210dc853188485caacb2 | [Fix] Door remote fixes (#43063) | Deferred | Interactions, Gamerules | The six-file door-remote tag and access contract must be reconciled with RMC doors, remotes, and access policy.
2811 | 7970c4a2e7165997fbb5f583d735b1fc458bf29a | Automatic changelog update | Irrelevant | — | Generated changelog only.
2812 | b3501bc3cad387fc6d142f0c13cdeb555223b840 | Revenants cannot be stuck with plastic explosives (#42889) | PortCandidate | Interactions, Gamerules | A one-line Spectral blacklist prevents incorporeal Revenants from carrying attached plastic explosives.
2813 | 5d967bf6f150dfe9ef483a3cbb2aa6878c77acda | Automatic changelog update | Irrelevant | — | Generated changelog only.
2814 | 28ebad6621eccb54355bccf05d95df9714ded6e5 | Removed rollerskates & clown-outfit cargo bounties; reduce pancakes needed for pancake cargo bounty (#42899) | Deferred | Interactions, Gamerules | Cargo bounty removal and quantity changes are economy and content policy.
2815 | eab6b9cf1d01f41b4714e3e522461ceb941eb20b | Automatic changelog update | Irrelevant | — | Generated changelog only.
2816 | 2e0875abeddab482da4daf9125d2e835b029ef1f | Fix damagable mispredicts (#43080) | AlreadyPresent | Medical, Interactions | CMU's older handle-state path already copies the incoming damage dictionary before calculating and applying its delta.
2817 | 15e4b51a1166b2c9234bea134245a5f352f49130 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2818 | b32e24b77080838afd2deed077f271429de0c49d | Update Credits (#43084) | Irrelevant | — | Upstream credits metadata only.
2819 | 2011b520ea3c061433e18a30565c50b8300c676a | use proxy methods in EntitySystems (#43083) | Irrelevant | — | Proxy-method substitutions are mechanical source cleanup with no standalone behavior.
2820 | 5add0838b16250dd5ae8ec1d02e2b99428536531 | fix topicals (#43087) | Ported (CS-0013) | Medical, Interactions | CMU CS-0013 already treats topical damage types absent from the target container as zero instead of throwing.
2821 | 855979f4c848db6d9ea5b2b4c1d00c684530ae1b | Automatic changelog update | Irrelevant | — | Generated changelog only.
2822 | 1008bc5c063567b2123d8351fad834cf47f28225 | APC building doAfter fix (#43089) | PortCandidate | Interactions | The initial APC steel-construction step still lacks the intended two-second DoAfter.
2823 | d6eb03c8025e53797b5e3874af30e36fb09f3faa | Automatic changelog update | Irrelevant | — | Generated changelog only.
2824 | 59e7886c8d42b4450d5fcc0a676d65bd185e4218 | Fixed Mining Hardsuit Helmet Name + Added Internals to Crates (#43072) | PortCandidate | Interactions, Gamerules | Focused mining-hardsuit naming and crate-internals corrections add the missing nitrogen option for nitrogen breathers.
2825 | 5136e984312c141c5505874b59f4909d20cd599a | Automatic changelog update | Irrelevant | — | Generated changelog only.
2826 | 38a31af8265fdb517f45a53bd33c7b69f9c6e4d5 | Fix "Unknown messageId" warnings from ID cards (#42886) | PortCandidate | Interactions | The current null job-title fallback still localizes an empty key and produces Unknown messageId warnings.
2827 | df8e1511965f62bd22a58c253838d06abf151236 | yaml format nitpick (#43074) | Irrelevant | — | YAML indentation and formatting cleanup only.
2828 | 877070139f95df1225ccacb07211bb1c849b6f94 | Fix AtmosDebugOverlay being always active (#43073) | PortCandidate | Interactions, Physics | CMU still creates the atmosphere debug overlay at system initialization instead of only while debug data is active.
2829 | 2de2d7604f3c4f398342bc07caa97c130b626450 | Only one puddle sparkle please (#43086) | AlreadyPresent | Chemistry, Interactions | CMU's retained evaporation tick and sparkle spawn are server-only, so prediction cannot create duplicate sparkles.
2830 | 19c8221e8b0f8a87683be4aa0eca43fcca56f45f | Add borg construction interaction test (#42359) | Deferred | Interactions | The borg construction integration test targets a construction and borg substrate that diverges in CMU.
2831 | 07f24e7daa2862ba80c74bfe4fa5fd3ad0e06f8e | Fix door remotes 2 (#43094) | Deferred | Interactions, Gamerules | Follow-up whitelist tags are inseparable from the deferred index-2810 door-remote contract.
2832 | 0673373afc335a74ad412d67c716ff5cd829b40d | Automatic changelog update | Irrelevant | — | Generated changelog only.
2833 | 1fee95f82012fc1a3faa1f7ff1f66b86e00415ca | Stable merge to master (#43095) | Deferred | Interactions | Effective first-parent delta is one file, +1/-1, documenting alternative-interaction prying for an alert absent from CMU.
2834 | 18149dbb3b7f0beadc572069ea91a682aebf3e93 | Support for melee weapon user overrides (#39633) | Superseded | Interactions | Index 2877 fully reverts this melee-user override API before the pinned target.
2835 | d9d0cd92ae49e51df8909e9b058e72e36c663805 | add nested entity effects+conditions (#42341) | Deferred | Medical, Chemistry, Interactions | The nested condition and effect API crosses CMU's divergent entity-effect and reagent architecture.
2836 | 8cb0664f94ebd875daa2d875ea82b18c4732cd0f | Inventory + Storage toolshed query commands (#40813) | PortCandidate | Interactions, Gamerules | Bounded inventory and storage Toolshed query commands can be adapted independently for administration.
2837 | 11fd67d1fbc362d9ac027321a283317245fd6887 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2838 | b098b82cad715cf648102e62c840fe38e9093ecf | Hyper autolathe now requires the same ammount of manipulators as autolathe (#43109) | PortCandidate | Interactions | CMU's hyper-convection autolathe still requires three manipulators while the standard autolathe requires four.
2839 | be19f0bbdcdf18ce3398b6a14f0bb486510ea915 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2840 | eea773ffee682f21b0209976205fb3984d7d6581 | Remove salv stuff from mothership (#43007) | Deferred | Interactions, Gamerules | The Mothership map rewrite and Xenoborg economy removal require target-final borg policy reconciliation.
2841 | 506f9a36ece31b9e85f4dc6f30d48a7d39bca246 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2842 | eb1831774bf0e779b6849200254dcec70c56e276 | HTNComponent from SharedNPCSystem (#42750) | Deferred | Movement, Interactions, GameTicking | Moving HTN ownership across shared and side-specific NPC systems crosses extensive RMC NPC behavior.
2843 | 1918f8e8ea2a6c6709039729859503d547289c34 | Small tippy fix + msg toolshed additions (#43082) | PortCandidate | Interactions, Gamerules | The Tippy layer guard and session-oriented message command overloads are focused fixes absent locally.
2844 | 0611a4fdeba31f3c22af3608fa1401c4ecae50ea | Automatic changelog update | Irrelevant | — | Generated changelog only.
2845 | cfe2e06435dbd1aa16efcb5fc86d24d613c008c9 | Brings Estoc DMR accuracy to standard rifle accuracy, removes movement speed debuff (#43038) | Deferred | Movement, Shooting, Gamerules | Estoc accuracy and movement changes are weapon balance policy.
2846 | c2681cdb17e5cb55314e92ba4fdc1d4fc3feda8e | Automatic changelog update | Irrelevant | — | Generated changelog only.
2847 | b2b547b8061fb33b07d7ff642c7fc7bbfe40c4c1 | Initial decoupling pass on Damageable (#43103) | Deferred | Medical, Shooting, Interactions, Physics, GameTicking | The 83-file Damageable decoupling rewrites central damage APIs and many RMC-divergent consumers.
2848 | 90f6a725078008b554336e4b0d154460eb95a4b9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2849 | ae6b6811f9f0905f216979c896f2922ec86b0489 | PinpointerSystem&Component cleanup (#42463) | Deferred | Movement, Interactions, GameTicking | The Pinpointer component and system cleanup crosses retained RMC pinpointer and objective behavior.
2850 | 755cdea079baf5f3e7c25050ae2c755275f6e577 | Serpentcrest antivirus update (#43102) | Deferred | — | Large generated Serpentcrest map rewrite.
2851 | f98be0df723cf085a993fab30dad4b90fc798b9e | Automatic changelog update | Irrelevant | — | Generated changelog only.
2852 | e00c8bdee4272155dd16199c5ddcef05fc04292e | Vulpkanin Gauze (#43096) | PortCandidate | Medical, Interactions | The Vulpkanin gauze markings and sprite states are a cohesive cosmetic extension to CMU's retained gauze set.
2853 | 12f25e2f45fec8176261bbaf6570f682745ff4be | Automatic changelog update | Irrelevant | — | Generated changelog only.
2854 | e630dcd7744969ea6d25cf47f6c2d94627c4e49b | Update Credits (#43140) | Irrelevant | — | Upstream credits metadata only.
2855 | 05843d05c295727eee461c2cf16b484ec4b2038a | Mix xenoborg_scream.ogg to mono (#43132) | Irrelevant | — | CMU does not retain the referenced Xenoborg scream asset, so there is no audio stream to normalize.
2856 | 15f95286746122d43833bd5b3e7ccd4c5e7ae814 | Fixing issue 42759, air grenades cannot be re-triggered. (#42866) | Deferred | Interactions, Physics | CMU lacks the target air-grenade prototype substrate; the retrigger guard belongs with that feature.
2857 | 1821d1e4eca97ca732cfda43a2c502eb35e0b6ec | Automatic changelog update | Irrelevant | — | Generated changelog only.
2858 | dd051d017c573954a14356b0cb255e72ff0ac06c | Nukie guidebook improvements (#43131) | Deferred | Gamerules | Nuclear Operative guidebook wording is antagonist policy content.
2859 | 601e5721880418ce5f03fa45cef6374614182e3a | Make map tests more fine-grained. (#42977) | Deferred | — | The five-file map-test and YAML-linter restructuring needs dedicated test-infrastructure reconciliation.
2860 | 1b5011152b6e1e4e5d871a251d6ba5618db02df1 | Weather entities (#41427) | Deferred | Interactions, Physics, GameTicking | The 21-file weather entity, status-effect, trigger, command, and prototype migration is a broad simulation change.
2861 | 24382dd3959d552cdc1cfb676a3a52386dbfbb01 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2862 | 4cdef6ac92ee8de549b24c2a57cfbdbe7bbce9c0 | Patch for engine ComponentFilter (#43168) | PortCandidate | Interactions, GameTicking | A one-line namespace qualification avoids collision with the newer engine ComponentFilter while preserving CMU's NPC query type.
2863 | 8e0a27199a6e21ada363a340707fbe70d9d9c0e6 | Refactor guide entry tests. (#43159) | Deferred | Interactions | Guide-entry test refactoring should follow CMU's eventual GameTest and guidebook-test migration.
2864 | b01d2ec5cb18e2d51bb6d548634b2c7d9ee9a505 | Refactor explosion prototype tests. (#43158) | Deferred | Physics | Explosion prototype test refactoring belongs with the deferred test-framework migration.
2865 | 5eba73c5d873efdac7908b9871dbef290189eb65 | Refactor Device Linking tests. (#43157) | Deferred | Interactions | Device-linking test refactoring belongs with the deferred test-framework migration.
2866 | be8bd66f67feb17e78ba63cfb597c35a5798f83c | Refactor construction tests (#43155) | Deferred | Interactions | Construction test refactoring belongs with the deferred test-framework migration.
2867 | 87a37874ae5d2d921b610e7d25a3d9b3c5108aed | Add exception tolerance to SharedDoAfterSystem. (#43088) | PortCandidate | Interactions, GameTicking | DoAfter exception isolation is valuable and CMU has the required runtime APIs; adapt it with index 2894 while preserving RMC DoAfter hooks.
2868 | b07fc5f4c1daba7bac966d270b8ec9819425dc7a | Reactions test cleanup. (#42979) | Deferred | Chemistry | Reaction-test cleanup depends on CMU's divergent chemistry and reaction-test architecture.
2869 | 229ce3e3f0eb3bab3b67d13a045a0c9b701b5fff | Refactor MobThresholds and Stamina tests. (#43156) | Deferred | Medical | Mob-threshold and stamina test refactors should follow the deferred damage and GameTest migrations.
2870 | c83077f436a23ee09e83c39f76c39755359dc917 | Update `NodeHelpers` and `Node.GetReachableNodes` to use `Entity<TransformComponent>` and `Entity<MapGridComponent>` (#37734) | Deferred | Interactions, Physics, GameTicking | The 13-file node Entity<T> migration crosses central power, pipe, electrocution, and RMC node behavior.
2871 | 700e901cbf44fc841d7c66cc5dcda5fba35ccf28 | Add a basic API to `JukeboxSystem` (#42896) | Deferred | Interactions | The Jukebox API is a structural refactor with no target-slice consumer and must preserve CMU audio behavior.
2872 | 5f030ee346743e73be634f5c0ef33e390c176028 | Atmos YAML-defined gas flammability, flammability API (#43165) | Deferred | Chemistry, Physics, GameTicking | YAML-defined flammability changes the central atmosphere gas contract and belongs with the shared-atmos cluster.
2873 | f3bb1becf5dce787367f52e542a847b2248f0304 | Vent Hordes (#43047) | Deferred | Movement, Interactions, GameTicking, Gamerules | Vent Hordes is a substantial event-rule, AI spawning, and content feature.
2874 | d547adcf89169e3776fa80ffb29d5a5b011914c7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2875 | 67b0e89457ce39221abfbc3b07345a4ed0ab2ecd | Add DB Config to development.toml (#43036) | Irrelevant | — | Upstream development database preset configuration has no portable runtime behavior.
2876 | 012ffe5abddee8953f51ec14776c6995df7ee16d | Add banning to server api (#43205) | Deferred | Gamerules | The server API ban endpoint changes privileged external administration and database behavior.
2877 | 38cd873fc30f757a686e7647a76b072e1b585056 | Revert "Support for melee weapon user overrides" (#43106) | Superseded | Interactions | This revert cancels index 2834's melee-user override API in the target history.
2878 | 03f19635896f102cb8481ccbb0ef0122b1a205e0 | Update RT to 273.0.0 (#43130) | Superseded | — | CMU already pins a newer RobustToolbox generation; the engine submodule is outside content-port scope.
2879 | d90284304e98334bdafaefa81a9aaee23f328e64 | Wrapped parcels can go into mail carts (#43226) | Deferred | Interactions | CMU does not retain the target mail-cart prototype; the whitelist addition belongs with that cargo feature.
2880 | 30c28b583365e285436382f3eaf2cee7b1769b37 | Make test runnable on windows in debug config again (#43227) | PortCandidate | — | A one-line path comparison restores the affected integration test under Windows debug builds.
2881 | 03d5c4c685aa0a8528a61b50ed67be2693f1ab5c | Removes a test that handles engine behavior in content. (#43228) | Irrelevant | — | Removal of a content test for engine-owned behavior has no runtime behavior to port.
2882 | d6f1e97de5aa223e3022e68948ba84330b41e619 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2883 | a915930991cfe47478e27f606b049fef9c1b20da | [Fix] Silicon Ion Law Scramble Text (#43229) | PortCandidate | Interactions, Gamerules | Allowing ScrambleTag in silicon-law UI fixes scrambled law presentation using types CMU already retains.
2884 | dcbe5e08c844b632f7da807e106b5c452de80a9d | Automatic changelog update | Irrelevant | — | Generated changelog only.
2885 | a1d7406bf2d64f4d0938b2b668e9c8dcffff159a | Move a metric fuckton of AtmosphereSystem to Shared. (#42989) | Deferred | Chemistry, Physics, GameTicking | The 71-file AtmosphereSystem server-to-shared migration rewrites core simulation and test APIs.
2886 | 58cab1039c6c0d6aba7ff4c4ce06fefd344cfbba | fix bottle reagent localization (#43208) | Deferred | Chemistry, Interactions | The affected target filled-bottle prototypes are absent from CMU's retained chemistry-bottle layout.
2887 | e105237e1e44b0989f89353cc32c0bc4f3911ab2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2888 | 2cbc8575f786cfa7eaa70b60ec8e3c26c53e38c5 | Fix missing word from emitter alerts (#43225) | Deferred | Interactions, Physics, Gamerules | CMU lacks the upstream emitter radio-alert substrate that this localization and data-field fix extends.
2889 | e49d36a88e20732755c524be946c1754622f897b | Make EntityTableSpawner spawn relative to grid instead of map (#43234) | PortCandidate | Interactions, Physics, GameTicking | CMU's EntityTableSpawner still uses map-position spawning, which can detach offset spawns from moving grids.
2890 | fe8f5f2d41d22b43f893ffd7fbe0e343aa474c91 | Add a holy light effect to bible healing (#43189) | PortCandidate | Medical, Interactions | The short Bible-healing light effect is a self-contained presentation enhancement.
2891 | 23aa4c95e4f1daf7ea8e5a3cf90f3318a12bba64 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2892 | 6e3338229332e33c3f4a12d526cbb8e233e3e037 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2893 | 93dccc34c58f9c06f79ce0d95b2f13d8806d83f8 | Content-side IRobustRandom cleanup. (#43231) | Irrelevant | — | Content-side using-directive additions are mechanical source cleanup.
2894 | fbed18b1648b8eea3ca05fa4fba6108f88d1230a | Cleanup warnings: CS0168, CS0414 (#43198) | PortCandidate | Interactions, GameTicking | This is the target-final conditional-compilation cleanup required when integrating index 2867's DoAfter exception tolerance.
2895 | 4ab475ad0b1986f4609d07f62978208f1e6fed94 | On Exo, fixed power for AI North and South external cameras (#43143) | Deferred | Physics | Generated Exo map power correction requires current-map reconciliation.
2896 | 4e6bf98aace6e2f990b4b2eb841e660bf35b0c2c | Automatic changelog update | Irrelevant | — | Generated changelog only.
2897 | 193c0dc32716fae7d900357351aaa07339318bb1 | CachedResources for GasTileDangerousTemperatureOverlay (#43032) | Deferred | Physics | The cached-resource refactor targets an atmosphere overlay class absent from CMU's retained client architecture.
2898 | 05b6ec1862f6723ed94384a826ebd960cb8af590 | Update Credits (#43241) | Irrelevant | — | Upstream credits metadata only.
2899 | d5ed6aa462549ceaaf154707f112656592bf95dd | Refactor: Gas tile overlay split (#42881) | Deferred | Physics, GameTicking | The seven-file gas overlay split is a large client atmosphere rendering migration.
2900 | 3caea1649d7ac20e7407e2a9f26cf4840f01b3e9 | Move ID card name/title length limit to server (#43237) | PortCandidate | Interactions, Gamerules | CMU still enforces ID name and title lengths only client-side; authoritative server truncation closes the bypass.
2901 | 60ce454cb5dfe411a70fa8b2320770d4549f6b23 | Update ruined_prison_ship.yml (#43242) | Deferred | — | Generated ruined-prison-ship map correction.
2902 | 38f780037036a6db556e4108de0a87324604ff97 | Single item cargo orders are delivered in Parcel Wrap instead of crates (#40834) | Deferred | Interactions, Gamerules | The 24-file parcel-based cargo delivery rewrite changes order, wrapping, UI, event, and prototype contracts.
2903 | c99d94cdf934f5e5adf57cd70bed0cdd5574a748 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2904 | c51a7ea4257afec4c09a24c29f20aa6d91e4b4a3 | Refactor Ion Law string generation (#42629) | Deferred | Interactions, Gamerules | The large Ion Law localization and string-generation redesign crosses silicon and station-record behavior.
2905 | 116e09e1cc66bca8601cbd2ec6b146425b269b70 | Fix bug blocking character saves for species without hair. (#43170) | AlreadyPresent | Interactions, Gamerules | CMU's retained pre-Nubody profile schema stores hair fields directly and does not enumerate empty hair-marking lists while saving.
2906 | 651b70f13ae34638d1dea0f5b91293cca82d7c73 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2907 | 2736c458c4d11be477e447678e882fd0049d7a1f | Predict reagent grinder (#42815) | Deferred | Chemistry, Interactions | The 14-file predicted reagent-grinder migration crosses divergent chemistry, kitchen, UI, and solution APIs.
2908 | bc0ce07cc22d25362c27c37eb560cff3b07c8da8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2909 | 5c94e14d1dad13b5c6a964677f2252b664798b2b | Fix reagent grinders on dev map (#43253) | Deferred | Chemistry | Generated development-map rewrite tied to the deferred grinder migration.
2910 | 31bf1e763dc40f9bce9b40c05ea462757d90d3c1 | Fixed the two-handed activated sound bug. (#38070) | PortCandidate | Interactions | The wield-triggered item toggle still omits the user and lacks first-prediction gating for its active sound.
2911 | 53e413913e21f4de1b10b4c7b3234136c26484c6 | Removed MovementBodyPartComponent (#43257) | Deferred | Movement, Medical | MovementBodyPartComponent remains actively used by CMU body logic and several body-part prototypes.
2912 | 9b0067eff4d107a25a7bf783818d7c210a892c22 | Staging To Master (#43264) | Deferred | Interactions, Physics, GameTicking, Gamerules | Effective first-parent delta is 46 files, +340/-689, mixing Xenoborg economy removal, Mothership content, power-system deletion, and atmosphere changes.
2913 | ec8f1d7ea417b4517e5f6575635ef22f9243c69f | Add test result archiving to our test actions + Test fixes (#43175) | Deferred | — | Workflow result archiving is mixed with YAML-linter and chemistry-test fixes and needs tooling reconciliation.
2914 | 33beb79a820a4f303e8762c38bcdd2f8210e1128 | Update RT to 274.0.0 (#43261) | Superseded | — | CMU already pins a newer RobustToolbox generation; the engine submodule is outside content-port scope.
2915 | 5645b94fa2cdb4aa05275c83b2d05860aaaacda2 | Make the chem guidebook show when chemicals adjust body temperature (again) (#42394) | AlreadyPresent | Medical, Chemistry | CMU's retained AdjustTemperature effect already supplies equivalent reagent guidebook text.
2916 | f63d2fd71365ccc4e03d6a9c769e003977387fa0 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2917 | 4ae82ec8a3b6d40781b41998dae59cc4c213e081 | Tech disk resprite and pricing changes (#37719) | Deferred | Interactions, Gamerules | Technology-disk sprites, tiers, console behavior, and pricing form a research-economy feature.
2918 | 53ca3fe3f0593b94c1eb1ad0de4eefe7d142e114 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2919 | 9087a2ee6c1566848e623ec7327803cca54f9b3d | Stable -> Master (#43284) | AlreadyPresent | Medical, Physics | Effective first-parent delta is one file, +1/-1; CMU's pre-decoupling explosion path already includes only damage types present in the target container.
2920 | cedae35908f75c5788d74acac2671985a80efa7f | Fix thermomachine guidebook typo (#43285) | PortCandidate | Physics | A one-line guidebook prototype reference corrects the displayed maximum thermomachine temperature.
2921 | ee3926ff9473cc2dd6ce2de78504896dfa0b6ce8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2922 | b03a72d0e712266a1ba8e05879d26f8a63582e77 | DeleteComponentCommand minor cleanup/rename. (#43289) | Irrelevant | — | Admin command file rename and mechanical cleanup only.
2923 | 087edd285c1c3a8cd6a750e612e8827c89298205 | Cleaning Blood Footstep Sounds (#43266) | PortCandidate | Interactions | The blood-footstep audio normalization and attribution refresh is a self-contained asset improvement.
2924 | 2008be23f68c6294748ab76f444c556c671e5a9c | Update Packed's TEG Burn chamber (#43278) | Deferred | Physics | Large generated Packed station TEG chamber rewrite.
2925 | 6a675126ad848468cfce6f538545d77ed5e7fea9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2926 | b251848409f330eae15e2a45bd846de29020ca06 | Move salvage RandomSpawners to EntityTableSpawners (#43233) | Deferred | Interactions, Gamerules | The three-file salvage spawner rewrite belongs to the broader entity-table migration and CMU loot-pool reconciliation.
2927 | f27d60049c485cc1664ba912a93a26e4dd51a0d9 | Borg charger hitbox changes (#43300) | PortCandidate | Interactions, Physics | A focused fixture addition corrects the borg charger's interaction and collision footprint.
2928 | 545a4bcf9bef6abfc63b94fc73152000e3c935cf | Automatic changelog update | Irrelevant | — | Generated changelog only.
2929 | d14423f0f6bb3bd58f17172d4e4fcc1bbec663b0 | Move some simple random spawners to entity table spawners (#43305) | Deferred | Interactions, Gamerules | Ten prototype files migrate random spawners and retained content pools to entity tables.
2930 | 7a9cd0be40207931949568dbed82ae32d92a39e0 | Fix melee attack sprite rotations (#43307) | AlreadyPresent | Interactions | CMU's retained melee effect path already lacks the NoRotation assignment that this commit removes.
2931 | 803fd2d427546171f00ef0bb832618798e234b43 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2932 | 7cf3728c59026702596194e92f5e5aca326ec675 | Update Credits (#43313) | Irrelevant | — | Upstream credits metadata only.
2933 | d86219c50a2a3e83036d3199604273490876b373 | removed PhysicalConstants.ZERO_CELCIUS (#43316) | PortCandidate | Chemistry, Physics | Two isolated conversions can replace the misspelled duplicate Celsius constant with canonical Atmospherics.T0C.
2934 | 226deda26c06dec6caffe8b61ce6d963fe9bf18e | Fix welders running an additional time when no damage remains. (#43321) | AlreadyPresent | Interactions | CMU's older Repairable system performs one non-repeating tool operation, so it cannot schedule the target's extra repair pass.
2935 | 441f4c8e79b1985a07e693b8a75f7a9c81ba2310 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2936 | 4f485d8d33a206208330138f5cf0fe061cb1e031 | Correct attribution for /Textures/Objects/mre.rsi (#43328) | Irrelevant | — | Texture attribution metadata only.
2937 | cc732f240d28ffe4b09dfffdd8ef2df5f79e21d8 | Flatpack opening uses collision of flatpacked entity (#41849) | PortCandidate | Interactions, Physics | CMU still rejects flatpack opening using any-entity intersection instead of the packed entity's actual hard collision.
2938 | fcb74f64fc57710fcfca228842660dd7508e7292 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2939 | 823597144498770bd15e4b50e09a074efbdd9006 | Move remaining random spawners to entity table spawners (#43324) | Deferred | Interactions, Gamerules | Seven remaining random-spawner prototype files belong to the broader entity-table migration.
2940 | 0d1c74494578eca949dfdc47b3009988d185ffd3 | Improvments for Reach station (#43046) | Deferred | — | Large generated Reach station map rewrite.
2941 | 1a2831de510e7042d672252467fb51c27eeae8bc | Automatic changelog update | Irrelevant | — | Generated changelog only.
2942 | d743763f33b45a00ed6495d3e984693ef010cb11 | Update RT to 274.0.1 (#43337) | Superseded | — | CMU already pins a newer RobustToolbox generation; the engine submodule is outside content-port scope.
2943 | db74373e508c3aafc41582ad46492ef4c1fd4f62 | remove all use cases of TimerComponent (#43320) | Deferred | Interactions, Physics, GameTicking | Removing TimerComponent crosses flammability, powered lights, conditional spawners, maps, and retained RMC timer behavior.
2944 | 35d97c6be6f30aa022bdd87d85ed79db8509c27b | trico no longer heals when mob is crit, and no longer heals rad (#43293) | AlreadyPresent | Medical, Chemistry | CMU's retained Tricordrazine already gates healing below 50 total damage and has no Radiation healing entry.
2945 | c57e11760a150bbff6c336b52bda1866da712533 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2946 | 744e2984e606df80a78bceb0fe587354b959a871 | Fixes ambuzol pills. (#43331) | AlreadyPresent | Medical, Chemistry | CMU's pre-stage Medicine metabolism already lets swallowed Ambuzol and Ambuzol Plus cure infection.
2947 | 343cc049a3a0bca29edf723c64ad58221076fb0e | Automatic changelog update | Irrelevant | — | Generated changelog only.
2948 | 7e386def1b4fe4c34391a1db756e62dac8b5cf58 | Fix `SharedCrayonSystem` being both `[Virtual]` and `abstract` (#43339) | AlreadyPresent | Interactions | CMU's SharedCrayonSystem is already abstract without the Virtual attribute.
2949 | 3d67725a58490866706b029271dc60f2bd765c19 | Painting and poster random spawners to  entity table spawners (#43270) | Deferred | Interactions, Gamerules | Painting and poster spawner rewrites belong to the broader entity-table and content-pool migration.
2950 | 971490e202f9c043463fc649578d094064a086f2 | Update RT to 275.0.0 (#43357) | Superseded | — | CMU already pins a newer RobustToolbox generation; the engine submodule is outside content-port scope.
2951 | 5bd9d21cb605b3a8ccb6ad46ded392a6b850ab58 | Change chat straight quotes to curly quotes (#43350) | PortCandidate | Interactions | Five localized chat and emote strings can adopt the target's consistent curly quotation marks independently.
2952 | bc29aeac3bf1ab6f62e50ad82dc295779a5e9ca6 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2953 | 9adb10d791460b7123a8484440806d4dfb0a4beb | GameTest part 1 (#43182) | Deferred | GameTicking | The 29-file GameTest framework foundation requires dedicated integration-test infrastructure adoption.
2954 | ef1169a079aae6a685200bd3df6c5ca2d57b8781 | Minor fixes to server api ban endpoint (#43355) | Deferred | Gamerules | The server API fix depends entirely on the deferred index-2876 privileged ban endpoint.
2955 | ab9adaa14e7fc6524174c2c6ff7e1ead216b70b0 | [FIX] Moved random manifest back server (#43361) | Deferred | Interactions, Gamerules | Moving random manifest generation server-side depends on the deferred index-2904 Ion Law refactor.
2956 | c186f922966c2704f8fcd1c9b91691bd4b65137b | Automatic changelog update | Irrelevant | — | Generated changelog only.
2957 | e47ce26377fa69141d56561b5dfc9fb761d34dee | Remove `[Virtual]` from some classes that shouldn't have it (#43347) | Irrelevant | — | Virtual annotation and sealing cleanup has no standalone runtime behavior.
2958 | c10a2ae2da7082b17df437ab26b64ed3352743e0 | Update RT to 275.1.0 (#43368) | Superseded | — | CMU already pins a newer RobustToolbox generation; the engine submodule is outside content-port scope.
2959 | b582f2e156a454d860a210a4d02fa885da6a64f6 | cleanup GasPrototype and some other atmos code (#43318) | Deferred | Chemistry, Physics, GameTicking | The 23-file GasPrototype and atmosphere cleanup depends on shared-atmos and gas API migrations.
2960 | 067bd9f43c4dc78d46c9c3644f300b29dd48c335 | Fix `TrackingIssueAttribute` blocking orgs/repos containing numbers (#43369) | Deferred | GameTicking | The one-line TrackingIssue test fix depends on the deferred index-2953 GameTest framework.
2961 | 8e87fd3e58ccbdf4631569b70d9a18a3307d127d | Fix: Make clockwork glass grindable (#43371) | PortCandidate | Chemistry, Interactions | Clockwork glass still points Extractable at nonexistent brassglass instead of its cglass solution.
2962 | 937b5f955d657a7960f93b14d99a5c0c06beb67f | Automatic changelog update | Irrelevant | — | Generated changelog only.
2963 | e0eae8628f7a4a80efeb627f494fbbb1f8ace88b | Makes singularities respect reduced motion option (#43362) | PortCandidate | Interactions, Physics | The singularity distortion overlay can directly honor CMU's existing reduced-motion CVar.
2964 | 54bbdde221d392146977a98e8138f86131867e69 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2965 | a87a4d3fb3f27983e01d15d1aae053a26d8609c6 | "Reduce motion of visual effects" now works for blood loss effect (#41878) | PortCandidate | Medical, Interactions | Reduced-motion support for the blood-loss and drunk overlay is valuable but must be adapted to CMU's retained older status-effect system.
2966 | 9598347e81efcac3baeb704e980f8621f8ca696a | Automatic changelog update | Irrelevant | — | Generated changelog only.
2967 | 1f24878cf6d5e7d7cdcaeabfffa67fcd0ec25103 | Heat distortion shader (#42973) | PortCandidate | Interactions, Physics | The seven-file heat-distortion overlay is a bounded visual feature; integrate only with its index-2979 correction and rendering checks.
2968 | c30b35d6ad24bf6a2d55a6e24ff3ef46e4c8c678 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2969 | b131c58501bd8d052e5214d83834191dca38ac82 | Update Credits (#43376) | Irrelevant | — | Upstream credits metadata only.
2970 | e9e497103a22d7cd45111aa9c90b7264e77973ff | Update RT to 275.2.0 (#43378) | Superseded | — | CMU already pins a newer RobustToolbox generation; the engine submodule is outside content-port scope.
2971 | 29d9c1786d62307ee55f40a4c035815c8435b222 | Fix changeling transform ability (#42107) | Deferred | Interactions, Gamerules | CMU lacks the upstream ChangelingTransform and dynamic store-UI substrate this fix targets.
2972 | ef2ea25cdf6472a2ded88948feb011c4de6b6107 | Kill unused terminator files (#43381) | Irrelevant | — | Deletion of unused Terminator localization and assets has no standalone behavior.
2973 | bc5b695107eee0743a5e2af248fc1f87ebacc971 | Remove PDA equip sprites (#40498) | Irrelevant | — | Deletion of unused PDA equipped sprites has no standalone behavior.
2974 | 9ee10acc99e2b4ec7fa581168476c1cc109abd29 | Lathe Menu Title Enhancement (#43392) | PortCandidate | Interactions | A focused FancyWindow conversion lets the lathe menu display the machine entity's name and description.
2975 | f812ee23be300dfc2c80cc097eb709e4ac5ff51f | Automatic changelog update | Irrelevant | — | Generated changelog only.
2976 | f44e9999b41dd2bdc68d99ce0a5a5a7568e7eefb | Reduce speso value of tech disks (#43395) | Deferred | Interactions, Gamerules | Technology-disk value changes depend on the deferred index-2917 disk and pricing rework.
2977 | 79b55a4f65f1cce7d978414059e8a8302dad0214 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2978 | aa00efccb2cdc182ae17b0f33abbf079c6fc0cf5 | merge stable into master (#43396) | Deferred | Gamerules | Effective first-parent delta is two files, +12/-12, changing Wizards Den whitelist and character-name server policy.
2979 | e27f51762b5e0476c7d8f9041bb7db4bfaaa4070 | Bugfix of Heat distortion shader (#43397) | PortCandidate | Interactions, Physics | This is the required target-final viewport-origin correction for the index-2967 heat-distortion overlay.
2980 | 76e4d48273e445e8a6c27a1342b9c3c6b8176eca | Greatly reduce spawn rate of salt ore (#43012) | Deferred | Gamerules | Salt ore spawn-frequency changes are procedural generation and economy balance policy.
2981 | 74c8fdde7221c54372836c51475ea545f99fef93 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2982 | 03b46abef034c4186953b53e57e6fd3341ffb2f0 | Fix ghost time of death (#43411) | PortCandidate | Medical, GameTicking | CMU still records ghost TimeOfDeath with CurTime but examines it against RealTime, producing incorrect elapsed times.
2983 | 4b9e7352d57374f138fb8cee1186d7a2c6023b6d | Automatic changelog update | Irrelevant | — | Generated changelog only.
2984 | d42adbf05df1902c0ab3fb90995ee64737df383b | Gametest Part 2: Preliminary refactor every test to use GameTest as the framework. (#43207) | Deferred | GameTicking | The 163-file migration of tests and benchmarks to GameTest requires a dedicated infrastructure effort.
2985 | dc87ece47802af48eb6fd97f37df02b3a6010de1 | STABLE TO MASTER (#43434) | Deferred | Gamerules | Effective first-parent delta is one file, +1/-1, correcting the whitelist action introduced by index 2978; reconcile the pair as server policy.
2986 | 0b8ff0ffa276b6c59c17d415f360c959739ba1be | New Wizard robe and hat in-hand sprites (#43429) | PortCandidate | Interactions | Self-contained Wizard robe and hat in-hand sprite refresh.
2987 | 1e59b3aac85e6b11434165d8a3dce2eed7816fa7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2988 | 81be6f25713f5770636368e0d1c6782922d5e7dd | Force Vent Critters to Attack (#42399) | Deferred | Shooting, Interactions, Gamerules | Forced Vent Critter attacks depend on the deferred index-2873 Vent Hordes event and feedback content.
2989 | 9504c1e0bb66294937dfd5037fe5c7a08e420c68 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2990 | 17ccf1dc70d7a931c798bf8987c343d65afb96ed | Hijack the Automated Trade Station objective (#42135) | Deferred | Interactions, GameTicking, Gamerules | The 22-file trade-station hijack objective adds new cargo, anchoring, store, beacon, objective, and content contracts.
2991 | ecf554549ca70db784ea88dc8af0147f0b793caa | Automatic changelog update | Irrelevant | — | Generated changelog only.
2992 | f688bfc7713126274df5ac1620de079f2c973946 | Remove suitstep1 and suitstep2 ogg files. (#43441) | Irrelevant | — | Deletion of unused suit footstep audio has no standalone behavior.
2993 | 5e456146e31f4174fc9392cf4bda410606602222 | fix Changeling transformation making items in slime storage inaccessible (#43393) | Deferred | Interactions, Gamerules | CMU lacks the target ChangelingTransform and identity-storage architecture that this storage fix extends.
2994 | 7234cec825014418fb5d25b7aec84972d4324ac3 | Predict ghost examine (#43150) | Deferred | Interactions, GameTicking | Ghost-examine prediction must integrate with index 2982 while preserving CMU's RMC imaginary-friend exception and paused-time behavior.
2995 | 088dbb2d38e7db3f779311f2ca8636440f2e0262 | Add blacklist to entitystoragecomponent, add blacklist to genpop lockers (#41633) | PortCandidate | Interactions, Gamerules | A reusable entity-storage blacklist and GenPop application close a focused containment-policy gap, with RMC storage review required.
2996 | f0cab2d6ed42040f04242e5dde3d02646f70c0ee | Automatic changelog update | Irrelevant | — | Generated changelog only.
2997 | 6c9e10e1a9ef8c25904d35ae8e0f92dece76683f | Removed JuicePotato reagent (#43448) | Deferred | Chemistry, Interactions | Removing potato juice is reagent and botany content policy.
2998 | 417f1b7ceb3414580d1f219ecb8663a097d73700 | Add "failed to load crew manifest" message (#43400) | PortCandidate | Interactions | A small UI-state fallback replaces an indefinitely loading crew manifest when no owning station exists.
2999 | f6fc3edcf7fcc62d6563e7a6f482b5b1e2be96d5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
~~~

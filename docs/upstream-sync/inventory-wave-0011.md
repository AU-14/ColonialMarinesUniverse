# SS14 upstream inventory: wave 0011

Audit date: 2026-07-20

- Pinned baseline: `59633f6dc50e77dda8cefa344d87c7b01e06a810`
- Pinned target: `40ca2c7f90d11d27be5457d177c133f0947d1c08`
- Range: ordered first-parent commits, zero-based indices 2000 through 2199
- Columns: index | full SHA | exact upstream subject | disposition | core-system areas | rationale

`PortCandidate` retains target behavior that still needs integration. `AlreadyPresent`
means CMU already has equivalent behavior. `Deferred` preserves behavior pending a
focused compatibility pass. `Superseded` means later target behavior replaces the
commit. `Irrelevant` identifies generated, project-administration, engine-pointer,
or empty-merge changes with no standalone content behavior to port.

~~~text
2000 | 231a93e742fb2cbab906e418f62b6ff18df0a6dd | TriggerOnUserInteractHand and TriggerOnUserInteractUsing (#41843) | Deferred | Interactions | The seven-file trigger and interaction-event rewrite crosses RMC's retained trigger consumers and needs a focused API reconciliation.
2001 | 482e963227ca6f6c7a7260332b98ee99a6083fec | Automatic changelog update | Irrelevant | — | Generated changelog only.
2002 | f20288046193abbf67a940f1faee73e88a3a41a8 | Admin log now shows APC power toggle interactions (#41839) | PortCandidate | Interactions | APC breaker logging is a focused auditability fix that can be adapted to CMU's APC handler signature.
2003 | c5148750d56f8f132b16aa84998155ffb411eda1 | Automatic changelog update | Irrelevant | — | Generated admin changelog only.
2004 | 6fc13a5875ea2cc7d46a4bcbc822cecef0dfd577 | Adding a random gate (#41627) | Deferred | Interactions, Gamerules | The new linked-gate component, UI, systems, and prototypes require target-final device-network and map-policy reconciliation.
2005 | 368fa40f5c44c306dd29b89544a34592d1baa0ef | Automatic changelog update | Irrelevant | — | Generated changelog only.
2006 | 226f91068613699cbd402c06d57224c7f473f826 | Assorted minor cleanup (and shotgun shell descriptions) in Resources\Prototypes\Entities\Objects\Weapons\Guns\Ammunition\ (#41841) | Deferred | Shooting | The 41-file ammunition rewrite mixes formatting, hierarchy cleanup, and descriptions across RMC-divergent gun prototypes.
2007 | 30afada8f3f172b135f795bfdd02334108f5a143 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2008 | 410b2c99458d8c0de518841a309f0babfeeaa798 | Skeletons are now affected by Holy damage (#41757) | PortCandidate | Medical | The modifier-set and skeleton prototype adjustment is small, but should be checked against CMU species damage policy.
2009 | ff8a000a0afea5df8d9bbad9dc6f2bf6cb403921 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2010 | cc90ac99f8f5f74be4c5935a8b611272dd2c0f77 | Make gun chamber empty by default (#41791) | PortCandidate | Shooting | Removing preloaded chamber rounds from standard guns is isolated, subject to RMC weapon-spawn expectations.
2011 | 7eb797e7e5779831c6c6fcd39c36957023856fcb | Toys entity tables (#41840) | Deferred | Interactions, Gamerules | The broad toy, crate, bounty, and entity-table migration needs target-final prototype reconciliation.
2012 | faa12d0e663377aa255d8c4d0130d6877bc47862 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2013 | 5650e6be0fd39da9888f29c64452650c4e9e70f9 | Fix plant metabolism in guidebook. (#41854) | AlreadyPresent | Chemistry | CMU's retained guide-entry path already renders plant effects without the upstream nullable-metabolism zero-scaling bug.
2014 | 12b9e3735b9ecb70744daaba0773f5edb3956c19 | Move logic from EvenHealthChangeEntityEffectSystem to the damage system API (#41684) | Deferred | Medical, Chemistry | Moving even-health distribution into DamageableSystem changes a central damage API used by RMC medical and entity-effect systems.
2015 | fbb452b60dea2fd5a79ad9778bdcb0327b3e1e9b | Update to Bardrobe to add Pun Pun's outfit (#41705) | PortCandidate | — | The single vending inventory addition is isolated and needs only retained wardrobe-ID validation.
2016 | 841f22dcfdf73911d2eb502bb31b5f362e928c64 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2017 | 12992fd75bbba558ca6c074a6e3940b3560f60ff | Remove roundstart tools from some cyborgs (#41823) | PortCandidate | Interactions, Gamerules | The small borg loadout adjustment should be checked against RMC's round-start module policy.
2018 | 9206ad9a64737c1899b09fb9c7da5a927472e766 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2019 | a0817cdbb3e9a8925d9399ff9104ee1c1de2289b | Fix ColorExtensions math (#41717) | PortCandidate | — | The corrected sRGB/OkLAB conversions are self-contained client math, gated only by CMU's pinned RT color API.
2020 | 4d76c130516ac3f3a990f42c785ea65596677b52 | Rollersakes heisen bounty (#41859) | Superseded | Gamerules | The reward adjustment is replaced by the target-final removal of the rollerskates cargo bounty.
2021 | 42e7bad3dfd62ff7484d838e568a036d359866c1 | Fix news console formatting and pda news formating (#41799) | PortCandidate | Interactions | The rich-text and news UI formatting corrections are focused, subject to CMU's retained markup controls.
2022 | 6c9ef19e9e83d280ccf9253d85b43d865c2bfaa1 | Remove most unknown shuttle events (#41860) | Superseded | Gamerules | Index 2028 restores these shuttle-event maps, so this deletion has no target-final standalone behavior.
2023 | 165d5ecc2ed6c49b3d20958b1ffaf4e0f06adb06 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2024 | 5f6c08e1015acc8cf45ffb237e95e125b2b16e27 | ERT Overhaul 1/3: Apparel (#37855) | Deferred | Shooting, Medical, Interactions | The multi-part ERT apparel and asset overhaul must be reconciled as a unit with CMU emergency-response loadouts.
2025 | 2b08ae480d613052fd13aa39584ebd837e8a1952 | Adds crowbar to Mediborg Rescue Module (#41861) | PortCandidate | Medical, Interactions | The module tool addition is isolated, subject to RMC mediborg role and slot policy.
2026 | bfb0c1791e8ef557cbf93cd9987300ff88c03399 | ERT Overhaul 2/3: Equipment (#38105) | Deferred | Shooting, Medical, Interactions | The equipment and chemistry-container overhaul depends on the other ERT phases and CMU loadout policy.
2027 | 1e01a1ed6bbacbd2274a3110033081027726f2d3 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2028 | c65dba54b390d1b7cd6715f286f3456dc3c58657 | Revert "Remove most unknown shuttle events" (#41862) | Superseded | Gamerules | This restores the state before index 2022; the canceled pair contributes no independent target-final port.
2029 | 83e1a6a8eb4b992f2ed71eb83f814786f7d9deaa | Prevent Initial Infected from rolling on evac (#41863) | PortCandidate | Gamerules | The one-field round-end exclusion is a focused event-selection safeguard.
2030 | d53fe69b863a33b7146d6ec22154e8d1f9910649 | Repairing borgs now takes multiple doafters (#41638) | Deferred | Medical, Interactions | The repairable lifecycle and do-after rewrite crosses RMC's borg repair paths and needs compatibility testing.
2031 | d602541c1d0a2eff2f08ae854e8650c89a0ae83d | Automatic changelog update | Irrelevant | — | Generated changelog only.
2032 | 2619bc47ef3849bd7c87bcea847ecc528994bf51 | Add tile atmosphere tests (#41228) | Deferred | Physics, GameTicking | The large atmos test maps and harness depend on upstream LINDA and integration-test infrastructure not yet reconciled.
2033 | b17ee1c882cc7b46304773d80797c1baffd1f13d | Ignite atmosphere on explosions (#41262) | Deferred | Shooting, Physics | Explosion-driven tile ignition changes explosion processing and atmos coupling, both divergent core paths in CMU.
2034 | 61bf74e4e3e289e9217a273f2680a042592773ef | Automatic changelog update | Irrelevant | — | Generated changelog only.
2035 | 574cdf9c4ca752f00dd40f116ec30540e149d6be | Add myself to atmos codeowners (#41869) | Irrelevant | — | Upstream repository ownership metadata only.
2036 | a0e7fe8233c0ebb7fd2e1de28787727804732250 | Exo - Exomas Version (revertable) (#41715) | Deferred | — | The seasonal Exo map rewrite and its later target revert require target-final CMU map reconciliation.
2037 | 5e963250019d3191e65919fc6725ca12e08e4f5e | Automatic changelog update | Irrelevant | — | Generated map changelog only.
2038 | 10b989e7bfa84e9991b6653b46c5001c97640f40 | Cryogenics evenheal + New chem "Arcyrox" (#41696) | Deferred | Medical, Chemistry | Arcryox and cryogenic even-healing depend on the deferred damage-distribution API at index 2014.
2039 | 971d4efca83615bae0aa42459e334e761d413681 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2040 | 0fb6f26edb4017ffc31569aecd6459790452c3c6 | Xenoborg door control module (#41546) | Deferred | Interactions | The 19-file access-overrider, remote, UI, and xenoborg module feature crosses RMC silicon and access architecture.
2041 | 7d0f1b335bc4038debc732ecf4135702a8e1c9b5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2042 | 49743515ee03fc6b97b768f39d62aec9aa605a5a | Remove unused tags tied to unused entities (#41366) | Deferred | Shooting, Interactions | The broad prototype deletion must be filtered against CMU and RMC entities that may still consume the removed tags.
2043 | 8fba285cdb8e1a1062260174ef7186c4d856a211 | Add audio collections for Weh, Hew, and Honk to Vulps (so that they have audio when they do that) (#41610) | PortCandidate | — | The speech-emote collection assignment is an isolated species audio fix.
2044 | 619f807b97c5528e2f38ccc7f5dabb0ea8ff22bd | Automatic changelog update | Irrelevant | — | Generated changelog only.
2045 | 45eb268247405f2cefbde748986a9a0827dc0bc0 | Fix mothership core fixture (#41745) | PortCandidate | Physics | The one-prototype collision-layer correction is focused, subject to CMU's mothership content availability.
2046 | ab64807e2ce04f111f7dbc90d45197ceb83d76ce | Station AI now rolls before most standard crew (#41663) | PortCandidate | GameTicking, Gamerules | The job ordering adjustment is small and should be reconciled with RMC role-priority rules.
2047 | 5e907889d93587232d12bcb6a1364761b7c97b82 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2048 | 186b6460c7211b129b8369e8e2aee2d69af31d22 | Add foolbox (#41473) | PortCandidate | Interactions | The toolbox content feature is bounded, though its storage, toy, and loadout prototype IDs need validation.
2049 | 13638bd65508458b3c5c9465178f9cbecdf9ee44 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2050 | 870b8db07612b73d9d1dbb47b94bb89f6a9e8fac | Tweak Killer Tomato Size (#35866) | PortCandidate | — | The prototype scale adjustment is isolated and can be compared directly with CMU's killer tomato.
2051 | 110b8e16dd3f38cc9776b82f332ecddb35fb2db7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2052 | b0b88b216d146e9a401345f058bc7b5d11742d83 | Small improvement to suit sensor update loop (#41872) | AlreadyPresent | Medical, GameTicking | CMU already advances the sensor deadline before station assignment checks, preventing repeated processing of unassigned sensors.
2053 | 2a596d283c47e9223d58c1af4a1a150f12fd72f0 | Decouple standing state and drop item behavior (#41566) | Deferred | Movement, Medical, Interactions | The standing, stun, body-part, slippery, and buckle event changes cross several RMC-divergent mobility paths.
2054 | 20600ab700cc56f94fcc8dcfc0d34e11f4304d5e | Automatic changelog update | Irrelevant | — | Generated changelog only.
2055 | 4643bb8bbb4969a12630522b9dc81bcc0a6040e0 | Arcryox Metabolism Fix (#41881) | Deferred | Medical, Chemistry | The metabolism-rate correction belongs with deferred Arcryox and even-health integration from index 2038.
2056 | 5facf93b4af88c4fae5a7cf165706565b6133087 | fix AI battery alert (#41879) | Deferred | Interactions, GameTicking | Alert inheritance and server-owned AI battery state depend on the later battery-component unification and RMC silicon reconciliation.
2057 | 41042fcfb78b83f9d4f595b32484853795ce5dc3 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2058 | 19126949c37ae7328c6f7c091848cdaebad3809d | wallmount debug overlay (#38495) | PortCandidate | — | The client overlay and command are isolated developer tooling, gated only by local permission naming.
2059 | f079ec6233eeb4dd0db1db87487014e539d7524f | Fix destructible benchmark OOMing (#41880) | PortCandidate | Medical | The benchmark batching fix is isolated test infrastructure for the shared damage/destructible path.
2060 | 4aa7a963dc3dcebd4813f9d1e28b10c22f1402e6 | Prevent Vestine and all other Botany chemicals from affecting all seeds. (#41883) | PortCandidate | Chemistry | Passing the target seed into botany entity effects is a focused correctness fix, subject to CMU's older effect signatures.
2061 | 2ef64bd5cc761d7864149ce9c30629aed9a6366b | TriggerOnIngested (#41875) | PortCandidate | Medical, Interactions | The two-file ingestion trigger is bounded and can be adapted once its retained consumers are identified.
2062 | 2455dbbdb093006e7ef0516869c9001214522c33 | Remove flammability mass (#41803) | Ported (CS-0229) | Physics | The non-hard fire-overlap fixture now has zero density, so adding flammability no longer changes an entity's physical mass while collision-based fire transfer remains intact.
2063 | 511b66df0f95aaf7649b537b5ae117d7ca7dd6b1 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2064 | 4484f0f3514363804343a5443509e8b5b2fc8b31 | Fix prototypes so they pass analyzer checks (again) (AGAIN) (#41882) | PortCandidate | — | The five serializer and inheritance corrections are small and can be selected against the prototypes CMU retains.
2065 | 0e76d4e5ed6ae70a31d60beb350b7ce3c1a2f9b7 | Metabolizing bloodstream (#35071) | Deferred | Medical, Chemistry, GameTicking | The 42-file metabolism, bloodstream, smoke, analyzer, and reagent rewrite is a major medical-core migration.
2066 | 30322514445215774d3486b3886860a9361125ea | Automatic changelog update | Irrelevant | — | Generated changelog only.
2067 | 97508e81a8da0eac59b036579aa06f95baa926db | Update nix dev env (#41886) | Irrelevant | — | Upstream Nix development-environment metadata only.
2068 | 3c15d9f312db4543ff6b10ff502ae392bf36eafd | Adds BallisticAmmoSelfRefillerComponent (#38537) | Deferred | Shooting, GameTicking | Predicted ballistic self-refill changes the shared gun update loop and must be reconciled with RMC ammunition providers.
2069 | 31c91ce342b8e79cdf5bc88cf2306b0a6fa74d0c | Automatic changelog update | Irrelevant | — | Generated changelog only.
2070 | 926a81abe511a8b60deffaec375a468754d89296 | Give Vulps "Unique" Stomachs (#41893) | PortCandidate | Medical | The species organ and body-prototype addition is localized, subject to CMU's retained body schema.
2071 | e9ecdeec650d53186402e94664a538022ccdb7b2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2072 | 1bb4b935af3d9898bed2c8a20a19c8d2940a18b6 | Fix xenoborg modules (#41625) | PortCandidate | Shooting, Interactions | The three prototype-file fixes are bounded but require RMC xenoborg module and lathe validation.
2073 | 13ddce2a09c50119cc1a311ffcc274c611a8bc78 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2074 | c93fea42dd52f704f413d31da7eefdb3c3520d3c | [Bugfix/Optimization] Metabolize Foreign Blood (#41892) | Deferred | Medical, Chemistry | Foreign-blood metabolism changes bloodstream events and exclusion rules on top of the deferred index-2065 rewrite.
2075 | b4fa6f4a07a1cf6f1871cebaaa3d677ef94f7f8c | Fix loadout entity names not being exported/imported (#41891) | Ported (CS-0228) | Interactions | `RoleLoadout.EntityName` is now a data field, so customized loadout entity names survive preference export and import.
2076 | a1f4ea8365905b0d1e58c088270ad84850fbd75b | Automatic changelog update | Irrelevant | — | Generated changelog only.
2077 | ecd876cab9647a8b741706d7cefbbd75c9bc5ef3 | Mirror contrib guidelines to GitHub (#41896) | Irrelevant | — | Upstream contribution-process documentation only.
2078 | 4fe48ec3cc868ef7bb8d42b3228664fdb3bae10f | Adds debug wizard's grimoire (#41900) | PortCandidate | Interactions, Gamerules | The debug-item prototype adjustment is bounded and can be checked against CMU admin spawn policy.
2079 | 89b25adf52d908fcec7810c8597b4b3197c73e36 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2080 | bb95787af77cc9491998749a61806fa957d4cdcc | Make StaminaModifier into a status effect, apply to Hyperzine (#41902) | Deferred | Movement, Medical, Chemistry | The status-effect, stamina-threshold, and reagent migration changes core movement and medical state and requires RMC compatibility work.
2081 | 55fef2ab2edae20bfaca8aa7b68126cbb8d05b64 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2082 | ccc70aef07f0765eb78f69cae5099ecd156cb853 | Re-work Arrivals Shuttle to have un-interactable substation and APC (#41884) | Deferred | Interactions, Physics | The large generated arrivals map rewrite and new power prototype need target-final CMU map reconciliation.
2083 | a095c61ba49f046c6f846ab4ef87f06ccc573c4b | Automatic changelog update | Irrelevant | — | Generated map changelog only.
2084 | a21983d5aac19eda4c8773b799aa6b8684a13653 | Syndicate Wall Lockers and Secure Storage (#33251) | Deferred | Interactions, Gamerules | The 19-file storage hierarchy and asset feature needs target-final contraband, access, and CMU content reconciliation.
2085 | 24887dc7d52fb799a879258358ef3bce725e8fb7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2086 | 2b356f64bdfa4f8d444c7999f23b534fd48f5025 | Rebalance the Ghost Role Raffles (#33157) | PortCandidate | Gamerules | The raffle-weight changes are isolated configuration, subject to CMU population and ghost-role policy.
2087 | fcf82072193a90f9301813002bb72edbc6b535c4 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2088 | 1f2d80297cb81e8dbbd1c1f46aeb531a2624204c | feat: RnD tech research console now have reroll feature (#32931) | Deferred | Interactions, Gamerules, GameTicking | The ten-file research UI, server state, and technology-selection feature crosses CMU's divergent research progression.
2089 | d88bc489ae185ba2afa230d86e9acef1305e1a60 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2090 | 444991fbd0ca1b26e4acf4279c20e57a3a4e5b01 | Cleanup of circuit tote / stamp box prototypes + added small cardboard boxes as a general item (#41335) | Deferred | Interactions | The storage graph, fills, and prototype hierarchy rewrite needs reconciliation with RMC container content.
2091 | c97ffb006e57990a520d8ab8b8c3f9eb6b7f4a45 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2092 | 3266c94eac0dea5bdfc4b8cd814ed7b0a980b580 | Unify BatteryComponent and PredictedBatteryComponent (#41867) | Deferred | Shooting, Interactions, Physics, GameTicking | The 74-file battery unification is a major predicted-power migration affecting guns, machines, tests, UI, and RMC silicon code.
2093 | e552736422d5d6898b8bfeac65815fa57c44dcc2 | Shield QoL + buff (#41326) | PortCandidate | Interactions, Physics | The shield modifiers and examination text are a focused balance and feedback change.
2094 | 386115a5756b58ddc08f611c2a43364300ae266a | Automatic changelog update | Irrelevant | — | Generated changelog only.
2095 | 092f0f8b4a01092ca12726ceca2080409a61fea6 | Snowball update (#41908) | Deferred | — | The generated station-map rewrite needs target-final CMU map reconciliation.
2096 | 2c5b023dc1b7497a124d4ef4eb1627a2fcef3ed6 | Automatic changelog update | Irrelevant | — | Generated map changelog only.
2097 | a9bb4921a28f8d65fe2366f043bfd6ef2e6d9531 | Station AI ghost role (#40607) | Deferred | GameTicking, Gamerules | The station-AI ghost-role lifecycle changes role toggling, AI state, and silicon prototypes across RMC-divergent systems.
2098 | dcd083a25b66e2dcd0c215cd2b149878fa1033ce | Automatic changelog update | Irrelevant | — | Generated changelog only.
2099 | e2ef727096a141706fbdf217d3ec24698731f0c0 | Log Station AI radial actions (#41911) | PortCandidate | Interactions | The radial door-action audit logging is focused, subject to CMU's retained Station AI action implementation.
2100 | 000c2e9b5d7e6baf855571429ed218677d24a066 | Automatic changelog update | Irrelevant | — | Generated admin changelog only.
2101 | 77036e8cdde1878d396a24ac45c74a81d2787a01 | Added sprites for openable ingredients (#41923) | PortCandidate | Interactions | The ingredient open-state sprites and prototype layers are a bounded presentation change.
2102 | c179445ec9f7a44ebf559235ea9bc66ef600aeb2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2103 | 79f58a0314031b852d616a7f0719371ded6bcf8f | Don't process paused MoverControllers (#39444) | Deferred | Movement, Physics, GameTicking | Active-mover tracking and pause lifecycle substantially rewrite RMC-divergent mover controllers and relay ordering.
2104 | 517b37698d3983a82a5aaf26edcd7dc432974e8f | Staging -> Master (#41929) | Deferred | Medical, Chemistry, Shooting, Interactions | The effective first-parent delta is 7 files, +59/-40, spanning bloodstream, magic, and gun behavior that needs selective reconciliation.
2105 | 6932f2819136e570e8e763f53c05d1920071d9fa | Merge Injector & Hypospray Systems & Components (#41833) | Deferred | Medical, Chemistry, Interactions | The 24-file injector/hypospray component, UI, event, and prototype merger is a major chemistry API migration.
2106 | 85060d96cf087dba3a80d36fadda7467ad03df62 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2107 | 2cb8e9b7fed4cda4e94c2a3437fa18ed647c9587 | Update Credits (#41931) | Irrelevant | — | Upstream contributor-credit metadata only.
2108 | 56bff9aee9f2bf0d0a41774e564e81f9d77bf302 | Fix the mothership again (again) (#41924) | Deferred | Physics | The generated mothership map rewrite needs target-final CMU map and collision reconciliation.
2109 | eefecdcf2f6d8f91c13174b8000ba1dfef5f2d55 | Stable release for 2025-12-20 (#41934) (#41935) | Irrelevant | — | The merge has an empty effective first-parent delta.
2110 | f3f91e3f6b67530953a397bea0409389f9a4d673 | Miscellaneous Injector fixes + BorgHypo fill sprites. (#41932) | Deferred | Medical, Chemistry, Interactions | The follow-up fixes and fill-state assets depend on the deferred injector merger at index 2105.
2111 | 0a6ad5dcff58a8366d97d35816c334ecb76abeea | Automatic changelog update | Irrelevant | — | Generated changelog only.
2112 | 7750e3ca2e54fde0fb10d2456c41f6b121112a9e | Rename LOOC chat to Help chat (#41933) | Superseded | Interactions | The pinned target later restores LOOC wording, so the intermediate Help-chat rename is not target-final behavior.
2113 | fab0fe14ccbd732d4648feb23e8c957535fc1ec3 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2114 | 787330f5c6c256c53c2ed6f9b0909ab4265476a5 | v269.0.0 RT update - .NET 10 (#41855) | AlreadyPresent | Physics, GameTicking | CMU already targets .NET 10 and carries a later pinned RobustToolbox synchronization, subsuming this platform-floor migration without touching the engine submodule.
2115 | eb41d5010b1ae9d517b258e087d43e6663d775d3 | Physics Assert in SharedMoverController (#37970) | Deferred | Movement, Physics | The assertions and movement-query changes depend on upstream mover and v269 physics APIs that diverge from CMU.
2116 | f59ef4b986bba91c5755c9dc6fcdf9a82581e2fe | fix solution contents duplication on spill behavior (#33231) | Deferred | Chemistry, Interactions, Physics | The six-file spill, puddle, pressure, and destructible rewrite crosses RMC fluid and solution ownership paths.
2117 | 853570662ea781818030a46ffc9d0928e09fe82d | Automatic changelog update | Irrelevant | — | Generated changelog only.
2118 | b436e2a937235c72c317cdf3be7fbc4cab3048cd | Fix missing scrollbars in Admin Player List window (#40525) | PortCandidate | Interactions | Invalidating cached item height when the row generator changes is an isolated UI correctness fix.
2119 | 347a728ab7d58f2a493cdc155dcd727599f8e881 | Automatic changelog update | Irrelevant | — | Generated admin changelog only.
2120 | dde01f746f81e3381f3a16f2eae560d699ffe3f7 | Basic Dynamic Power Consumption Systems (#41885) | Deferred | Interactions, Physics, GameTicking | New networked power-state components, systems, and integration tests require broad machine-consumer migration.
2121 | 8b8f621b8c16da663596b60153e60c8a54b1678d | Allow cable coils to be destroyed (#41279) | PortCandidate | Interactions, Physics | The destructible prototype addition is isolated, subject to RMC construction-material behavior.
2122 | 28fd00b7ea503674fdabe9ad8f0276d35ee956c3 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2123 | 760463a67a8b06e0fad30e30c36530e988f04e44 | Port FTL arrival effect fix from https://github.com/new-frontiers-14/frontier-station-14/pull/3495 (#41951) | PortCandidate | Movement, Physics | The docking offset and effect-prototype correction is focused and can be compared with CMU shuttle arrival behavior.
2124 | cabf9d51246ba29a516ca3339c48ca7fe9e8afc8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2125 | 5363a9f2faa047611c1d043f4028e5fa229e17fc | Update debug backpacks to use the proper suffix (#41959) | PortCandidate | — | The four prototype suffix corrections are isolated content cleanup.
2126 | 3a3707d2a2283ebb3f202708c98f9ba52ee499a7 | Fix Setgamepreset (#41963) | PortCandidate | GameTicking, Gamerules | The one-line preset command argument correction is a focused administrative fix.
2127 | 0ed5619e8bd85d39e3d7f635a859d9984da6bd9e | Fix atmos devices not correctly reffing the changed atmos (#41585) | Deferred | Physics, GameTicking | The seven-file atmos-reference rewrite changes processing, monitoring, commands, and integration tests on a divergent LINDA base.
2128 | c47f3ca9067387096bbdd8a3cb8c1082e54b6d8f | Automatic changelog update | Irrelevant | — | Generated changelog only.
2129 | 38d6b7a119c2e83389ba57c8d30df554bb1efa13 | Fix DeltaPressureTest race condition when using LINDA (#41388) | Deferred | Physics, GameTicking | The test rewrite depends on the target LINDA scheduler and atmos harness not yet integrated.
2130 | 9f84b2473307965b02d8f5d2ab48d5b664b9f9b2 | Use cached Atmospherics AirtightData when applicable (#41390) | Deferred | Physics, GameTicking, Gamerules | The cached airtight API affects atmos, anomalies, heat exchangers, and game-rule utility code and needs a coordinated pass.
2131 | d601ed5f4aeccd31fe7e1b0bbf3cb1a641a9d310 | Make donk co. microwave syndicate contraband (#41960) | PortCandidate | Gamerules | The two prototype contraband-category changes are isolated economy and role-policy data.
2132 | ad6644afd4f558145fb4a149173a1d3b88620a0b | Automatic changelog update | Irrelevant | — | Generated changelog only.
2133 | 860f1418cdccb5a1d1ce120c2514faf7223fe88e | Fix incorrect table breakage sound (#41968) | PortCandidate | Interactions | The table sound-specifier correction is an isolated prototype fix.
2134 | 229c08c5605bc970e8ebdc195de84d52d82b2e32 | Fix the Infinite Spill (#42022) | PortCandidate | Chemistry, Interactions, Physics | The focused puddle guard prevents a solution from recursively spilling into itself, subject to local fluid API names.
2135 | 402cc654773b74ac0d8fdd778cfd7abedd9dcb0c | Change "mafioso" (singular) to "mafiosi" (plural) in the Italian accent. (#42026) | Superseded | — | The pinned target later removes the Italian accent data containing this intermediate wording fix.
2136 | 503052bca7b5b78aed783d001149eb6553196656 | Fix spreaders not re-spreading on deletion (#42016) | Ported (CS-0227) | Physics, GameTicking | Neighbor activation now checks each anchored spreader rather than the grid entity and skips only the terminating origin, so deletion correctly requeues same-tile and adjacent spreaders.
2137 | dbda861cade19c83f64be5ef405efb03d93934b4 | Change Botany Minimum Quantity For Random Chems (#41955) | PortCandidate | Chemistry | The minimum random-chemical quantity adjustment is an isolated botany balance fix.
2138 | 428df6a58a387757f65de4b8d81a713506a73f4c | Add botany equipment to marathon brig (#42028) | Deferred | — | The generated Marathon map rewrite needs target-final CMU map reconciliation.
2139 | ff1cba2949978295a9b50d25dd024a054e23729b | Automatic changelog update | Irrelevant | — | Generated map changelog only.
2140 | 92ee561f4b5a3a79f0f50613c6777898f5f51877 | Update RT to v270.0.0 (#42029) | Irrelevant | — | This changes only the upstream RobustToolbox pointer; CMU's pinned engine synchronization is tracked separately.
2141 | 9511285508133839a11c5f6a14ab7cf61b558060 | Fix NanoTask and bounty print formatting (#42030) | PortCandidate | Interactions, Gamerules | The two string-formatting corrections are focused and can be adapted to local cargo and cartridge APIs.
2142 | dd22d58f2d923bf6abf7347a4cfc27735270fb98 | Change "pappa" (food) to "papà" (dad) in Italian accent (#42018) | Superseded | — | The pinned target later removes the Italian accent data containing this intermediate correction.
2143 | 7ac84d1acb285fcb5d2e5732d1739672122c5fe1 | Fix greytide terms in Italian accent (#42020) | Superseded | — | The pinned target later removes the Italian accent data containing these intermediate replacements.
2144 | c6a4d3f7d8938ba037e9057d02ac1de6f7363435 | Clarify checkbox formatting in PR template (#42035) | Irrelevant | — | Upstream pull-request template documentation only.
2145 | 6f38eed9d9b056ce6b4bcba2f8693cf889cdd65b | Splits temperature damage processing into its own component (#30515) | Deferred | Medical, Physics, GameTicking | The 27-file temperature component and processing split touches damage, zombies, atmos, and RMC temperature consumers.
2146 | e197b7f9ad57ccd92e3db21c3f54d9850a1fd514 | stable to master (#42038) | Deferred | Interactions, Gamerules | The effective first-parent delta is 2 MMI/borg files, +7/-42, and needs RMC silicon lifecycle reconciliation.
2147 | cdc0c35f3f49bccce2207da57cb001e39a30a3ce | AddMolsToMixture atmos helper (#42033) | Deferred | Chemistry, Physics | The new atmos-mixture helper and tests depend on the target LINDA gas API and scheduler semantics.
2148 | 0444987d5037d0390b929ab1013a14a50b1ff807 | Fixed Voice Mask and Ripley APU interaction (#42023) | PortCandidate | Interactions | The one-system equipment-slot check is a focused mech interaction fix.
2149 | 92413255062af8a3555ddf1cce3d566da089b1a4 | Update dotnet sdk from 9 to 10 for nix devl shell. (#42041) | Irrelevant | — | Upstream Nix development-shell metadata only.
2150 | 2d77e48b4c3d92d371eedc41cc0295a8e017ec09 | Add jet injectors (#40076) | Deferred | Medical, Chemistry, Interactions | The 25-file injector modes, UI, audio, prototypes, assets, and loadout feature depends on the deferred injector merger.
2151 | 3ecc3cb295f134e95a708989cafd346588a3b905 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2152 | 6129fbe98e4f10759b73ea29e602acdab1e3b037 | make comp-repairable-repair sane (#42048) | PortCandidate | Interactions | Replacing hand-built article selection with Fluent's entity helper is a focused localization cleanup.
2153 | 8fab0ccb585d58d2105c024c44380dbb6f40059d | Remove reverted shuttle event change from the changelog (#42065) | Irrelevant | — | Upstream changelog cleanup only.
2154 | 2f0d3476129ece976f3de1d68b172b87049440e1 | Fixed Xeno air alarms warning/danger sprites not showing (#41590) | PortCandidate | Interactions, Physics | The air-alarm layer and two sprite-state corrections are bounded xeno atmos presentation fixes.
2155 | 2182c7be705915364e5c285e79ef781649f584dc | Automatic changelog update | Irrelevant | — | Generated changelog only.
2156 | 4ff7411fb786c1288db1d2feab080729d2551018 | Voice mask effects are toggleable and hide your accent (#41965) | Deferred | Interactions | The 12-file voice-mask UI, speech, accent, action, component, and prototype rewrite crosses RMC identity systems.
2157 | caebc10c5d8282573ae099e1d3f914cf630a7c7a | Automatic changelog update | Irrelevant | — | Generated changelog only.
2158 | 8b33f4734f4e53166948bbe06d50290605423955 | Fix Kitchen Spike Paralysis by removing an unused subscription.  (#42078) | AlreadyPresent | Movement, Interactions | CMU's retained kitchen-spike implementation does not subscribe hooked victims to the cached movement event, so the paralysis bug is absent.
2159 | 2aa29de1eeb8d38b649f809d902b728fd49221e5 | Energy guns' fire mode text formating fix (#42103) | PortCandidate | Shooting, Interactions | Separating examine markup from popup text is a focused battery-weapon feedback correction.
2160 | abeeb910fb1c5639f6c6cc7134dff583f88708a4 | ERT Overhaul 3/3: Loadouts (#38481) | Deferred | Shooting, Medical, Interactions | The final ERT loadout and asset phase must be reconciled with indices 2024/2026 and CMU emergency-response policy.
2161 | 339b28740accb0a79d9e84bcf53de1f8c8af1e4d | Automatic changelog update | Irrelevant | — | Generated changelog only.
2162 | 8313a4e3105d69a496261cddcffcc6894a053bbe | Atmospherics/Temperature HeatContainers (#39997) | Deferred | Physics, GameTicking | The six-file heat-container math substrate is a new atmos and temperature architecture that needs dedicated parity review.
2163 | 589b187499bf98c795dd8d06691f96d8b96d87db | Lowered Xenoborgs MinPlayers From 40 To 30 (#42111) | PortCandidate | Gamerules | The one-field population threshold is isolated configuration, subject to CMU event policy.
2164 | 1b3047644afdbe61b2112d730904820d3604c1ab | Automatic changelog update | Irrelevant | — | Generated changelog only.
2165 | cd8d5a6a9c629fb75f5e45dc347e04ce64b79db3 | Cleanup warnings: CS0414, CS0618 (#42068) | Deferred | Interactions, Physics, GameTicking | Mixed warning cleanup removes or changes fields across construction, launcher, chat, traits, join flow, and spray code that diverges in CMU.
2166 | ee2f1da8c2cda4a52c4d3784cf46c6f2f79fcf91 | Merge IFF controls into one control. Make syndicate IFF turned off by default.  (#42104) | Deferred | Interactions, Physics | The IFF UI, event, component, and shuttle-system consolidation changes retained vessel-identification behavior.
2167 | e1da70ebf7b4db6ef80101eb57493a4e916938b4 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2168 | cf2596118626491a799b16808a852b9b71ed558f | update communicator kit description for voice mask implanter (#42115) | PortCandidate | Interactions | The one-line kit description correction is isolated, subject to CMU thief-kit contents.
2169 | 662d2ee964e934138ab945bde64fc390454e7db2 | ReagentGrinder Comp and API to shared (#41956) | Deferred | Chemistry, Interactions | Moving grinder state and APIs to shared changes prediction, UI, component ownership, and RMC kitchen consumers.
2170 | 6506c7786f33fe70e2674935a8fcdf0307dc27ea | Update Credits (#42127) | Irrelevant | — | Upstream contributor-credit metadata only.
2171 | 01e583f500fd9d35f9df5a7b2a2a037169eafd92 | Fix broken vending machine UI behavior (#42110) | Deferred | Interactions | Moving breakage and UI-close handling into the shared vending system crosses RMC vending and prediction architecture.
2172 | e8dab47f8971bb157cae56b1e9a46d87d65b1b90 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2173 | 45fa4114853435d851c8639bafeebd375bcc2390 | Add crayon box to Big Bite meals (#42077) | PortCandidate | Interactions | The meal-container entity-table adjustment is bounded, subject to CMU food prototype IDs.
2174 | 129c56544e068682a2326638a96a45a6d4d871d2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2175 | 645c2494ec68b71c20627dc65d69b782b4d614c3 | optimise shuttle collision entity throwing (#40984) | Deferred | Movement, Physics | The impact lookup and throw-selection optimization changes a hot shuttle-collision path that requires RMC physics parity testing.
2176 | 4cf18a222b382b3731d3c287f51825675bb2f631 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2177 | 0dcb2756c7195681b80f861b6555b32988fa64c2 | Add `AtmosTest` test assertion for a valid grid (#42139) | Deferred | Physics, GameTicking | The harness assertion depends on the upstream atmos test substrate and grid-component layout deferred at index 2032.
2178 | 077dceeb2df026f5182a5b77447d7c3b795aa476 | Delete MetabolismMovespeedModifierSystem (#42134) | Deferred | Movement, Medical, Chemistry | Removing the legacy metabolism speed system depends on completing the status-effect migration at index 2080 and auditing RMC reagents.
2179 | 951f13fd699f06c08831ea4fca5c0f431fcdc9ec | Add antag control for the space ninja (#42133) | PortCandidate | Gamerules | The admin antag-control verb and localization are focused, subject to CMU ninja role ownership.
2180 | 131108b018cd7155351bcabdec1f90383f7f423e | Fix plasma station comms apc overloaded by default (#42144) | Deferred | Interactions, Physics | The generated Plasma map rewrite needs target-final CMU map and power reconciliation.
2181 | be7653c131ed157f8fba9a8da485410cf6524ec5 | Automatic changelog update | Irrelevant | — | Generated map changelog only.
2182 | ac3a91eac107637b962698527638e08716edaa17 | Fix possible bug in my fix of IFF console. Add documentation to HideOnInit. (#42122) | Deferred | Interactions, Physics | The IFF initialization fix depends on the deferred control consolidation at index 2166.
2183 | 552938cda20dd4ec0f34a1d2e0c22d862b0eba9c | puts Space ninja survival box contents into their bag (#42102) | PortCandidate | Gamerules, Interactions | The three prototype changes are bounded and can be checked against CMU ninja loadouts.
2184 | df7473a05805974bbf3d1a2df79ec6ff9dbb4bc3 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2185 | beb4d5f5844b65aa33b801441327e828af2db01a | Remove syndicate bomb restock time (#42114) | PortCandidate | Gamerules | Splitting the fake-bomb listing by uplink type is a focused economy and role-policy change.
2186 | c6c84ef17db9a40458ac46c5f89df53611237cf5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2187 | fb17257562a99a54f314aa266b0ad41efb4b97b2 | Ammonia restores Rat King Bloodlevel (#42167) | PortCandidate | Medical, Chemistry | The reagent effect and conditions are localized, subject to RMC metabolism component names.
2188 | 56462d0cb19df5559603d2dda85639e9edef66bd | Automatic changelog update | Irrelevant | — | Generated changelog only.
2189 | d61ecd3d50d71d6e38e0642e94181a18215c59ac | Align detective stamp with rest of stamps (lower by 2 pixels) (#42177) | PortCandidate | — | The single sprite alignment correction is isolated asset work.
2190 | 3cc79c223a243317446578982d2a4367773e5ce8 | Chemmaster Pill Source (#40121) | Deferred | Chemistry, Interactions | The seven-file UI, server, and shared message feature changes pill-source state across CMU's divergent ChemMaster implementation.
2191 | a41101e8daaf10965770daa79f2458e350d8f2c8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2192 | 0b1e8a4bbc091ca07a11b3586808635a47b06445 | Status Effects Toolshed (#41670) | Deferred | Medical, Interactions | The toolshed command and completion parser depend on the new status-effect architecture not yet reconciled with CMU.
2193 | 41234b7eb1500130a305e6e6a1cf095929b22112 | Move borg module remove button to the left side (#42119) | PortCandidate | Interactions | The one-line XAML control-order change is isolated UI work.
2194 | cdf64617962339a37326dfe4bf690b1bfd852b2d | Automatic changelog update | Irrelevant | — | Generated changelog only.
2195 | dc9d4accfd3f8b98712a8fafea3a9f5ebd484d64 | Fix warnings (#42175) | AlreadyPresent | — | CMU does not contain the upstream chat-notification prototype declaration that produced this redundant prototype-name warning.
2196 | 360e43b9e7687654c4917f1e72fa62aded30d78e | (Fix) Make paper extinguishable with fire extinguisher (#42142) | PortCandidate | Interactions, Physics | The reactive and extinguishable base-paper prototype additions are focused, subject to RMC paper inheritance.
2197 | 8514405ca9d751c38c05f52c0912da3d6d722cef | Automatic changelog update | Irrelevant | — | Generated changelog only.
2198 | 97a75f49c6b059024729d969ed7116551153389e | Damageable Cleanup + Bugfix (#42076) | Deferred | Medical | The TryChangeDamage return-value fix is useful, but the API and tests differ substantially in CMU's older damage system.
2199 | 2c5b67fc3f0f063e6039741b067876b03b9c28b5 | Ninjas now get a custom bag! (#42112) | PortCandidate | Gamerules, Interactions | The custom bag prototype and assets are bounded, subject to CMU ninja loadout policy.
~~~

# SS14 upstream inventory: wave 0006

Audit date: 2026-07-20

- Pinned baseline: `59633f6dc50e77dda8cefa344d87c7b01e06a810`
- Pinned target: `40ca2c7f90d11d27be5457d177c133f0947d1c08`
- Range: ordered first-parent commits, zero-based indices 1000 through 1199
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
1000 | aa828b96abb9ca7378659a9deb00e2b4c0872cc0 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1001 | f6cd8673d35a8aa7d89338289c8d8782098c36bb | Recharger tweaks. (#38138) | PortCandidate | Interactions, Physics | The retained slot whitelists, descriptions, and indicator sprite should be reconciled with CMU power-cell and RMC equipment inheritance.
1002 | 0678e3b4689fe8a116e400c707d5aaa64cedc4d6 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1003 | 0663576c4698fb0d0a139cd25b16d1bd3b84123a | Descriptions for .20 Rifle (#36496) | PortCandidate | Shooting | The retained ammunition and rifle descriptions apply cleanly but should be checked against RMC's separate firearm naming.
1004 | 4555b7260893c3b5c10dde6d1644a7d386bf447c | Automatic changelog update | Irrelevant | — | Generated changelog only.
1005 | ea3c44686ccf0140318ca2efd8df4744fd58ae03 | Xenoborg jammer now ignores xenoborg associated frequencies (#38005) | Deferred | Interactions, GameTicking | Frequency exclusions cross both radio and device-network jammer contracts and need focused reconciliation with RMC communications.
1006 | 705e4d3aa1e759f35c3e79e44d162df7f8ab2680 | Add "Lizard Visage" Snout Markings to lizards (#35294) | PortCandidate | — | The retained markings and texture assets need comparison with CMU's current reptilian customization resources.
1007 | 42786240ec81fe6feaf6976796b0573d76247edd | Automatic changelog update | Irrelevant | — | Generated changelog only.
1008 | d699a4e985374c6c624d6ef9ccecf75c0ac86dc5 | Xenoborg items are now highly illegal (#39856) | Ported (CS-0183) | Interactions, Gamerules | BaseXenoborgContraband now uses the separately integrated HighlyIllegal contract without reclassifying RMC equipment.
1009 | b2d09ba457fed0c80118fd40c9d60c428d3d7e31 | Rat King Refactor Part 0: Separate Rummaging from RatKingComponent. (#40530) | Deferred | Interactions, GameTicking, Gamerules | This entity-table and component split begins a wider Rat King refactor and must be integrated with the full retained chain.
1010 | 941b8668883e485a49d139ec47acb9ccda195c30 | Fland: Update the TEG (#40534) | Deferred | Physics | The generated Fland power-layout update requires target-final map reconciliation.
1011 | 708d412f3bb3f7fc589ae15aa84740954f415ce1 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1012 | 0a0bcbe164a1140b2ce4b4a41d5c9c90e5f1e295 | Random Instrument Collection (#40532) | PortCandidate | Interactions | The retained entity-table and cargo-fill changes should land with the later instrument-table fixes.
1013 | 09f24f2ba79f7dedf559e2a5fb8451cfa919dd8d | Automatic changelog update | Irrelevant | — | Generated changelog only.
1014 | 5e55974c0e29644c5fca7956b96eecfa9e0c4a59 | fix out of bounds pixels on vox neck displacement map (#40495) | PortCandidate | — | The corrected binary displacement map needs a direct asset comparison before replacement.
1015 | 80947b128a8e127bfd1101924c4515fd9c587c9b | Add explosive cord. (#25875) | Deferred | Shooting, Interactions, Physics | The large cable, detonator, trigger, construction, research, and asset feature crosses divergent RMC explosive and storage systems.
1016 | 306b6179575577e00ce74c4126e33c0d4a4e181c | Automatic changelog update | Irrelevant | — | Generated changelog only.
1017 | 64c07d7266cf9fb56608d3c4bc7eb1c203bfc8c1 | Exo: Link engie airlocks (#40546) | Deferred | Interactions, Physics | The generated Exo airlock links need target-final map and access-network reconciliation.
1018 | a6041faf2c14e53f94e379af21d69e31f2a6d5c3 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1019 | 9fa17c51bd286d812cfbbfe2a0508acb3c0664ac | Admin smite: Homing rods. (#40246) | Deferred | Movement, Physics, Gamerules | The chasing-physics smite requires behavior and administration review against CMU's current smite and movement systems.
1020 | 5e65f5ca89571957d792f6a8af6d4a8b70ff472a | Automatic changelog update | Irrelevant | — | Generated changelog only.
1021 | 79878f01dde32bf46ce199e51323066a40f7dfa4 | Added white towels to autolathe and uniform printer (#40160) | PortCandidate | Interactions | The retained recipes and lathe pack membership need reconciliation with CMU's current lathe categories.
1022 | d52ebb215dd4f4be976a229e68cac13de8221489 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1023 | 4ea0d517cf8312bef89c2db21eaceea09d8a0881 | Fix Rollerbed sprites (#40550) | Ported (CS-0184) | Medical, Interactions | Strap occupancy now owns deployed-bed visibility on the standard rollerbed and both complete RMC visualizer overrides.
1024 | 27f281add5a608c3a90abbbba218fb63075278fa | Automatic changelog update | Irrelevant | — | Generated changelog only.
1025 | 3b4547571e4d81ba987ae095189636f9540f226c | [BUGFIX/CLEANUP] Edible Plushies and Clothes (#40276) | Deferred | Medical, Chemistry, Interactions | The broad edible/clothing prototype cleanup must be reconciled with RMC wearable, food, and ingestion overrides.
1026 | 094b230585d8389ff74bb15d0e180bdf714ce2e5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1027 | 1225ea4f03d8e274a913ae2f9087ce3ab0afd6c6 | Update engine to v267.2.0 (#40560) | Irrelevant | — | RobustToolbox is independently rebased and engine updates are outside this content inventory.
1028 | bd439131ea2411174d9b944c321488007a35386d | Fix instrument crate heisen test (#40558) | PortCandidate | Interactions | The retained entity-table correction should land with indices 1012 and 1041 so all referenced instrument tables resolve coherently.
1029 | dc3f5e35646ade3f507c4daa8956c897d877e254 | Make Closets Less Tanky Than Gun Safes (#35671) | PortCandidate | Interactions, Physics | The retained structural resistance split should be reviewed against RMC closet inheritance and destruction balance.
1030 | c7117f38ac4dc342f3584dce001e5689904c2756 | Add a sleep delay to Nocturine (NewStatusEffect version) (#40231) | Deferred | Medical, Chemistry, GameTicking | The sleep-delay effect is coupled to the newer status-effect architecture and CMU's divergent medical chemistry.
1031 | 5bb0ec878b9980dd4a298d68ab85281d26c7552b | Automatic changelog update | Irrelevant | — | Generated changelog only.
1032 | 522d10f579ebd0809abbe1a24113b986e0a267fc | Minor plushie.yml touchup (#40552) | PortCandidate | Interactions | The retained collision sounds and melee-animation cleanup need a narrow comparison with CMU's expanded plushie prototypes.
1033 | 388d66c046072aeae1397e27314f6f6e7728c4e3 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1034 | 871e7a1eaea2fe2b20064e32383e36440de549a8 | Vulp Plushie (#40303) | PortCandidate | Interactions | The retained plushie, assets, cargo fills, arcade reward, and food-sequence entry need adaptation to CMU's species and content layout.
1035 | abff932ca808ae83c1b1a607ad3e41efe4f5fb10 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1036 | 141d9031259e7012e2f9cb294ccae0c2cb83c162 | Stunned Status and Knockdown Meth fix.  (#39547) | Deferred | Movement, Medical, Chemistry, GameTicking | The reagent-wide status and knockdown conversion must land with the newer status-effect chain and RMC stun semantics.
1037 | a64000ddb3f3fc816110e29b4e8734d1bd8eac5d | Automatic changelog update | Irrelevant | — | Generated changelog only.
1038 | c0264406c7d95b7295cbaf33f66f8cb152979a8a | More Vox Sounds (#39914) | PortCandidate | Interactions | The retained Vox speech, gasp, and emote audio bundle needs asset and attribution comparison.
1039 | 345088dbc05b40b67d89e5de1f5798436d1945f2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1040 | a67aefb8a945a808638e79a13fdec3a16fd2ef31 | New GitHub issue templates (#39979) | Irrelevant | — | Upstream repository issue-template policy has no gameplay delta to port.
1041 | bd519192d1238ed5f690f7571cb4c951a6b41174 | Salv Instrument Spawn Rework (#40572) | PortCandidate | Interactions | The retained salvage table should be integrated with the instrument-table chain from indices 1012 and 1028.
1042 | d8172862c5174da21213bbb5d0e6b69d8bbbe4b2 | Edible Verb Fixes (#39933) | Deferred | Medical, Interactions | The utensil and digestibility changes target the newer ingestion architecture and require an RMC food-path adaptation.
1043 | 398c8df343e2b41ebdf03fe6d3714a1e894434c0 | Readds Tasers to Security (#39087) | Deferred | Shooting, Medical, Gamerules | The equipment, projectile, and stun bundle is balance-sensitive and conflicts with RMC's divergent security firearms.
1044 | bf90dd9c7377b4a9d840b48af5ce1676146bfd66 | Update Credits (#40586) | Irrelevant | — | Upstream credits metadata only.
1045 | a84caf5c8402a70c176d287a0ef49944e1d05865 | Added Vox Mime Hardsuit Sprite (#40567) | PortCandidate | — | The retained species-specific hardsuit sprites need direct asset and metadata comparison.
1046 | 1ba8f92d0c37a6a4cca017f78aeb24fb872bda9d | Make vulp gasps and deathgasps audible (#40579) | PortCandidate | Medical, Interactions | The retained audible flags need reconciliation with CMU's current Vulpkanin speech-sound definitions.
1047 | 3572d6b7c8c966c1d16542710e936e2168f14296 | Removes Taser Bolt Damage & Allows Tasers to be used by Pacifists (#40588) | Deferred | Shooting, Medical, Interactions, Gamerules | This taser and pacifism policy change must be reviewed with index 1043 and RMC stun balance.
1048 | fd5f9d7f604180cf4a4863ad22b512af805116fd | Empty commit (stable merge) (#40599) | Irrelevant | — | The effective first-parent merge delta is empty.
1049 | f2d43172588289917215c3044a4d9a74c2f3ad5b | Clean up some parts of ExplosionSystem (#40485) | Deferred | Shooting, Physics, GameTicking | The broad explosion, damage-modifier, airtight-map, and API rewrite needs focused reconciliation with RMC damage and physics.
1050 | 9932c2eed5339b963ee22453a94659e66cad13ff | Replace outdated tip 79 about artifact scanners (#40597) | PortCandidate | — | The retained replacement tip should be checked against CMU's current tip numbering and artifact terminology.
1051 | 974759ba79c2208a75f11e7ed9e9cbdc0774cdc7 | Fix label markup escapeing (#40600) | Deferred | Interactions | Safe integration requires removing RMC pre-escaping and preserving clone-safe label decoding rather than escaping stored labels in isolation.
1052 | 768870ac686196b946179f5e77959f623b0791a0 | Add logging for additional grilles (#40603) | Ported (CS-0169) | Interactions, Gamerules | Clockwork and diagonal grille construction now emits the same high-impact audit log as standard grilles.
1053 | f3202dcff920e56ca1820ead91bcc1f8ce1d6282 | Fix Shotgun ammocount not updating (#40568) | AlreadyPresent | Shooting, Interactions | CMU's RMC-adapted insertion path already updates ballistic appearance and ammo-count state after successful inserts.
1054 | 21a29212ab2c664ad016218bb2802ddace84e909 | Fix chess dimension smite (#40583) | Ported (CS-0167) | Movement, Interactions, Physics, Gamerules | The chess-dimension smite now preserves the victim's established PhysicsComponent while enabling tabletop dragging.
1055 | 52430df55f20578817df29638752debb219a4a0d | Make file dialog API usages read-only (#37779) | Ported (CS-0168) | Interactions | All three upstream importers and the additional RMC cassette importer now request read-only file streams.
1056 | 42c519b969d2ab1f1a288a75cc06ef888e52cdcf | Updated Elkridge burn chambers (#40590) | Deferred | Physics | The generated Elkridge atmospherics and power edit needs target-final map reconciliation.
1057 | 5255f96915b24b86ac332ceaa7cbbe3eb5d4d744 | Hit 'em with the Michaelwave (#40618) | PortCandidate | Interactions | The retained microwave in-hand and machine sprite overhaul needs an asset and metadata comparison.
1058 | a597c7f3a2bfe2410561887999695d6f040a64bb | Rephrases two Whistle descriptions (#40631) | PortCandidate | — | The retained wording can be applied after confirming CMU's matching whistle prototypes.
1059 | 7f8308f53357f5bec65fe85ab22608b486b1c53c | Fixes conveyer typos (#40627) | PortCandidate | Interactions | The retained device-link localization spelling corrections remain applicable.
1060 | b545dd67cae658d937efc25442a5d60a19295ef2 | Fixes a typo in the leaking SUPERPACMAN description (#40628) | PortCandidate | — | The retained scrap-description spelling correction remains applicable.
1061 | 1668e8cff6af38037eaa299f03ce6b3019f84ab1 | Two bounty typo fixes (#40633) | PortCandidate | Gamerules | The retained cargo-bounty localization corrections remain applicable.
1062 | 451968906ce60338b5218ad6879d57afaf4fcf7a | Minor typofix for the experiment plushie description (#40635) | PortCandidate | — | The retained experiment-plushie description correction remains applicable.
1063 | 569e785daa78724c4359b62cd5f264e710131829 | Typofixes and rephrases a bunch of job descriptions (#40634) | PortCandidate | Gamerules | The retained job-description wording should be selected against CMU's expanded RMC job locale.
1064 | be3c7c5ce42599102daab74dfc40742d805bdc0e | Minor change to the Hydra description (#40629) | PortCandidate | Shooting | The retained launcher-description clarification should be applied without altering RMC launcher balance.
1065 | a9e272a6cfa8b1fdde9b3d2f3e4fc0930c9740c3 | moved desoxyephedrine from ambrosia to glasstle (#40638) | Ported (CS-0185) | Medical, Chemistry | Desoxyephedrine production moved from both ambrosia variants to glasstle while preserving CMU's RMC reagents.
1066 | faf8881a879dcf37e0c5354335649b9f9d30f8ec | Ambrosia/Glasstle Prototype Bugfix (#40639) | Ported (CS-0185) | Medical, Chemistry | Produce solution capacities and contents now match the paired seed chemistry from index 1065.
1067 | b6bbb5d1b49feb51f5275fe0fdc103f12fd0d123 | Stage Name For Musicians (#40640) | PortCandidate | Gamerules | The retained musician stage-name loadout should be reconciled with CMU's role-loadout and character-name policies.
1068 | c95bf3f94fb77e7829dbf00b9a7b1c2b373eda76 | fix: scan for ShowAccessReaderSettingsComponent on examiner and not on airlock (#40626) | Deferred | Interactions | CMU lacks the affected upstream access-reader examine flow, so the examiner-side capability fix must land with that prerequisite.
1069 | 37ee54621a492bac36e581d23f0e72a6c5e52763 | Allow multitool device saving on devices with wireless (#38938) | Ported (CS-0186) | Interactions | Automatic configurator mode selection now prefers networking on mixed-capability devices while preserving an active linking session.
1070 | 13294a951a665ffcf1a98da38182cbfa391f33f2 | Adds default "Toggle" to "Status" linking port (#37690) | Ported (CS-0170) | Interactions | The standard Status source now proposes Toggle as its default compatible sink.
1071 | f87234d4d877841677990eebd5e898dd251d22a2 | Prisoner Eva Suit is now an Emergency Eva Suit (#36696) | PortCandidate | Movement, Interactions, Physics | The retained parent change should be reviewed against CMU prisoner equipment, protection, and suit inheritance.
1072 | 9828f165b45d86d0a1bdb96781753b641866ee4e | Increase puddle spillover volume to 50u (#38044) | PortCandidate | Chemistry, Physics | The retained spillover threshold is a small fluid-simulation change that needs CMU chemistry balance review.
1073 | f660964006b014a1f730e9bf24d767665373c666 | Organized Head Locker Fills Feat. Circuit Totes. (#39868) | Deferred | Interactions, Gamerules | The broad locker-fill, tote, construction, tag, and asset bundle must be reconciled with RMC command equipment.
1074 | aeb52a661cc9017d0f61ecf57ccb417fb70ced31 | STABLE INTO MASTER (#40648) | Irrelevant | — | The effective first-parent merge delta only changes Wizard's Den panic-bunker deployment presets.
1075 | 368d4dd273a740998a298088c8a3df26f30877b0 | Add utility knife/box cutter (#39567) | Deferred | Shooting, Interactions | The weapon, audio, bounty, belt, lathe, tag, and asset feature needs focused integration with RMC knives and vendors.
1076 | de9593c0e7206189fc2b3ded585390a53f75405b | Remove static IoC from client & server EntryPoint (#40562) | Deferred | GameTicking | The client/server bootstrap rewrite is coupled to engine IoC and CMU test initialization and requires a dedicated startup migration.
1077 | 47c79dca67cde43bf1b8ccea60ea54d733150093 | Fix lockbox ERRORing (#40642) | Deferred | Interactions | The hunk references the unintegrated generic Paintable contract; applying it alone would add an unresolved component.
1078 | bf5fccbbe956aa6ca4acac5a93e7f5e462df7733 | Stable -> master (#40662) | Deferred | Gamerules | The merge's effective first-parent delta adds a cached-preference guard, but CMU now uses divergent primary and fallback antagonist-preference APIs.
1079 | 9b7c87bd7e90191404e03f55be0acab46b6e891f | Fix anomaly shuffle particles type (#40624) | Deferred | Physics, GameTicking | Moving anomaly particle state to shared code is coupled to the wider anomaly prediction and network-state architecture.
1080 | bfc65b1554d42843e00c8bd948ea4b1f93db54c4 | some entities name fixes (#40663) | PortCandidate | Interactions | The retained silicon and circuit-board names should be compared with CMU localization and RMC silicon prototypes.
1081 | 3db18aa7b29bd8c9d704b536e7bbaed78cfd51bc | Material Door rebalancing (#36597) | Deferred | Interactions, Physics | Door health, material returns, and construction changes are balance-sensitive and precede the later target-final cleanup.
1082 | 24b661ddd7d133412b61987ccc0de070b1756c27 | Smuggler Satchel Heisen (#40665) | PortCandidate | Interactions, Gamerules | The retained entity-table sizing fix applies cleanly but should be checked against CMU smuggler loot balance.
1083 | 9672cd88fbe9b461cc0006faa88cb1332d18a4a6 | Remove x86 targets from Content Packaging (#40664) | PortCandidate | — | The retained packaging-target cleanup applies to CMU's current packaging project and can be handled as build maintenance.
1084 | 9c65777246d73658572e255386079ddfda2bfc93 | You can now stuff the nuke disk in plushies (#40674) | PortCandidate | Interactions, Gamerules | The retained toy storage whitelist change needs a security-policy check against CMU's nuclear disk behavior.
1085 | d92ebd222acdc25b706ebc0e9a96e5a0ed5760e0 | Document tags.yml: A and B (#40673) | Irrelevant | — | Comment-only prototype documentation has no standalone runtime behavior.
1086 | 3dacdc03c619f22f0af956c8ffde885e3e501a73 | Localize space villain arcade (#40641) | PortCandidate | Interactions | The retained dataset and localization migration should be adapted to CMU's current arcade implementation.
1087 | 46960c031507c76d59030888fa8208687e0ea982 | Conditional Meat spike logging severity (#40604) | PortCandidate | Medical, Interactions, Gamerules | The retained humanoid-aware log impacts should be reconciled with RMC bodies and administration alert policy.
1088 | d07527404c9da5d5df20c2eb1ac0c7f81959a4e0 | Suppress `SharedMapSystem` info logs in tests (#40592) | Irrelevant | GameTicking | Integration-test logging suppression has no production behavior to port.
1089 | bf49e289991d481009562a8c72228421f2364513 | nerf cheese prices, part 1: bedsheets (#38230) | PortCandidate | Gamerules | The retained bedsheet price reductions need CMU cargo-economy review.
1090 | 63c8dc572b0705f7bb4e77335bcd14e48b1f8e50 | nerf cheese prices, part 2: electronics (#38246) | PortCandidate | Gamerules | The retained electronics price reductions need CMU cargo-economy review.
1091 | d8e005087ce9bfd8fc758be0b821857d7a62fe3f | Add interaction tests for mousetraps (#35502) | Irrelevant | Interactions, Physics | Test-only coverage adds no standalone production behavior.
1092 | 250c1392fc590d9ef24c106320b32e06b4f86256 | Fix PDA point lights (#40687) | Ported (CS-0175) | Interactions | The shared PDA flashlight now uses the retained radius and explicit falloff, including inherited RMC admin PDAs.
1093 | 3764a719bfcd444b050639a57a06d593136c91f8 | Cyborg Martyr Module Free Hand Fix (#40224) | Ported (CS-0188) | Interactions, Physics, Gamerules | The module's virtual self-destruct tool now survives its blast and remains repeatable when the cyborg survives.
1094 | b6ab9dddc7e293e99688492e55c19a6d417dbe88 | Fix xenoarch exceptions + misc. cleanup (#38742) | Deferred | Interactions, GameTicking | The node attachment and lookup rewrite changes network-entity ownership across the artifact graph and needs focused state reconciliation.
1095 | 66614496338e4184d3d73a8e5f437e3ec2a1f5c1 | Adds smart equip to pocket 1, pocket 2, and suit storage slots (#37975) | Deferred | Interactions | The new inputs and smart-equip paths must be reconciled with RMC inventory slots and keybindings.
1096 | 690bb5a8f2349e572264d37f80da046eae56d966 | Add integration test for the RCD (#40625) | Irrelevant | Interactions | Test-only RCD coverage adds no standalone production behavior.
1097 | 5227489360126521b5ded181dc63ee29f73c6d87 | Predict EMPs (#39802) | Deferred | Medical, Interactions, Physics, GameTicking | The large server-to-shared EMP and power migration crosses numerous RMC systems and must land as an architecture checkpoint.
1098 | 3f0e9d696223286014bb5e5ca1871b409d0a40a8 | VomitSystem, Predict! (#39921) | Deferred | Medical, Chemistry, GameTicking | Moving vomiting to shared prediction crosses entity effects, destructibles, smites, and RMC medical behavior.
1099 | cad61d62e1b8a8b697c6c28a04281c358bf54d99 | Added Pride-O-Mat to marathon (#40696) | Deferred | Physics, Gamerules | The generated Marathon map edit should be reconciled with the target-final map rather than applied in isolation.
1100 | 7e3ee1d7c6097b2290d8e7f93a031b82794bbd0a | Explosives with timers now properly alert admins when detonating (#40471) | Deferred | Shooting, Interactions, GameTicking, Gamerules | Timer attribution depends on the newer shared trigger chain already deferred elsewhere and needs an atomic trigger integration.
1101 | 2dc0cef5d4f22952b59dedf89c03b11f07e4b48c | made evac signs glow (#38545) | PortCandidate | Interactions | The retained point-light prototype and glow asset need a direct resource comparison.
1102 | 24753a78db1720b70dd4195e29a0887dd61b6a3c | Add high severity logging to stun prods (#40709) | Ported (CS-0171) | Interactions, Gamerules | Completing a makeshift stun prod now emits the retained high-impact construction log.
1103 | d9b296a64049343ff5bd493310bee381d5750588 | Ian Suit gives accent! (#40694) | Ported (CS-0189) | Interactions | The Ian suit now grants BarkAccent through CMU's established AddAccentClothing contract.
1104 | b57be2413e4735a366dd42d30ce85bea4614e4b7 | Incendiary rounds do pierce (#39204) | PortCandidate | Shooting, Physics | The retained penetration flags apply cleanly to standard incendiary projectiles but require RMC ammunition-balance review.
1105 | c9373c5397c19672aec5f20e4233d858669b4077 | Update Credits (#40706) | Irrelevant | — | Upstream credits metadata only.
1106 | 7f69c44dd77fcad9410a6f9196257e6b32ad8ebb | Add Arrivals sign (#40227) | PortCandidate | Interactions | The retained sign prototype and asset need a direct resource comparison.
1107 | 096f998c3fb54e8434d39b6c8d87e3d22500b67b | Fix species default skin tones (#40707) | PortCandidate | Interactions, Gamerules | Initial profiles should derive appearance from the selected species, but the hunk needs adaptation to CMU species and preference extensions.
1108 | f94faf8aebc32cc69e825fc618180d8a8fdbe2f7 | fix edge case typing indicator error (#40708) | PortCandidate | Interactions | The combined RSI-state assignment and corrected moth asset should be adapted to CMU's typing-indicator visualizer.
1109 | 0466b8a5d3edc351c081588ff367339a91e54b73 | Document tags: C - F (#40711) | Irrelevant | — | Comment-only tag documentation has no standalone runtime behavior.
1110 | 326eaad18dc784e66970d0cb04bcd360324f2e9f | Prioritize spoon mixing over drinking (#40704) | Ported (CS-0187) | Chemistry, Interactions | Reaction mixing now runs before CMU's utensil handler and claims a valid mixing interaction.
1111 | 526ca2f800b191ff1276f4157c62f191ccc9e7ff | Automatic changelog update | Irrelevant | — | Generated changelog only.
1112 | 56cf3f536d3f3296cfe9e21eb0611076095f953c | Automatic changelog update | Irrelevant | — | Generated changelog only.
1113 | 7a6152217ee091802aef53dbefdb86f825245966 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1114 | 424c057c9220744c83d05a38e34f79dcc19b6070 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1115 | bcdaae489e7b814ded24f8b2775af146da83121f | Automatic changelog update | Irrelevant | — | Generated changelog only.
1116 | 9d3fde24f42cf09ab36d080f2e02877afd913e43 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1117 | f2dcd27ecb820810e89569035388132fc2041dfc | Automatic changelog update | Irrelevant | — | Generated changelog only.
1118 | c031b95f42165b6947c0b1fa75ce94bc64bd823d | Automatic changelog update | Irrelevant | — | Generated changelog only.
1119 | f9d9db6cfa38671fd9d9381bddd84f17b153f1f0 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1120 | a4b34c6a3c3f38dfd24065e22084af94063d0ba0 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1121 | 3218642e132d44f830e4fb1820a9f08c34c53d38 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1122 | 7a9cb78a849ad1acc7f399f04515b549debcaf75 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1123 | 6b8f660c8a229f09b2f30fe2098002fd79a1b6cb | Automatic changelog update | Irrelevant | — | Generated changelog only.
1124 | 562097abfad4daf7133460f003434c3c70953bf4 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1125 | cd968bee527c762cc1d2159811e2d4f7ccc98b71 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1126 | f5566eafe419d7c40aa64e3151af4ac3115fddb6 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1127 | c6c7b78e309e11a9894cfbd4870dfacf2465fa05 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1128 | c703a0a980d3f7e6b15da53478709cf9f14742ff | Automatic changelog update | Irrelevant | — | Generated changelog only.
1129 | e41662866447e66d3b09a2407174f504a15e5c7b | Automatic changelog update | Irrelevant | — | Generated changelog only.
1130 | 7e5eee302a83ef26b67850e67295a8e32863bce8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1131 | f23287267c9907c3dfbdfa668660cfa07e7d00c3 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1132 | cf8d3f9cbc6643f81b840c75aadc25a019eae83d | Automatic changelog update | Irrelevant | — | Generated changelog only.
1133 | e7bdec79806325b717733712e44867f044b0b5ce | Automatic changelog update | Irrelevant | — | Generated changelog only.
1134 | 174995bfd32fd9e323070619ab91542e43d527cd | Automatic changelog update | Irrelevant | — | Generated changelog only.
1135 | d66609f9c2abeec4072e7aeaf5bf0f749658aca8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1136 | ebe74e5df2cedcd30239b4575febdf869c834659 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1137 | 1b86ba6b506e6db9758c05e966f0e0a1c0597a38 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1138 | 8781a95b6c2cb733d8007b56b1e2bc2493e9168c | Skeletons are now playable instruments (#40009) | Deferred | Interactions, Gamerules | RMC skeleton inheritance changes the affected species base, so this interaction and role policy needs a deliberate adaptation.
1139 | 1ca03fe8b1ffb513936e168479e64e1c645d7cc6 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1140 | 561fd493bd81e93a9b8352b984f6983756a5dabf | Marathon improvements Part 1. (#40725) | Deferred | Physics, Gamerules | The generated Marathon map bundle needs target-final layout, power, and role-content reconciliation.
1141 | 78748d5c7d67c1787476856d44f107918a2d258e | Automatic changelog update | Irrelevant | — | Generated changelog only.
1142 | ee2b38d299c4ecc894f28883f68e57eaaae4b57f | Create more Holy Books based off Spacestation 14 Dieties (#39181) | Deferred | Medical, Interactions, Gamerules | The large holy-book, chaplain-loadout, and asset bundle changes religious content and healing interactions and needs policy review.
1143 | 2212b690e0bda304eaa869574a6e04c1fab5ceeb | Automatic changelog update | Irrelevant | — | Generated changelog only.
1144 | b9f2fd4e6738a63d5af624f967246e75fddc5966 | Replace all time requirements with bats (#40751) | Deferred | Gamerules | The repository-wide role, antagonist, and loadout requirement migration is server-policy-sensitive and crosses RMC jobs.
1145 | 0bb1ede5ee112dcdd00afde8c1a0432378a83e6d | Remove the Tanakh and Satanic Bible from the game. (#39698) | Deferred | Interactions, Gamerules | The content removals, migration entries, assets, and chaplain loadouts require an explicit CMU content-policy decision.
1146 | c2c9d7c784986c54fad988e614a1bf56df49a110 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1147 | f1e5d1eb07d0cf943f8addfff21371c13a056d7c | Let gorillas pull things (#40700) | Ported (CS-0190) | Movement, Interactions, Physics | Standard gorillas now use the established handless Puller contract without changing RMC pullers.
1148 | 46aa3bbc41fd583f3baee222cbfbd35bc6e55ff9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1149 | e1c03249fa0d73f528f8b988f00bb64530278002 | Golden Plunger General Fixes & Tweaks (#40619) | PortCandidate | Interactions | CS-0166 supplied the tag contract; the retained cart slot consolidation, mapped visual, metadata, and corrected binary asset remain to port.
1150 | ce7558f3d294ab80f503ef531999ab209da5cb52 | Change Discord round restart text (#40584) | Irrelevant | — | Wizard's Den deployment-notification wording has no CMU gameplay delta.
1151 | 84f994f296e7a88bcbf10dbce46669e61cd2ffc4 | Give mimes their french bread back (#40601) | PortCandidate | Medical, Chemistry, Interactions | The retained bread variants and emergency-box fill need reconciliation with RMC mime equipment and food prototypes.
1152 | b78eecfb936900bfe8fd36ccbcde3bad803ce634 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1153 | 101b9ffb257b451a2220baee5531f565ead3833e | Cleanup material_doors.yml (#40666) | Deferred | Interactions, Physics | The target-final inheritance and destruction cleanup depends on index 1081's unintegrated material-door rebalance.
1154 | 691ca31b9535e074007bc85e345655948c6bf143 | (Cleanup) Fix logger obsolete warnings (#40553) | PortCandidate | GameTicking | Applicable logger migrations should be selected against CMU's current engine API without importing unrelated startup or guidebook changes.
1155 | 80c66c02bedf47da0b96d4aec0594d3a286c1b74 | changes the min and max variables in the TargetTemperature clamp to t… (#40453) | Ported (CS-0172) | Interactions, Physics | Portable-heater requests now clamp against the limits owned by SpaceHeaterComponent.
1156 | 984d28232b815c99c23f01950650e219cb814199 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1157 | 1d827754c9776080f9fa9060cd9724e10226bd8c | Move BrainSystem and necessary components to Shared (#40499) | Deferred | Medical, GameTicking, Gamerules | The server-to-shared brain and ghost movement migration crosses prediction, mind lifecycle, and RMC body systems.
1158 | e32253451b7e4b9a8f629228f529f19ff8466a31 | Vox burn into fried chicken (#40115) | PortCandidate | Medical, Chemistry, Interactions | The retained species burning result should be reconciled with CMU mob destruction and food inheritance.
1159 | 6b480673cd6bff9c3282fbf4df25fb3f7f438512 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1160 | 4b51b2953d780fba56fd9721b4a91f44d3f8fbfa | Fix post-mapinit NPC exception (#40244) | Ported (CS-0176) | Movement, Interactions, GameTicking | HTN blackboards now receive their owner at component startup while map initialization remains responsible for waking NPCs.
1161 | ff01e13d247d916b58bd9638be79a012c130e166 | Head of Security's Energy Magnum (and Warden's Energy Shotgun) (#40615) | Deferred | Shooting, Interactions, Gamerules | The weapon, projectile, locker, objective, guidebook, and asset bundle conflicts with RMC security equipment and balance.
1162 | 7b7f49cd3939ce73eb7c8046f8bcc8169d66cb97 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1163 | 70543885e75598c2aad64113cc824a33acf9efcb | Add log statement for missing guidebook proto (#40380) | PortCandidate | — | The retained client diagnostic is useful but should be adapted without the unrelated logger-API cleanup in the same target area.
1164 | ceedfb6d39ebb8331473550fb2d4a8f0202da082 | Standardize state names in drinks yml (#40316) | Deferred | Chemistry, Interactions | The large prototype, solution-ID, and binary asset rename must be reconciled with CMU's divergent RMC drink hierarchy.
1165 | 745c6d0edc2a1271431a4e797134de21fc331e52 | Unpredict MagnetPickupSystem.cs (#39988) | Ported (CS-0177) | Interactions, Physics, GameTicking | Magnet pickup scan deadlines are now authoritative, networked, pause-aware, and dirtied when advanced.
1166 | 4bd1cb3ac44a924e45ae374fc0edcc8f46511dde | Cancer Mice Ghostrole Info (#40102) | Deferred | Gamerules | The FreeAgent role classification and ghost-role wording are policy-sensitive and need CMU antagonist review.
1167 | 6dc131b76c87b2c6b87cd7f4c2a029be66a058ba | Automatic changelog update | Irrelevant | — | Generated changelog only.
1168 | 167f3f2b92424f358b29265b3d0157e298a96480 | Biosuit Suit Slots (#39888) | PortCandidate | Medical, Interactions | The retained suit-storage slots and cargo fill should be reconciled with RMC biosuit loadouts.
1169 | 1deef4bb7e66d423fc722406cfa932799fe9da49 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1170 | d463aba5875861226000167f991420223a5fa181 | New HTN precondition: has status effect (#39781) | PortCandidate | Medical, GameTicking, Gamerules | The retained reusable HTN precondition applies to CMU's current NPC stack but needs status-effect and prototype consumers reviewed.
1171 | afe11dc9595dc4a976ced6a156bd331101a672a4 | MRE wrappers / cotton nutri-bâtards are no longer twice as nutritious as nutribricks (#40761) | PortCandidate | Medical, Chemistry | The retained food-solution correction should be adapted to CMU's MRE and bread reagent contents.
1172 | 0734cdd18abfb71e90c3710e9fb778c8ef170e10 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1173 | 5be5e280be9955a206ca485c9c42d4604470e77a | Added Vox Beak Types, New Markings, and Sprite Layering Fixes (#40569) | Deferred | Interactions, Gamerules | The large species customization and binary asset bundle needs reconciliation with CMU's current Vox markings and appearance policy.
1174 | e841cb9fc6e470b791db5bd43e2ffd3a054766da | Automatic changelog update | Irrelevant | — | Generated changelog only.
1175 | 63c17b151d46953de6f57347ccaad749e1354c95 | Added the golden shaker (playtime reward for Bartenders) (#40762) | Deferred | Chemistry, Interactions, Gamerules | The item, assets, loadout group, and playtime gate require CMU role-policy and drink-hierarchy reconciliation.
1176 | 86b1be88dd93ccd59872328ffbad4528ed8c72fa | Automatic changelog update | Irrelevant | — | Generated changelog only.
1177 | 33cd3df45ae1ca64defbbd39cb541e22a7cdbc4c | Change Energy Shotgun to fit as a Warden weapon (#40757) | Deferred | Shooting, Gamerules | This balance follow-up depends on index 1161's unintegrated Warden energy shotgun.
1178 | 694fd0628ff7ea23cb2f86611a69f89085a9ee29 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1179 | 3f115fa1d48e3da132119fb5f96ed8a776559a1a | Fix- Cobras killing adders (#37424) | Ported (CS-0174) | Interactions, GameTicking | Space adders now share the SimpleHostile faction while retaining Xeno alignment.
1180 | 44563dafdcef0294f4f4e82359223603571c5353 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1181 | 0c9d33d5d6e4a355188185ae0fa4fdcf287857e5 | Cleanup warnings: CS0414, CS8524 (#40776) | PortCandidate | GameTicking | Applicable warning cleanups should be selected narrowly because several removed dependencies remain in CMU systems.
1182 | 68b3c7a5206d63c3590f3c2be36d6df909cd161e | Clean up bucket.yml (#40772) | Deferred | Chemistry, Interactions, Physics | The solution-ID, inheritance, composition, and destructibility rewrite crosses CMU and RMC drink and fluid contracts.
1183 | 532564e05fab1e85d3e00b20f87b0cd6e86c5cc6 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1184 | 91aa169784a59ff446cb4487cd134e6f95171342 | Slightly re-nerf zombification speed (#37445) | PortCandidate | Medical, GameTicking, Gamerules | The retained infection timing applies cleanly but needs RMC zombie balance review.
1185 | 4f93089e4240c2ec72684357ba95392fc49196a4 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1186 | 41c7229b9dda4ebbf426288d15fd199645cda482 | cleanup and reorganize belts.yml (#40773) | Deferred | Interactions | The large prototype split and migration must preserve RMC belt descendants and repository paths.
1187 | 7cdd8cb2a9755cfb0e6ddaf133578c2ea0b679df | Add disclaimer about AI generated content to Readme.md (#39334) | Irrelevant | — | Upstream README policy has no standalone gameplay delta.
1188 | 92082f80914856cc608817d0449afb7430af99ab | Fix the temperature gun not reflecting and going through windows  (#37581) | Ported (CS-0178) | Shooting, Physics | The accepted target-final adaptation retains opaque collision, adds impassable masks, and makes hot and cold bolts energy-reflective.
1189 | 2a5a72d5bd556c3edfb76c6b49e24e20009e5ff6 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1190 | 1b62863e52f129dcc88386b508afbb41c741966b | Fix: Allow energy shotgun lethal projectiles to hit holos (#37920) | Ported (CS-0178) | Shooting, Physics | This target-final collision follow-up is included in CS-0178's combined watcher and temperature-bolt contract.
1191 | e7f1d67df0f1e463e60dbe5b76f428a44ae0cb2b | Automatic changelog update | Irrelevant | — | Generated changelog only.
1192 | f80f3223571bfd6cacbb2b22b63902508c4184cb | Added folders and clipboards to trinkets tab (#39920) | PortCandidate | Interactions, Gamerules | The retained loadout entries should be reconciled with CMU trinket groups and character-loadout policy.
1193 | c566b17da77d5cffd7b69cd28827d8ca2fa0e280 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1194 | 0805943c9879352aced2b73e2414a4b0ec8ee06f | Fix wizard can teleport into the ATS wall (#40755) | Ported (CS-0173) | Movement, Interactions, Physics | The ATS warp point now lands in open station space instead of inside the wall.
1195 | 3f2a58a1575c14969aac17fdd6d2edaec2363336 | Relic fixes and tweaks (#40537) | Deferred | Movement, Physics, Gamerules | The broad station, shuttle, prototype, parallax, and binary asset update needs target-final map reconciliation.
1196 | d3771a19e2502dfda94cdec325a87c26551d3bb1 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1197 | 02ada04eb4f2cf69fdc1aee7999bc103e452adc3 | Removed wardens enforcer from box (#40785) | Deferred | Shooting, Gamerules | The generated Box map equipment change depends on CMU's divergent Warden weapon and station-map policy.
1198 | 0039dff91fe5da9bcfb303ebcbb9ff1ab556adc1 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1199 | df6307fe66f71944c5b3d5ed1e683a2723953181 | Allow more energy projectiles to hit holo creatures (#40782) | Ported (CS-0178) | Shooting, Physics | The final hologram collision expansion is included in CS-0178's combined target-final projectile state.
~~~

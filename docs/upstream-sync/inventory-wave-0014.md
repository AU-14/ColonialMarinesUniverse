# SS14 upstream inventory: wave 0014

Audit date: 2026-07-20

- Pinned baseline: `59633f6dc50e77dda8cefa344d87c7b01e06a810`
- Pinned target: `40ca2c7f90d11d27be5457d177c133f0947d1c08`
- Range: ordered first-parent commits, zero-based indices 2600 through 2799
- Columns: index | full SHA | exact upstream subject | disposition | core-system areas | rationale

`Ported (CS-####)` links an accepted core-system change to the durable audit, while
plain `Ported` identifies accepted non-core work. `PortCandidate` retains target
behavior that still needs integration. `AlreadyPresent`
means CMU already has equivalent behavior. `Deferred` preserves behavior pending
focused reconciliation. `Superseded` means another target or local architectural
change replaces the commit. `Irrelevant` identifies commits with no standalone
behavior to port.

~~~text
2600 | 4ce1aa6bfeada86290ef809d031f01879553e619 | Prevent anomalies from spawning multiple entities on the same tile when instructed not to. (#37833) | PortCandidate | Physics, Gamerules | Focused anomaly placement fix removes a tile after its first selection; CMU still allows duplicate spawns there.
2601 | 8591b92e9ec27e90086d0e93836b7af76acdbf5c | Automatic changelog update | Irrelevant | — | Generated changelog only.
2602 | 1d05170ea6d35bbe635c2910191223b1e1ce2d67 | Fix bio suits (#42748) | PortCandidate | Medical, Interactions | Self-contained bio-suit tail-hiding and sprite repair; reconcile the three locally divergent PNGs when porting.
2603 | a765d420748762470fa0e885fbdc2f7421b0ca32 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2604 | b5f4c28c9f5847fa8348f5000c1a11e50df9b400 | Ensure DNA scrambling makes valid profiles (#42757) | Deferred | Interactions, Gamerules | Valid-profile DNA scrambling depends on the newer substrate/profile-generation path absent from CMU.
2605 | 3e6d5f6e248aa031afb05fe45d089373f2470ab7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2606 | c358dbf9ac87b8288693193c406e5643ead9a16e | Make Ichor work in the bloodstream (#42758) | Deferred | Medical, Chemistry | Ichor bloodstream metabolism uses the newer metabolism schema; CMU retains the legacy Drink metabolism contract.
2607 | 7b630cd561437df640192055d8514dc1b2375fc9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2608 | 08bbc119727f79bbdc257b0a6b22ddffc3ab223a | Clean up Marking data structure, add tests for Zombie transformation (#42756) | Deferred | Medical | Marking structure cleanup and zombie-transformation tests cross the Nubody/marking architecture missing from CMU.
2609 | 748179752f337e3bb176b1f6955d32c8f17a714f | Automatic changelog update | Irrelevant | — | Generated changelog only.
2610 | d448f0454e247d74515bfcc60930b1691c8d3401 | Make `StatusEffectsSystem`'s cache an instance field (#42762) | AlreadyPresent | Medical, GameTicking | CMU's current status-effect implementation has no process-wide static prototype cache, so the lifetime defect is absent.
2611 | 6e84e72e782ca93d5fb9f041ce782e15681e1fd3 | Fix markings colour setting (#42771) | Deferred | — | Marking color-setting changes target the newer MarkingsViewModel/Nubody UI files absent from CMU.
2612 | 0599a24a7a767248edf20c3e939f8181dcc7dcfa | Automatic changelog update | Irrelevant | — | Generated changelog only.
2613 | 8c891ef0d771c6d4ba288b29764da03a0bc376ed | Syndimov Kit (#42764) | PortCandidate | Interactions, Gamerules | Bounded Syndimov kit, Syndicate ID job icon, uplink entry, and one icon asset can be reviewed as a unit.
2614 | 899c8d29350a89f0ff5ccf5003221586e8a6e9bd | Automatic changelog update | Irrelevant | — | Generated changelog only.
2615 | d4fe565b2e5de65ae2f38cf32893faa9f15c6f4e | [EXPERIMENTAL] Removes Blunt and Burn Damage Threshold Gib Behavior (#42474) | Superseded | Medical | The experimental removal of blunt/heat gib thresholds is restored by merge index 2721.
2616 | 9722ae01c4c350cb603d35db5ff743aadd6dc393 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2617 | 27ef42af10837e4d61dd88a82444ffa6ff1865f6 | Only rebuild organ markings when sex or group has changed (#42782) | Deferred | Medical | Organ-marking rebuild logic depends on the newer organ-marking picker/view-model architecture absent from CMU.
2618 | a7a2ee6225a3d523ed418b6aeb89d3ef3e36ffe2 | Adding logs for ImmovableRodSystem Interactions for admin panel (#42769) | PortCandidate | Physics, Gamerules | Focused Immovable Rod collision and gib admin logs can be adapted independently; pair with spelling fix 2622.
2619 | 716943ee5d1658998fd63f5ad2b1bd7618030307 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2620 | 222021dcc8fa1a46990c5c78dd172f697245d0ce | Make volumetric devices respect their pressure limits (#35211) | Deferred | Physics | Pressure-limited volumetric devices change central pump/filter atmos math; land only with corrective indices 2632 and 2690.
2621 | 34c712a445cee47f4caacb4a49ccfe91cd1182f8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2622 | 81dc1af6de0ab71a03f0ccb26f2d1739d398181c | Minor spelling correction in admin logging (#42784) | PortCandidate | Physics, Gamerules | One-word Immovable Rod log correction is safe but has no effect unless candidate 2618 lands first.
2623 | 8c4d86c968a03174491c012acb0d48648f1be17d | Hopefully fix subgamemodes from pushing rounds into extended. (#42744) | Deferred | GameTicking, Gamerules | Subgamemode rule/preset restructuring crosses CMU's divergent Xenoborg and round-preset policy.
2624 | 381fda04403358d01c491c0aa63ad59b8aa7e978 | Remove Uranium from Mute Toxin recipe (#42787) | AlreadyPresent | Chemistry | CMU's Mute Toxin recipe already contains Vestine and Space Glue only; Uranium is absent.
2625 | 27f674d77bdebc6bc3d9ae6bda100cd482c9e2a8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2626 | f2b3bf9f01f4944bf8064fd78b1f710150cdef70 | Add feedback popup for gibbing/ashing removal (#42789) | Superseded | Medical | The experimental gib-removal feedback popup is deleted by merge index 2721.
2627 | 25262cb3b989a3e6ac72d89949adfe70ec4f45e3 | Added Insulated Components to RK and Servants. (#42569) | PortCandidate | Interactions | Two prototype coefficients give Rat King and servants the existing Insulated behavior without new code.
2628 | c0f691a1424567cbbaeb7ade8b771e5d3f1c1102 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2629 | 41f042b8f9748852e560b8a5ab288d4cb21fd819 | PredictedRandom Helpers (#42797) | Deferred | Medical, Chemistry, Interactions, Physics, GameTicking | Fifteen-system PredictedRandom migration is broad substrate work and is required by predicted metabolism at 2752.
2630 | c2f986ea8b537cb8aa586e40986ae9c5699b75bc | Cleanup Antag Selection Logic a Lot (#42673) | Superseded | GameTicking, Gamerules | Exactly reversed by index 2780; stable patch IDs match when the revert is reversed.
2631 | ddd75da06be2cb11347119ff49322948a08b5ccd | Stable to master (#42802) | Superseded | Medical, Chemistry, GameTicking | Merge effective delta is 1 file, +7/-1, adding a client metabolism guard later removed by predicted metabolism at 2752.
2632 | a229bc26a89e17485795b08663c22dd879ae428a | Fix gas filter math (#42801) | Deferred | Physics | Corrective gas-filter pressure math depends directly on the volumetric-device rewrite at 2620.
2633 | 991a3e9c229f8359d617cd090faddb8b0ac1bc13 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2634 | eca952e846c986c4ac0ca678c5b57fb91988b0cb | Optical thermal scanner implementation  (#42613) | Deferred | Interactions, Physics | Nine-file thermal-vision feature adds components, overlay, shader, UI, and tests absent from CMU.
2635 | aa7eca9e8cde52610eb2d793def4ea70833169df | Automatic changelog update | Irrelevant | — | Generated changelog only.
2636 | 91b518ac05e702d27d841e1ca4c98fedb85a978f | Add opticial thermal scanners to engivend (#42813) | Deferred | Interactions, Physics | EngiVend scanner stock depends on the deferred thermal-scanner feature at 2634.
2637 | cfb59ef20e488582157c7b0e722e85f8874cb4be | fixed vox tail marking and suitslot layer order (#42808) | Deferred | — | Vox tail/suitslot layering targets the newer body and marking layout and needs asset-layer reconciliation.
2638 | 4c62ce20255b3252dc52ad9c1e613581b5db5204 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2639 | bb8ce2962af106b40374e845b5e670a5b38e2964 | Chaplains can now choose a custom name (#42819) | PortCandidate | Gamerules | One role-loadout flag enables custom Chaplain names and can be adapted to CMU's older loadout path.
2640 | e627ba3fe758196f0e37bbb8f4332d6e7391690e | Automatic changelog update | Irrelevant | — | Generated changelog only.
2641 | 01039a81f0fc2ad770a03549bc857efe1413db43 | Defrost Plasma (#42822) | Deferred | Physics | Large Plasma station map rewrite belongs to map/atmos reconciliation, not a direct code port.
2642 | c36fffcc1861fe386ce1aa7f28a3012dd1fdc3a2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2643 | 3efa385f5cae1e6ed9ba7bc9ebc5fc512d99748c | Add CL for 42813 (#42824) | Irrelevant | — | Empty commit; its first-parent tree is unchanged.
2644 | fd870f3e77b9047af276c846eb3eb82f733bec94 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2645 | d72a59f6b75d35eec95f2fff084937c8767afc05 | Predict holoprojectors and add an integration test for them (#41569) | Deferred | Interactions, Physics, GameTicking | Holoprojector server-to-shared prediction migration and integration test cross CMU's older battery/projector design.
2646 | ff3076aa13a5183b3e347ef2a2bcdd4e8b75eb05 | fixing firelocks (#37523) | PortCandidate | Interactions, Physics, GameTicking | Focused firelock update closes a powered open lock while its alarm remains dangerous; CMU lacks the repeated-state check.
2647 | c1c0e59a8164741abdd6f7448f7852d4ebb693ac | Automatic changelog update | Irrelevant | — | Generated changelog only.
2648 | 1e7d50ebf03754cfcecb5db4dbea41b945f9257a | scale ammonia and nitrous oxide damage with gas quantity (#39591) | Deferred | Medical, Chemistry, Physics | Gas-quantity damage scaling uses the newer metabolism and reagent-condition schema absent from CMU.
2649 | 980cb75ef57a7449f76d99eadb88589b15d166a2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2650 | 993c316b6ca28d0e4893de9d1fa8ea5e40ab796b | Emitters now give alerts if interfered with. (#39513) | PortCandidate | Interactions, Physics | Bounded emitter tamper/power radio alerts are absent locally and can be policy-reviewed independently.
2651 | ba2b8338009f39e6309c4968d8e4a611c043652c | Automatic changelog update | Irrelevant | — | Generated changelog only.
2652 | 45a5d5b1d9eadaefec27dd04cd212db6b85a4533 | Fixed containment fields dying even when one side still had power (#41006) | PortCandidate | Interactions, Physics | Focused containment-field endpoint logic fixes premature collapse and applies cleanly to current CMU.
2653 | ebebc428f8f2b86ea1b0d35eaf52279a881f4a72 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2654 | e4edf7d92e640fd3bcca9bf522fed232bf7f6a3d | fix cl (#42831) | Irrelevant | — | Changelog-only correction.
2655 | 9170e26bb80c8003ccc7c4eb790bc0b6d955510d | Make nukie infiltrator shuttle pinpointer universal (#42101) | Deferred | Movement, Physics, Gamerules | Infiltrator shuttle map rewrite and universal pinpointer behavior require shuttle/map-policy reconciliation.
2656 | 3a202be2227c825db0ae7cd6fec791a213746d27 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2657 | 2221df17c36964a6e6d53eb6b8fa73fea16db0d8 | Make conveyors stack items that stop (#42829) | PortCandidate | Movement, Interactions, Physics | Small conveyor contact hook uses CMU's existing stack-merge helper when stopped items meet.
2658 | 6369aae8051776369294d2d62058067ff99bc26b | Automatic changelog update | Irrelevant | — | Generated changelog only.
2659 | 6714a2fc3742528405c779227e93110ef17d28f1 | Solved #42803 - Syndie contra explicitly has no allowed departments or jobs. (#42820) | PortCandidate | Gamerules | Two explicit empty contraband allowlists close inheritance ambiguity with no new substrate.
2660 | 48cb7eafaeb47003f0c78ff4f2fcb8ef9acd7732 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2661 | 1db05a6567c9735ce70b91e5279861b37255501b | Empty Crayon Box (#42837) | PortCandidate | Interactions | Self-contained empty crayon-box prototype reuses existing box and crayon content.
2662 | bbe00d6cd69d0d00c0ac4a121140ba5653514761 | Fix some locale that sometimes get ignored by gitignore (#42834) | Irrelevant | — | Locale path and gitignore organization only; no standalone runtime behavior.
2663 | ff417132a20430d326427f6e0d6232eb0981fb18 | Animated Vox Tails (#40925) | Deferred | — | Animated Vox tails depend on the newer wagging/marking asset stack and body-layer ordering.
2664 | c4d439f67a4d836dc0d5299fafb4dc1bf110dde5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2665 | 7e88e4efca4b27334b33c16065f263c163d0077f | Fix-sprites-practice-disabler (#42838) | PortCandidate | Shooting | Small practice-disabler in-hand sprite-state repair is independent of firearm code.
2666 | 1d5566304166ba7351074573eb2ccb359e3df617 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2667 | 15dbf80e3c41687cbd4793eac486f750bade796a | Change warfarin to heparin (#42847) | Deferred | Medical, Chemistry | Warfarin-to-Heparin rename assumes reagent/prototype substrate not present in CMU.
2668 | 6f79e1d744c6dbbdb8a95b5ed172662ea6301be2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2669 | 41dddf4d99d30839730011a8c64d1203cdedddbf | Fixed vox cigarette smoking sprites (#42584) | PortCandidate | — | Self-contained Vox cigarette mask sprite correction.
2670 | 9c23b4a6d8d3188b8027ae5c0042a31455e31e03 | Documents all the public APIs for Nubody & markings code (#42857) | Irrelevant | — | Public API documentation only.
2671 | 43c8dc711a25c98a89a1d131df0294afbcd5c0d5 | Fix(Humanoid): Prevent skin color verification failures due to precision loss (#42836) | Deferred | — | Skin-color precision, circular hue, and tests target a newer skin-color strategy architecture absent from CMU.
2672 | 63af4dd3b430f3b2595ca122c9f0567dc1d0f1dc | Fix RCD not being able to place hull tiles in space (#42740) | Deferred | Interactions, Physics | RCD hull-tile placement depends on the wider deferred RCD/tile event migration.
2673 | 545223af9bdbf411dae601e056c2544de3a4d1dd | Automatic changelog update | Irrelevant | — | Generated changelog only.
2674 | 676492e667978b1546e893217a1d2a67b86b1a95 | Thermal vision overlay for admins (#42812) | Deferred | Interactions, Physics | Admin thermal overlay depends on the deferred thermal-vision implementation at 2634.
2675 | 271252c00325d159fee4d13e96dcd3a6e824a3fd | New high pop station - Serpentcrest (#38991) | Deferred | Physics | Very large Serpentcrest station feature needs dedicated map, prototype, atmos, and power review.
2676 | a882259007c92377769b3af0e6bba0deb9462a6e | Automatic changelog update | Irrelevant | — | Generated changelog only.
2677 | 9c89a59d961a05a2132ffa08274a19f5f5bafba0 | Update Credits (#42848) | Irrelevant | — | Upstream credits metadata only.
2678 | a01e7dcf40e8a25028062b763f9147a81809a2e4 | fix: APC sprites stuck in fully drained states on round start (#42852) | PortCandidate | Physics, GameTicking | One APC state-invalidating assignment fixes stale drained sprites; CMU retains the old call path.
2679 | 0d9d2443ce8d98312fa049bd856f20c0649174da | Automatic changelog update | Irrelevant | — | Generated changelog only.
2680 | e76a1b5cd67d34608d466b6e5266d881acef81b5 | Minor tweaks to Urist names (#42791) | PortCandidate | — | Small Urist species-name dataset cleanup can be reconciled independently.
2681 | 865180d7fe6dcc3d69144aa0afe45e8b06bc5293 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2682 | 22ba8a3869139bc6c4a2170e3118968ce1706665 | Vulpkanin Yawns (#42768) | PortCandidate | Interactions | Self-contained Vulpkanin yawn sound/emote configuration.
2683 | 6f924dfa94b0484958bc6dd8866ebb7dcee29a50 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2684 | 15147dfcdfc1ce08950529c19ba5a965fa599458 | Split HumanoidProfileEditor.xaml.cs into separate files (#42715) | Irrelevant | — | HumanoidProfileEditor file split only, with no intended behavior change.
2685 | 94070d2962c21dce4569af25310fcb777a03317d | serpentcrest hotfix (#42863) | Deferred | Physics | Serpentcrest map hotfix depends on the deferred station feature at 2675.
2686 | 8cee089522285277f8335c465ff899fdd27e6d7c | Automatic changelog update | Irrelevant | — | Generated changelog only.
2687 | 3911d0df6f82eefef78abec936d409fda28450d1 | Ensure profile loading only returns valid species (#42842) (Stable merge) (#42865) | Deferred | — | Merge effective delta is 6 files, +240/-172, moving profile conversion into preferences and validating species across divergent DB APIs.
2688 | 8c68824ad8c09baf95c516549a033a6916dc555a | Slat tiles and decals (#37832) | Deferred | Interactions, Physics | Eighty-file slat tile/decal content feature needs dedicated asset, construction, and map integration.
2689 | 3813758766e06e75c99ba5497153cfd8cbdd09ff | Automatic changelog update | Irrelevant | — | Generated changelog only.
2690 | 1f82b9eb8e58887e55d262baf5c4daf23ab135c2 | Fix gas filter always outputting 20C filtered gas (#42876) | Deferred | Physics | Filtered-gas temperature fix depends on the volumetric/filter math cluster at 2620 and 2632.
2691 | 550d6a0f09dc258839af46e62bca6aec96d08b54 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2692 | c086acbc346cb331e4f648531635a8d23c276ab3 | Cleanup warnings: CS0114, CS0414, CS9107 (#42859) | Irrelevant | — | Compiler-warning cleanup only across paths that have since diverged or disappeared.
2693 | 862ce388a74b5102eb5aaf14c0fc0d58fcdeb37d | Regular bots are now repaired gradually, just like borgs (#42878) | Deferred | Medical, Interactions | Gradual bot repair changes silicon repair timing/fuel balance and must be reconciled with RMC bot policy.
2694 | 457cd6509c0b18daaf7157da2dfb3aa984dc5397 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2695 | 7009a061514eeabf5f6e6d0419d820bf833119c3 | Vulp Tail Wags + Tail Fixes (#42843) | Deferred | — | Large Vulp tail-wag asset feature belongs with the deferred marking/tail cluster.
2696 | aa4422fcdf19d82bfcc94f6dd97b175098200b8c | Automatic changelog update | Irrelevant | — | Generated changelog only.
2697 | fc4a96faad438a23df1cba21ec2c2f1fcc185da0 | Add Missing IntercomAssembly Components (#42821) | Deferred | Interactions, Physics | Intercom assembly inheritance fix assumes the newer BaseWallmountMetallic hierarchy absent from CMU.
2698 | 5f126736a6da29ab98346acb387c7f237fbca3a1 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2699 | 5a2da2679e11dd9cb5fb1f04c4fe83bf9eff8c45 | (Re)Add sneezing emote. (#41479) | PortCandidate | Interactions | Focused sneeze-emote whitelist/trigger addition applies cleanly to current CMU.
2700 | 57b248fc26c02b1af836c24be47a67ded1719aae | Automatic changelog update | Irrelevant | — | Generated changelog only.
2701 | b90c9e4dcf925efa112ace129f112a95d08441bc | fix: cleaning evidence off a person no longer reveals their true identity (#42868) | PortCandidate | Interactions | One identity-safe popup argument prevents forensic cleaning from exposing a disguised person's true metadata name.
2702 | 09222dd6436580853403c28951cc197348ee5a1e | Automatic changelog update | Irrelevant | — | Generated changelog only.
2703 | b47db861890c35436ad2db061925746631e080ee | Remove bad accents (#42880) | Deferred | Interactions | Accent removals are only partially reverted at 2778; reconcile the target-final accent policy as one cluster.
2704 | 5ae67c9cab889e33f3dc7c84a984e25a27f3a626 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2705 | fe4397e340e8c9ffa07f5ef4a275c0b9672b9b35 | Remove dwarves (#42882) | Superseded | Medical, Chemistry | Dwarf removal is functionally reversed at 2772; CMU retains dwarves.
2706 | 1d13dbfc4c86380b230a6542971bf253dede54c7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2707 | ffe93b0b24e763b6430d3cb272c42d49a21fdeb4 | Fix chameleon controller not updating fake mindshield action icon (#42900) | PortCandidate | Interactions, Gamerules | One action-state update fixes the fake mindshield toggle icon; CMU lacks it.
2708 | cb065fa8db6ec0175104941c8425de9a0d5282b5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2709 | 1cc21d9d362fba582cd1ef4daa8b86780b528349 | Fix Flares dying after 10 seconds. (#42765) | AlreadyPresent | Interactions | CMU's older non-looping flare animation path does not restore the spent state, so the ten-second death defect is absent.
2710 | e16ae0e76c54c27b4c27119891ca6d3e479e6332 | Typo fix (#41652) | AlreadyPresent | Gamerules | CMU already reads antagonist requirement overrides from Antags rather than Jobs.
2711 | 0e86bd45bd422e910b0ed134488ba0fa85782418 | Identity Mask now updates equipped Agent ID name (#42772) | Deferred | Interactions, Gamerules | Identity-mask Agent ID renaming crosses divergent shared/server voice-mask and ID ownership code.
2712 | 7593a46b07e9c26cb6a97a9b9662489286b8860b | Automatic changelog update | Irrelevant | — | Generated changelog only.
2713 | 2f3589ec889f6ff274bacfdfa04cb46038263a72 | Vulpkanin Sulfur Blood + Organs (#42722) | Deferred | Medical, Chemistry | Vulp sulfur blood/organs is a 31-file Nubody species feature requiring dedicated medical and asset integration.
2714 | 41cb4b8d53a0cbef0526e04924c67d35f34e5089 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2715 | 6e38a79257b74db933ffb7f6776356c37fc404af | Fix paradox clones having default voice and pronouns (#42923) | Deferred | Medical, Gamerules | Paradox-clone voice/pronoun repair assumes newer humanoid profile and vocal component data absent locally.
2716 | 9a7692aee584eecb1da8f27aa2f862f270f6166c | Automatic changelog update | Irrelevant | — | Generated changelog only.
2717 | faeacd18cfd8e944a57e103cdc15ccb082177909 | AI can now read papers and envelopes (#42926) | PortCandidate | Interactions | Two existing StationAiWhitelist components let station AI read papers and envelopes.
2718 | add86afc3a6d284d530d3d999b5a43ae6adadafe | Automatic changelog update | Irrelevant | — | Generated changelog only.
2719 | d1cbe4507217e992ad7c441ddad149b3114d26ae | Change basic viper magazine to high capacity in operative bundle  (#42927) | Superseded | Shooting, Interactions, Gamerules | The high-capacity pistol magazine is replaced by an energy sword in target-final bundle index 2740.
2720 | 883b58ca2a6fc9f3e332e3f8879c4d80a3063db8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2721 | 8f788087b05d0e67a7c4c65a366c64cb85670275 | merge staging into master (#42928) | AlreadyPresent | Medical | Merge effective delta is 4 files, +43/-16; CMU already retains the restored base/Vox gib thresholds and lacks the deleted popup.
2722 | 4f11f7021639752cf02de1c34f22268bac30169e | Fix uncooked animal proteins metabolism (#42942) | Deferred | Medical, Chemistry | Uncooked-protein metabolism and vomiting use the newer metabolism/VomitSystem substrate absent from CMU.
2723 | 15934385981a03ce0b1002db85376fbc7e887ecf | Automatic changelog update | Irrelevant | — | Generated changelog only.
2724 | 307aa0562f64ec816451362eb05bfa0ad9d289eb | Fix Thieving Beacon not detecting HUDs for said objective (#42945) | Deferred | Interactions, Gamerules | Thief HUD detection depends on a collection-objective path absent from CMU's retained beacon implementation.
2725 | d53be52b377daeaf162910d39998e6fdb93c7f2d | Automatic changelog update | Irrelevant | — | Generated changelog only.
2726 | f5bab1961f70f5bbefdbe3f16a141dd240cb6eb5 | Fix jetpacks not turning off when switching to another jetpack (#42689) | AlreadyPresent | Movement | CMU already turns off every other active jetpack when a new one is enabled.
2727 | cf831e2c4cf09be6ae316b711ee11cc8bc9a9fef | Automatic changelog update | Irrelevant | — | Generated changelog only.
2728 | 0b81cfb99eeb264f5d0ef4b01176915818a36597 | fixed barber scissors misgendering when blocked by a hat (#42948) | AlreadyPresent | Interactions | CMU's server MagicMirror handlers already resolve displayed identity for blocked-hat pronouns.
2729 | 5ec6d60d4ab6e199e00fcf1fde54a52d9ff0c7b1 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2730 | 2130cde6a3d095aaddc23fcf683fdab0b1263e55 | Fix reagent duplication from vomiting (#42954) | AlreadyPresent | Medical, Chemistry | CMU's RMC vomit path splits solution from the source, so the upstream duplication defect is absent.
2731 | 4751c7981f1f2265181fbabbefd9d44cc9ffb9e3 | Add missing test pair cleanup to SharedGasSpecificHeatsTest (#42763) | Deferred | Physics | Test cleanup depends on the earlier deferred shared gas-specific-heats test/API migration.
2732 | 39f302a3899874891286666cda1b31a164c12143 | New moth emote. Flaps wings! (#42912) | PortCandidate | Interactions | Self-contained moth wing-flap emote, icon, sound, and species configuration.
2733 | b7639b23a8d5ff27c786d4a036882c9ecfe1bf18 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2734 | b296dc85758eb56c891bfcd8b33533af11f9a793 | Added Emp interaction with Bar Sign (#42950) | Deferred | Interactions | EMP BarSign handling depends on the earlier deferred predicted BarSign system cluster.
2735 | 7d05dbccc86460ea33328fbcf3bc27f1fe2d7b27 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2736 | 6a19cd4941b545bea97c0aa70775d5bc239dd192 | Vox, Diona and Vulp Unique Sneezes (#42929) | Deferred | Interactions | Species-specific sneeze assets/config depend on candidate 2699 and divergent species audio tables.
2737 | 920fed4d77694ecfe945624d8cb1c315a58b4eff | Automatic changelog update | Irrelevant | — | Generated changelog only.
2738 | 0d3754d2b8a3f417c6d50f246875051a64b1a9d8 | Fixed Det coat armor status (#42969) | AlreadyPresent | Shooting, Medical | CMU's detective coat already inherits its armor base, so the missing-armor defect is absent.
2739 | f0ce055f825aa8ee3eb83e62924fb5b3c6e3b6d7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2740 | 2847b416b32024bf9ee01914b466c4546def4f20 | update basic operative bundle contents (#42930) | Deferred | Shooting, Interactions, Gamerules | Target-final operative bundle swaps equipment and removes an ID; this is CMU economy/loadout policy.
2741 | 3cbe2103f6185f911a56b1daf5d1a0c7b5485279 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2742 | 7e10c3eebf9ae1a9c953b2082fa0c66e06da5415 | Show fuckrules usage to admins (#42988) | PortCandidate | Interactions, Gamerules | Focused network flag and admin log report fuckrules bypass use to admins.
2743 | 24a28b39d138c3e720675d2bbb229464f009f768 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2744 | 9222e97fd59d18c92e44ccb55ae184146d9c96b3 | Add hand label blacklist (#42986) | PortCandidate | Interactions | Nullable hand-label whitelist plus blacklist extends an existing whitelist contract in a bounded path.
2745 | 7cba448845b4e52b8df8c9444379e407da599339 | More xenoborg names (#42984) | PortCandidate | Gamerules | Dataset-only Xenoborg name expansion is easy to reconcile with retained name pools.
2746 | f25dcc11d3fd21d48bb930c9bae5f3b96c781f81 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2747 | f661edf36ad218845dd6649e4807dc37f6bccc98 | Make prescription glasses actually work (#42990) | PortCandidate | Medical, Interactions | One correction-power value makes prescription glasses meaningfully correct vision; CMU still uses 2.
2748 | e517a04a5ea14c72f08bb286d00994bfab6f668a | Automatic changelog update | Irrelevant | — | Generated changelog only.
2749 | 274d4b6c7b8a65d85068351ea7e320fa4ee86050 | Update Credits (#42992) | Irrelevant | — | Upstream credits metadata only.
2750 | 67cb6dec635227d4ee7c2ea816f981a16ab3ac60 | Improved camera static shader randomness (#42968) | PortCandidate | — | One shader expression improves camera-static randomness without gameplay-system dependencies.
2751 | 0807fe320961fc6787ada6bdbe12626257676a01 | Move fuckrules CL to Admin (#42996) | Irrelevant | — | Changelog category move only.
2752 | 39f865576d79a66e9daf8e467f484a3bdc0f9385 | Predicted Networked Metabolism (#42798) | Deferred | Medical, Chemistry, GameTicking | Predicted networked metabolism depends on 2629 PredictedRandom and newer metabolism/Nubody ownership absent from CMU.
2753 | f7ec60c9099610681b95559f247077cab27b9676 | Minor Relic Fixes (#42921) | Deferred | Physics, Gamerules | Relic salvage-map changes belong to dedicated map and salvage-rule reconciliation.
2754 | 75d52f1b1e61d79538cd9a4ef6db9b2f163f554a | Automatic changelog update | Irrelevant | — | Generated changelog only.
2755 | d9c4cf1162718ea4f0e663e4b449d0092ec602e7 | Add unlockable reporter cosmetics (#41079) | Deferred | — | Reporter cosmetics span loadout prototypes and many assets across CMU's divergent role/loadout roster.
2756 | f1c1a97d4834e2bb87903aef85835c35d79835b5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2757 | 42063151d01657a062fad0d725eec7472e889767 | Oasis: Label every air alarm (#42911) | Deferred | Physics | Oasis air-alarm map rewrite belongs to map/atmos reconciliation.
2758 | bb28458147ff127e1bb3fcada95d0f8ce09aaa21 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2759 | cf74345ac62d2fe42dff54fffdda91b7fc7f3119 | Fixed Packed by adding extra APC to comms room (#42953) | Deferred | Physics | Packed station APC map change requires current-map and powernet validation.
2760 | ae26d205a675b46e0ebc8d4fc295f42884a5292e | Remake the "Vegan Meatball" salvage wreck (#42994) | Deferred | Physics, Gamerules | Vegan Meatball salvage-wreck remake is a map/content feature needing dedicated integration.
2761 | a714898b1933d4a34fffcb0300720cfd4aacfbc9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2762 | 47923bebefc3fa7905904375f4a4ea64fff1116d | EMP implant uplink texture (Addresses #42008) (#42998) | PortCandidate | Gamerules | One uplink implant icon path correction is independent of implant behavior.
2763 | 284693eeb46280987cfc92c84473a02ef525028e | Automatic changelog update | Irrelevant | — | Generated changelog only.
2764 | b584549803a3fd3e89aa3ad8f0d2f74607c88e55 | serpentcrest atmos/sec tweaks (#42905) | Deferred | Physics | Serpentcrest atmos/security map tweaks depend on the deferred station feature at 2675.
2765 | e9f8042e07c137d0106bc1eea9109ce2bd22db8e | Automatic changelog update | Irrelevant | — | Generated changelog only.
2766 | 04b4b7d51c2c85bec73f9e516dc51818122975a6 | Non-obsoletion warnings as errors in Release. (#42983) | Deferred | — | Release warning-as-error policy spans seven build files, including absent MSBuild/Content.props, and needs CMU build-policy reconciliation.
2767 | be4aaa0f958c80c75fae91e53d687aec27723789 | Lizard rehappy alternative (#42915) | PortCandidate | — | Single referenced lizard-happy audio asset refresh can be reviewed independently.
2768 | e2cc5de5704e9741f9545523c66c6f7184cdc9c8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2769 | 63457d041a0bd67ca93aa98ee76f182b13c9fed8 | Add TriggerOnRot Component (#42667) | Deferred | Medical, Interactions, GameTicking | TriggerOnRot assumes the newer generic TriggerOnX framework absent from CMU.
2770 | 0a55e5fbba8f8b0c05b586c182d2c9eec6aff06f | Largely Revert Unique Corgi Hardsuit and Equipment Sprites (#42696) | Deferred | — | The 172-file corgi/hardsuit asset revert needs target-final CMU asset-policy reconciliation.
2771 | 462f4d02b61ab435c2eaa9103f23da2d2bb30001 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2772 | 01c8cd8905fc008135440b82184c1b42caf2a9a9 | Revert "Remove dwarves" (#43027) | AlreadyPresent | Medical, Chemistry | Target restores dwarves, which CMU already retains with its legacy body/metabolism layout.
2773 | 65ff6c4c720eb7e9aa08bab12c9b82d9be360d6a | Fix repeated Localization warnings when viewing the Power Monitoring Console (#43037) | PortCandidate | Interactions | Focused Power Monitoring localization fix applies cleanly and removes repeated Loc.GetString warnings.
2774 | 7e5ddfd280f98ab31d6632c74bfc1301da96cae2 | Give borgs default prying (#41812) | Deferred | Interactions | Default borg prying changes 13 module/prototype files across RMC-divergent silicon equipment policy.
2775 | 2a274a0705763b5146b86bcfd9494702e918097c | Automatic changelog update | Irrelevant | — | Generated changelog only.
2776 | af71eec42bf2bda93f598da41f7f017d1b9f01e0 | Make HasMind not always false for client. (#43033) | PortCandidate | GameTicking, Gamerules | Small explicit HasMind networking fix addresses clients receiving null mind IDs; CMU retains the computed-field bug.
2777 | e774e11d0657e207f31fd04e83c0750de7e94cf2 | Add transfer entity to mind events. (#43020) | Deferred | GameTicking, Gamerules | Before/after mind-transfer event API depends on 2776 and has no standalone CMU consumer to justify the contract migration.
2778 | f981f8a32c04e67553d93c252d2f55e61fd4bdbd | Partially revert "Remove bad accents" (#43030) | Deferred | Interactions | Partial accent restoration must be assessed with the removals at 2703 as a single target-final policy delta.
2779 | 9ae88c418b3c9e3f662efffbc74892adf3b10abb | Automatic changelog update | Irrelevant | — | Generated changelog only.
2780 | 8315eaab7ea1d2247a1d4686e73eb3f23639ce62 | Revert "Cleanup Antag Selection Logic a Lot" (#43041) | Superseded | GameTicking, Gamerules | Exact reverse of 2630; reversed-revert and original stable patch IDs are identical.
2781 | ef21b128d32dbab55a68e74fac178025eea24f85 | Fix hypodart uplink description (#43035) | Deferred | Shooting, Medical, Chemistry, Gamerules | Text assumes a 10u hypodart, but CMU's actual reservoir, injection, and transfer values are all still 7u.
2782 | 4d5dab1098bcfdbce14906d9c77dbc669e295760 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2783 | 08db33b8b31fc06b06cdf634943987552c121547 | Stop AI knocking on shutters, blast doors (#42972) | PortCandidate | Interactions | One prototype-component removal stops station AI from knocking on base shutters and blast doors; CMU retains it.
2784 | 497888f22b682af1861378175f68e44fca0d792b | Add building animations to techfabs (#42962) | PortCandidate | Interactions | Bounded techfab running animation adds one asset/state and three existing Lathe fields.
2785 | 5a923b821cd29f23ec7c74ed52e350071666a51e | Automatic changelog update | Irrelevant | — | Generated changelog only.
2786 | 7bc062ee14a6549c961c6d5f4555035ad3a17951 | StrippableComponent timespan calculation fix (#43022) | AlreadyPresent | Interactions, GameTicking | CMU commit 28400205d7 already preserves full strip durations with the same tick-based calculation and tests.
2787 | e16fc10c158d3206708d18ab216ed38a99f977d3 | Fix Holosign Placement (#42909) | Deferred | Interactions, Physics | Holosign rotation fix depends on predicted holoprojector index 2645; the Dragon rotation hunk can be split-reviewed independently.
2788 | 9cdc009e006f66b9827bd2404ac9a96a25bea9a7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2789 | e361944ef62aba461790d44bdfa1a17c6701d7fa | Make DamageSpecifier serializable (#43049) | Deferred | Shooting, Medical, Chemistry, Interactions, GameTicking | Forty-six-file DamageSpecifier serialization migration changes a central damage data contract across systems and prototypes.
2790 | d21e95e6815e2492260bbb01b8ad48a4b157fd00 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2791 | f9f4e09af2eb9afa215860cf4bddc74e4c6c0bbc | Fixed redundant bar sign windows (#42960) | Deferred | Interactions | Redundant BarSign-window fix depends on the deferred predicted/EMP BarSign cluster.
2792 | e295d0adb362e6c7e0fc357c52091f92a380d0a7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2793 | 76741dc22b86df108f1e67ede78ccb886b5a328c | Make DamageableComponent manually networked again (#43054) | Deferred | Shooting, Medical, Chemistry, Interactions, GameTicking | Manual Damageable networking depends on 2789 and changes central predicted damage state ownership.
2794 | a3c6d0ddff919bd1388cc037a7111886e2382417 | Make sure vox passively regenerate at the same pace as everyone else (#43055) | AlreadyPresent | Medical | CMU still uses the base Brute group regeneration rate of -0.07 for Vox, so the slower per-type target defect is absent.
2795 | 2484ec7f360312b9a21b767d16eeeba47ab4d318 | Dynamic feedback popup (#43021) | Irrelevant | GameTicking, Gamerules | CMU omits the WizDen feedback-popup subsystem; round-end survey routing is product policy, not a content sync port.
2796 | 210fae0ffebf4a02b63a87c52cdfa6fb8c0aa0cc | Raise SolutionChangedEvent and SolutionContainerChangedEvent when handling SolutionComponent states (#42814) | Deferred | Chemistry, Interactions, GameTicking | Thirteen-file solution-state event expansion changes central chemistry state and prediction notifications.
2797 | 7f15e7795417b29541799b972c380fd689c6842e | Xenoborg extractor (#42796) | Deferred | Interactions, Physics, Gamerules | Thirty-nine-file Xenoborg extractor feature spans machines, actions, game rules, construction, assets, and locale.
2798 | 7ba3f7590b55ed435f5cb705419d4e578d4ddc6a | Automatic changelog update | Irrelevant | — | Generated changelog only.
2799 | adc66057ade41e3553cd8dc94fc209b936b47df2 | Adding serpentcrest to map pool + overhauling several rooms (#43061) | Deferred | Physics, Gamerules | Serpentcrest map-pool addition and room overhaul depend on the deferred station cluster beginning at 2675.
~~~

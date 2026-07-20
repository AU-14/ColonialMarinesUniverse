# SS14 upstream inventory: wave 0013

Audit date: 2026-07-20

- Pinned baseline: `59633f6dc50e77dda8cefa344d87c7b01e06a810`
- Pinned target: `40ca2c7f90d11d27be5457d177c133f0947d1c08`
- Range: ordered first-parent commits, zero-based indices 2400 through 2599
- Columns: index | full SHA | exact upstream subject | disposition | core-system areas | rationale

`Ported (CS-####)` links an accepted core-system change to the durable audit, while
plain `Ported` identifies accepted non-core work. `PortCandidate` retains target
behavior that still needs integration. `AlreadyPresent`
means CMU already has equivalent behavior. `Deferred` preserves behavior pending
focused reconciliation. `Superseded` means another target or local architectural
change replaces the commit. `Irrelevant` identifies commits with no standalone
behavior to port.

~~~text
2400 | 57e9a64d274dc428820f40ad4d42bab79f46634a | Automatic changelog update | Irrelevant | — | Generated changelog only.
2401 | 0d751543856ab408ec7388cedbd8ba0886a366d1 | Fix core pinpointer pieces having a 5-pointer recipe (#42446) | Deferred | Interactions, Gamerules | Borg/core recipe cluster diverges in CMU.
2402 | ab9cf3b5ccac79ac928ba1ed98ab2fbd722c7e53 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2403 | 2dca0785d7c21cd63117c915723ca339a8c29884 | Fix Zombie Resistance Probability (#42451) | AlreadyPresent | Medical, Interactions, Gamerules | CMU's older zombie path already uses the correct positive probability check.
2404 | d0352e734d97f3762f2e9082ea61bd2f83101874 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2405 | 40c634d7e2c6ad1beabc9c45b7c97b941e5af8de | Adds more starting materials for the mothership (#42448) | Deferred | Interactions, Gamerules | Mothership inventory and balance policy diverge.
2406 | 12592bf5299a650084e84a297133ef5a4a142600 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2407 | 8a59ead61e791ea5d93e7c528f59875f5d270303 | Admin Anomaly Scanner (#42443) | PortCandidate | Interactions, Gamerules | Self-contained administrative utility and content.
2408 | 3cd407e7a68ea8ae8449472ec8b4dd65cf3a6f37 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2409 | d0c2734dad6b2ecfda62738a03539a04904d23d6 | Nubody (#42419) | Deferred | Movement, Medical, Chemistry, Interactions | Large humanoid and body architecture migration.
2410 | 4e3cc7b7be623326fa8725f8891c6ad8035b3107 | Adjust the role timers for certain roles. (#42372) | Deferred | Gamerules | Server progression policy.
2411 | 2ad1d1484f54575fad0b567a15a8f925dce50ce4 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2412 | 362d09e8af13803f1efa0cb7629a49feebd4126e | Update Credits (#42491) | Irrelevant | — | Upstream credits metadata only.
2413 | 983457cfe332f77b473c48c7db6e067753ed1733 | close pull requests from forks' stable and staging (#42456) | Irrelevant | — | Upstream GitHub workflow policy.
2414 | af3547fd33c379e5fa6af40d2dc8f357f48ffc0c | Let vox eat trash again (#42503) | AlreadyPresent | Medical, Chemistry, Interactions | CMU's Vox stomach already accepts Trash and is nonexclusive.
2415 | 400523d814cce07478af596a0dc4a294732889d1 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2416 | f068cd885b176d1759c93b80b4f6e88e5054ab9a | Fix inventory contents not being dropped on gibbing (#42504) | Deferred | Medical, Interactions | Depends on the deferred new BeingGibbedEvent and Giblets contract.
2417 | 72fedafef5a85e51004569a11694742c6001cff5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2418 | 0f28c568a188955ffcfa6d6d60e6640b2cbc343c | Make sure simplemobs ghost on movement when dead (#42506) | Superseded | Movement, Medical, GameTicking | Index 2590 replaces this placement by moving GhostOnMove to BaseMob.
2419 | fa0e98e6e18fc90cd55dfa8f5cc43cb6f22135f5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2420 | 2ef4f158cd50029745bc3d5b8a511c59ea92b082 | stable merge 2026 01 17 (#42508) | Superseded | Interactions, Physics, GameTicking | Effective first-parent delta is 21 files, +647/-851, removing the first tile-stacking attempt; indices 2455 and 2464 establish target-final behavior.
2421 | 9dd31220b70e82b030c8a6af5aad500001f2b808 | Magic 9 Ball (#42189) | PortCandidate | Interactions | Self-contained toy and responses.
2422 | 076e7d67472bebbbe5fbb6d0edddbedfe510d0c8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2423 | 820fdca6efa9ea4c8390a5b9b8bb783b2759791b | Round-start equipment rebalance: Medical (#42423) | Deferred | Medical, Chemistry, Interactions, Gamerules | Broad loadout policy; one ChemDrobe portion was selectively ported by CS-0152.
2424 | 1e573a87198461d406c7a912ef022a6e0ba586b4 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2425 | 76c09a6b1a53eeb216af6dd1678239b693c25558 | Thieving beacons automatically set coordinates when unfolded. (#42520) | Deferred | Interactions, Gamerules | Depends on the deferred shared and predicted thief-beacon migration.
2426 | 27c803aeb2de89e1ddffa2753190541ef1789b00 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2427 | 966f244048751fa69a92fefa54231fc50448db22 | Adjust various traitor explosives (#42477) | Deferred | Shooting, Interactions, Physics, Gamerules | Uplink and explosive balance policy.
2428 | d75fc29a84bad9b1508c5d775c91879436b0b702 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2429 | fa9173c0ec788b940524ead08181e5bcbc44ccc6 | Traitor Chemicals Rebalance (#42484) | Deferred | Medical, Chemistry, Interactions, Gamerules | Chemical and uplink balance policy.
2430 | bae02728886d4c7e7e19908310be27582c56a311 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2431 | b0ddb0e64df2e0a2993b1c9391ad55e7e6ce1393 | Syndicate Wearables Category Rebalances (#42482) | Deferred | Interactions, Gamerules | Uplink category and price policy.
2432 | 0174e1f47d8658fafd478fcbf6b604e83b3cfc3d | Automatic changelog update | Irrelevant | — | Generated changelog only.
2433 | 159b0646a695c0c114de4977757b1b1660b48c5f | Syndicate Weapons/Ammo rebalances + Weapons Case (#42468) | Deferred | Shooting, Interactions, Gamerules | Broad weapon and uplink policy.
2434 | e62c785bf1430f941a2e4b389e77f5907df29805 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2435 | daccef7b43f24687feb7aa780f4bd9b0f162fd39 | Rename "Inset" style, implement in sheetlets (#41975) | PortCandidate | Interactions | Localized UI style cleanup.
2436 | 88877ff1dab9ad29ab5322eae80b161b091bd5df | Make Seed Non-Unique on Sample (#42527) | PortCandidate | Interactions | One-line sampled-seed correctness fix absent locally.
2437 | 5292f86d6b7384909fc20c360bbdfe1339c724f8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2438 | 950a329fce1b33acbf5546c4d59c6a0f97620291 | Remove loadout time towels (#42536) | Deferred | Interactions | Towel and loadout policy cluster with index 2463.
2439 | 50a4cffbc5183ae9e303c8bac30ac1c7acfd6dcb | Automatic changelog update | Irrelevant | — | Generated changelog only.
2440 | 27bb73ded4942515a93f1dbae2eed31caf040904 | Change back some medical loadout timers' names (#42538) | Deferred | Medical, Gamerules | Medical timer naming depends on upstream role policy.
2441 | 8cf744ec55fa19968ae5a7bc279d5b3862e3f878 | Visual nubody (humanoid appearance refactor) (#42476) | Deferred | Movement, Medical, Interactions | Depends on the Nubody migration.
2442 | 687033d4afc3a86286d220e98fb26373de213bcd | Automatic changelog update | Irrelevant | — | Generated changelog only.
2443 | 5d5a7e8929294742580d95898f3f20f1b855680d | Inflatable inflation (#42539) | PortCandidate | Interactions, Gamerules | Cohesive inflatable behavior and content feature.
2444 | 6240f59a29167a8a1846c4bad7be923fb270dddd | Automatic changelog update | Irrelevant | — | Generated changelog only.
2445 | 04f0e5231cf15952e6a0184b8c33bfbf0cfca586 | Update RT to 271.0.0 (#42533) | Superseded | — | CMU already pins a newer RobustToolbox generation; the engine submodule is outside content-port scope.
2446 | 7f7f3b6ef105cfff28265c0e51e651d618dcb006 | Fix MagicMirror UI (#42544) | Deferred | Interactions | Written against the Nubody appearance model.
2447 | 40f5f31a8e6dd0e690446205ca4d717f3659de92 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2448 | b86b6e0ff019a74b61dd477a78edb5a72a4318e8 | Examination verb for insuls (#42444) | PortCandidate | Interactions | Focused insulated-examination feature; integrate with index 2522's final localization correction.
2449 | 93247d961c6acdbd7ef17b711ed99f63be00cc64 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2450 | 33df83508c424c9efe05cc59712a2650e7a70a00 | Add sowelipililimute as codeowner for body and humanoid (#42549) | Irrelevant | — | Upstream ownership metadata only.
2451 | b105944b3f164cbcebb96849cebc2fdae7c1275d | Update RT to 271.1.0 (#42551) | Superseded | — | CMU already pins a newer RobustToolbox generation.
2452 | efb6c68ac802aff55cd5c591bb9deadf98e7abb3 | Fix humanoid profile voice being broken (#42550) | Deferred | Interactions | Depends on the Nubody profile model.
2453 | 6c2ca3ce7360f9ec370c16eaf570afe41da98ba6 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2454 | c9bb2ddf7cd7bb49bf969ab29853be35329bfbe8 | Fix Changelog (#42552) | Irrelevant | — | Upstream changelog maintenance only.
2455 | 59e10d2854116888f3b0e60b105aeb3e47f77eb3 | Tile Stacking - attempt 2 (#42543) | Deferred | Interactions, Physics, GameTicking | Large tile-system contract change with RCD and explosion dependencies.
2456 | 0673809762ee1cf769d5ec4eb18fe2e5892b8f06 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2457 | 892209db0184e5dfc09c0672cb5823fa58fb5cc4 | Medibot doAfter and some other improvements (#32932) | Deferred | Medical, Interactions, GameTicking | Broad AI, treatment, DoAfter, and content change.
2458 | a913216675d8a474ed6276636af26ceb737722e7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2459 | 4216e29fdb2097d8a5459d2445d8639172da62c9 | Fix sexed organs (#42554) | Deferred | Medical, Interactions | Depends on Nubody organ and appearance architecture.
2460 | 2cd832b67e0022cf6e58336460c2704ed017d3ca | Automatic changelog update | Irrelevant | — | Generated changelog only.
2461 | cf19062414953efdbacf0dbfaf0184b22df3669b | Fix handheld grinder and reaction mixer audio stacking (#42498) | Deferred | Chemistry, Interactions | Audio and prediction architecture differs locally.
2462 | 423accb9a243bfcb40c843bdddd8e6ea181c3d67 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2463 | bd40b85d7408459804f19d5feab2c7b9f13a1368 | Towel migration (#42555) | Deferred | Interactions | Large towel migration coupled to index 2438.
2464 | fb82cadc9f42cb5b119fe0150b2b6481669391e9 | "Fix RCD light spam, bypass of indestructible tiles and some plating fixes" - Tile Stacking got merged, time to bring back the RCD fix (#42556) | Deferred | Interactions, Physics | Depends on the target tile-stacking system.
2465 | 3618b611243e886e91c94dac2ba26dc3e542ef09 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2466 | acb685f3f93ce90719adaa86efbbc7830f82b0f9 | Remove "Fuck Lizards" and "Lizard Power" decals from crayondecals.rsi (#42541) | Superseded | Interactions | Intermediate asset state; target-final behavior reintroduces the pro-lizard decal.
2467 | 0ec9975e4fe9d9be6b83673bdbb6c041a091aa4b | Fix hideable humanoid layers (#42553) | Deferred | Interactions | Depends on Nubody visual-layer architecture.
2468 | 833f567fdb4c2627acccef4bbb0ce64cfb99cb4d | Automatic changelog update | Irrelevant | — | Generated changelog only.
2469 | eb5b0c558e7c3c4722aa3fc36c59337316fcbdbb | FIX: Give RCD plating unique name (#42560) | Deferred | Interactions, Physics | Coupled to RCD and tile-stacking prototypes.
2470 | 1b1cb64d24e244b804a8c908256f1f06c7ff4056 | Power Consumers Rebalance: Simple Dynamic Power Loading (#41961) | Deferred | Chemistry, Interactions, Physics, GameTicking | Broad simulation and balance change.
2471 | f7ae0b0617481472e857632a6b856be6752c7fab | Automatic changelog update | Irrelevant | — | Generated changelog only.
2472 | 3c48696b16b32aa1213b88a8b451937f1e275fee | Add aloe cream storage sprite (#42453) | PortCandidate | Medical, Interactions | Isolated missing storage sprite state.
2473 | 6cfedfa34ff827677988d0c6af5ef8b8705a54e6 | Ensure cat ears & tails cannot be selected by players (#42579) | Deferred | Interactions | Species and appearance policy with Nubody-adjacent data.
2474 | 5fd2b84a7d9a6cbdb5f04d71cbf1e50cbbb8c265 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2475 | 3a23bf5e055d4b8da82c2c1d95e2955239e02d42 | bagel update (#42558) | Deferred | — | Large upstream map update.
2476 | 69cd61ac2ebcebd0efd316354a51cb2f6c10597c | Tweak traitor deception items (#42510) | Deferred | Interactions, Gamerules | Uplink balance policy.
2477 | 1bfe7a0d1136a1d378ce1aac23288335754d7bd8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2478 | 1a0b108bdd3dfb2c6daba1f2a6fc54eaaf917c69 | Force-prying crit borgs opens borg panel (#42460) | Deferred | Medical, Interactions | Borg damage and panel behavior requires CMU reconciliation.
2479 | dc4473942048632ee830911cce51f223d030246a | Automatic changelog update | Irrelevant | — | Generated changelog only.
2480 | 8e1145b1fca8bfb6e3b858b54d3146d8de74cc66 | Cargo console rework (retry) (#34052) | Deferred | Interactions | Large UI and cargo workflow replacement.
2481 | aad796665fc8f3829614faad678809b63a302c80 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2482 | 6ee812cfe5a6e8201e1ac6647dfd22f4731c87c4 | SwitchButton (#39161) | Deferred | Interactions | New reusable UI framework needed by index 2594.
2483 | 4eb55ded54cc5233b21f8f2b36e4aa0093e2e911 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2484 | e9be97dee0a327edee22b1f2e5f7bea76f63e244 | Colour picker, palettes, & other spraypainter stuff (#41943) | PortCandidate | Interactions | Cohesive spray-painter UI and content feature without a core simulation dependency.
2485 | 1e0e1edfbda5c73c850c8da7c9c2d9868081dc20 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2486 | 6d4f622977d9049a545b076d102950d3a61ef823 | Character editor style fixes (#41278) | PortCandidate | Interactions | UI-only styling corrections.
2487 | c58e63c5c154c0806efce2e552dc2072b5dcec89 | Makes defib cabinets constructable and deconstructable (#42571) | PortCandidate | Medical, Interactions | Focused construction and content addition.
2488 | 0a5cd80e2270375271c31fd490fc33e35039540f | Automatic changelog update | Irrelevant | — | Generated changelog only.
2489 | 5a89088d39bb391676dae179abbfa44f3507c8e5 | Make some of the arachnid metabolisers animal ones (#42529) | AlreadyPresent | Medical, Chemistry | CMU's arachnid organs already inherit or use animal metabolism.
2490 | 59d8495cc72b8b3e6841e83539d0cdc82a083ea1 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2491 | 6f8242c1fc922ea1d6dc5b5423f89ab48e2da24b | Simplify hands UI code (#42534) | Deferred | Interactions | Structural hands-UI refactor with low direct payoff.
2492 | dfeb9f6bd308e8bd4ddea11e4fae29d6776225cd | Automatic changelog update | Irrelevant | — | Generated changelog only.
2493 | 5d2988d5ff609cf84e686624a474c39256ee99fd | Grappling rework - Grappling hooks are now physics-driven (#42409) | Deferred | Movement, Shooting, Interactions, Physics | Large movement and physics mechanic replacement.
2494 | 39e2b8a9a62d433d3c2801d1ab915a3c4e7cadd2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2495 | bcd36127301735eeda37cff3f2d3c0d9ebba74e0 | Add feedback popups (#41352) | Deferred | Interactions, Gamerules | New feedback-service and platform cluster.
2496 | 64f96e106986bbacdfc9b7720e0e96b2e9eb0008 | Fix construction ghost sprite offset (#42193) | PortCandidate | Interactions | The sprite-offset bug remains locally and CMU's current API supports CopySprite.
2497 | 0096ebde47f615083f6523fd1944e66453b31bce | Automatic changelog update | Irrelevant | — | Generated changelog only.
2498 | 31adfa297b711157fd95cbed2c3557cea9590933 | Replaces thief beer goggles objective with stealing HUD items (#38043) | Deferred | Interactions, Gamerules | Thief objective policy.
2499 | 1ba1e5dffa4c171f2f91c6f468fc2ff42c92d21b | Automatic changelog update | Irrelevant | — | Generated changelog only.
2500 | 940eb7b6a536124e7635dcb4728edda25210d7ca | Fix typo in feedback popup  (#42587) | Deferred | Interactions, Gamerules | Depends on the index-2495 feedback feature.
2501 | f3db27da10d88c550ba170b987a00979eb8b8ba5 | Fixes grappling hook audio infinitely looping (#42588) | Deferred | Movement, Shooting, Interactions, Physics | Depends on the index-2493 grappling rework.
2502 | ff1af35afba1c494efff277acb6c7d79865ad403 | Replace metabolism groups with metabolism stages (#42172) | Deferred | Medical, Chemistry, GameTicking | Large chemistry and body data-contract migration.
2503 | facd7da3942f8304d5b73f7fa9d7a70cd3eff42d | Automatic changelog update | Irrelevant | — | Generated changelog only.
2504 | 29b7fc4463c81a6d7d258d97e538652836509735 | Stable to master (#42599) | Deferred | Gamerules | Large ban-database refactor and migration.
2505 | 371aea8d79bd95d0244a59213f93abd2ee50fee7 | Fix master (#42610) | Irrelevant | — | Merge has an empty effective first-parent delta.
2506 | b4a3358b4b7a120c8d5df90f592762f9b96cda5a | Stable to master (#42611) | Deferred | Gamerules | Effective first-parent delta is one file, +8/-8; its migration-order fix is inseparable from index 2504.
2507 | 76801bd8b2226432ad0a0cdb452f1be782d8cc0c | Tweak Traitor Uplink - The Rest of the Uplink (#42582) | Deferred | Movement, Shooting, Interactions, Gamerules | Broad uplink balance policy.
2508 | 235eba881c775811bd0a92c9986e5a80ade0792e | Automatic changelog update | Irrelevant | — | Generated changelog only.
2509 | 007aca75c3f66d5eed7457ab6d9d9c02e02035bb | Fix holoparasite stun (#42315) | PortCandidate | Medical, Interactions, Gamerules | Focused holoparasite stun correctness fix.
2510 | b710525d2f9aee3efb67b448c1321d6880ae8094 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2511 | 7c03f61e31c0bc3bfb45c1a0d07093898332f96e | Admin log now shows who called or recalled evac (#41557) | PortCandidate | Interactions, GameTicking, Gamerules | Focused administrative observability improvement.
2512 | 351fbed6b8639ad1949b1829dc7bd44cd0e14a95 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2513 | 6ce33a463cedbbb333e0116fafc1fed1c417753f | De-panic bunker Vulture & set Cvars for feedback panel (#42612) | Irrelevant | — | Upstream deployment-specific defaults and feedback infrastructure.
2514 | 2977d89f3e93654126d2cd7c4a272e080929cf48 | Cleanup warnings: CS0168, CS0414, CS8321 (#42623) | Irrelevant | — | Mechanical cleanup tied to upstream source state.
2515 | 69168f81d865a792670492cc74390eb65a31dbff | Improvements to automatic job highlights (#42630) | PortCandidate | Interactions | Focused UI usability improvement.
2516 | 0d32aad06add394928c828fa6bf8a262da8ac5be | Automatic changelog update | Irrelevant | — | Generated changelog only.
2517 | e4c83a4040804172f202627f39d37ae5884ee250 | Fix roundstart with thief gamerule (#42633) | Deferred | Gamerules | Depends on upstream thief rule and objective state.
2518 | de3311e9e43eedab36e07a7c92f31d18fc092f76 | Update Credits (#42636) | Irrelevant | — | Upstream credits metadata only.
2519 | 1682ad243fff96282cd3b3d91a19299140838e8d | Update RT to 271.2.0 (#42646) | PortCandidate | — | The content-side RemoveReadNan setting remains relevant; the RobustToolbox bump is superseded and outside content-port scope.
2520 | ae5f8d0a6c77b736917c9eed261e254dfc26b777 | Fix emergency shuttle authorization bypass via ID rename (#42640) | Ported (CS-0244) | Interactions, GameTicking, Gamerules | Authorizations now use immutable card entity identities while retaining captured names only for console display.
2521 | 6daca9bd9665bc4e85c7dfe78cf9732f7f055f9e | GasLeak and PowerGridCheck rules components cleanup (#42624) | PortCandidate | Physics, GameTicking, Gamerules | Focused component and rule ownership cleanup.
2522 | f6a06db1fc47324d626bb4e3e225286b529af854 | Translation fix: insulated verbs (#42617) | PortCandidate | Interactions | Final localization correction for index 2448; integrate the pair.
2523 | 87895770856245557d9d55b9dad08cf6aab67f96 | Add an option for hold-to-attack in settings (#42596) | Deferred | Shooting, Interactions | Input and network behavior addition requiring CMU UX review.
2524 | f94e809f5387f88b0410052d404ef2f25ab1a0d7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2525 | e6dedb87a64a91d47e01c633cfffdb33c607b629 | Add EditorHidden member to ContentTileDefinition (#42564) | Deferred | Interactions, Physics | Tile-definition and editor API dependency.
2526 | 11ac6966d9812e6460b1ba31c5c75e987ba4434d | Fix: Make votes force select maps (#42426) | PortCandidate | GameTicking, Gamerules | Focused map-vote correctness fix.
2527 | 65b8aafed8aa87e4104efd9b4c726e1eed7fe0bc | Improve sandbox window toggle buttons state handling (#42281) | PortCandidate | Interactions | Self-contained administrative UI state fix.
2528 | 7b1ed2bd29eb797c594e9354747f5564d0138cfd | Remove duplicate loc getstring calls (#42648) | PortCandidate | Interactions, Gamerules | CMU contains the erroneous nested localization calls.
2529 | ab2cefaa7f72142a85fe1b9e0e818870c53e1cdf | restore tritium fire energy to reenable maxcaps (#42641) | Deferred | Chemistry, Physics, Gamerules | Atmospherics and explosion balance policy.
2530 | 18bf23dc7a927b5a2efc835861674345571d4cb2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2531 | 737889c22f4651d5aa1d3cee53d44218234604bd | Make crowbars consistent with 1x2 item storage (#42585) | PortCandidate | Interactions | Small storage-shape consistency fix.
2532 | 3afdaaaa5ae9f802b94b23b0b9a484f6fb495c2c | Automatic changelog update | Irrelevant | — | Generated changelog only.
2533 | 044aa4c8dc4d28af3493f984964801c1c456a63b | Add Part Assembly and Temprature Construction Validations for Dev builds (#41396) | AlreadyPresent | Interactions | Ported as CMU CS-0015.
2534 | 202b844967de80b4aed8bfc6c536d2820d876383 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2535 | e4ce0a7fbe1fc73946db995cc85c272e6e0eb927 | Fix dev map med APC overload (#42157) | Deferred | Physics | Upstream map-specific generated change.
2536 | a35a48c351c24ea2230e8646d38da942880407ee | Fix sound issues with arti crusher. (#42406) | Deferred | Interactions | Moves behavior into newer shared and predicted audio architecture.
2537 | a237493841100673de05dc05c018fc0d02afd3a0 | Prevent picking up chameleon projector disguises via context menu (#42656) | PortCandidate | Interactions, Gamerules | Focused context-menu exploit fix absent locally.
2538 | 235ad21f22edf2d02c59c86fafdc1e2daee5d300 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2539 | 256ecd3c468e023ae5f4071e62b6b0f2e356c999 | Refresh gas canister UI on canister startup (#42616) | PortCandidate | Interactions, Physics | CMU's canister system lacks the MapInit and UIOpened refresh hooks.
2540 | fdd8c2a1f6db2fe5c8d178b9f4353e407b11b05a | Automatic changelog update | Irrelevant | — | Generated changelog only.
2541 | 52155802e38c10612ca8197ebe98feec7b334053 | Doors can now close on clown spider webs (#42589) | PortCandidate | Interactions, Physics | Focused fixture and collision behavior correction.
2542 | b33c780a6c5888db376a5aaa92e8472f5ed27c04 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2543 | fd0f52592788f0e1b0d7485c8c4dd83161d905f2 | Fixes Opporozidone Instarot issues (#42472) | PortCandidate | Medical, Chemistry, GameTicking | CMU's rotting shutdown still resets scheduling incorrectly.
2544 | 36d09f982b40e692851ada4c1b4ba7cf455c16c9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2545 | 5b9ff83ce5ed68aeda6f2b0b17d273e42c5030a9 | Fix: Make vote call button toggable (#42450) | PortCandidate | Interactions | Focused vote-button UI-state correction.
2546 | 093257280bd7ea71516553d825aa581f598da570 | Fix InstrumentSystem.Update exception when deleting band lead (#42331) | PortCandidate | Interactions, GameTicking | CMU's cleanup path does not continue after clearing an invalid band master.
2547 | 9a5c2793261d8f2150ab0272845c9d8d4a3c7a2c | Move job weh plushies to locker loot (#42545) | Deferred | Interactions, Gamerules | Job and locker loot policy.
2548 | 4981392249bff86aa2ab2745b1ccb22c6382684d | Automatic changelog update | Irrelevant | — | Generated changelog only.
2549 | 3172ada5636fc068ac1f85893ba4c4cab0615f26 | Move character preview handling into a specialized control (#41252) | Deferred | Interactions | Structural profile UI refactor tied to Nubody work.
2550 | 97f73daaa5404e2818d9ed9f731908994105e44c | Replace Regular Boxing Gloves with Rigged Boxing Gloves in the Uplink (#42662) | PortCandidate | Interactions, Gamerules | Small isolated uplink content correction, pending CMU balance approval.
2551 | 4ddac1463ced81b7f5484f80bc0cbc7662752bc8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2552 | 4e9a456591aa9c48527e131ee2f2864fdf6d063f | Drop ICharacterProfile/ICharacterAppearance interfaces (#42661) | Deferred | Interactions, Gamerules | Broad profile API cleanup tied to upstream character architecture.
2553 | eb763886cdda9364ffb3a9a99d71b531d98ae57b | fixing the handling of the RespiratorComponent without checking (#42665) | AlreadyPresent | Medical, Physics | CMU's runtime already resolves the optional respirator safely.
2554 | b625152511acf08678f0ecc746840ae0211e0ede | Add the Uplink changes to feedback popups. (#42649) | Deferred | Interactions, Gamerules | Depends on the feedback-panel and uplink clusters.
2555 | d11f3fb3c1e9b387071bf82c623a519392ab812e |  Content.Packaging can now emit binlogs for the build (#42659) | PortCandidate | — | Useful isolated build-diagnostics improvement.
2556 | 8dbdb19e0cb9fb5beba6f39e3e28ee03c6840f94 | Fix 42643 meat spike doafter race condition (#42644) | PortCandidate | Interactions | CMU's construction graph lacks the body-container-empty deconstruction condition.
2557 | 80b0239c6eb79eecf14d6fd21c6bb526a40c48f0 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2558 | 9e6b0f68cd4e07941b4a3094a09bd2788bf4f741 | Some bonfire fixes (#42675) | PortCandidate | Interactions, Physics | Cohesive fixture and fire behavior corrections.
2559 | ed3a4d8e57e740e5e37bf56ece8e0f38ab03a87c | Automatic changelog update | Irrelevant | — | Generated changelog only.
2560 | ec572807f641d1c241acde2276f4efcacdd68131 | Add Attribution for Web Walls. (#42677) | Irrelevant | — | Upstream attribution metadata only.
2561 | ffd9c22badb85f4e7f45bae7a7eeede931e85e08 | Fix incorrect tip (#42678) | PortCandidate | Medical | Small player-facing medical tip correction.
2562 | 0c2b17b65cbf623f9cd18ab4c28f53a66a7e7563 | Removed duplicate disposal unit in Oasis Kitchen. (#42670) | Deferred | — | Upstream map-specific entity deletion.
2563 | 58dc0c1dcdfdfecde597c0b1b0bbf4682e978fd8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2564 | 479579e86ad188fa3b8707f4982a4c1012fd0848 | Remove Visitor Shuttles (Real) and also cleanup the event rule system to not break when I try to do this. (#41915) | Deferred | Interactions, GameTicking, Gamerules | Large event-rule cleanup plus upstream content removal.
2565 | 4b65de4a7e8ccd0ab7655136354fa4d844e2139c | Automatic changelog update | Irrelevant | — | Generated changelog only.
2566 | 7d7ca778374beac0a360b8ff94e184a4ecf83c82 | [FIX] Fix spacing explosions ignoring indestructible flag in stacked tiles (#42682) | Deferred | Interactions, Physics | Depends on the deferred stacked-tile representation.
2567 | 9bb1e7a7ac448c51c002da265aa14b3c554ad728 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2568 | ceb175c92d68a324613b1ebe6e2167bd35e8c9a0 | Health Analyzer Reactivation (#42608) | AlreadyPresent | Medical, Interactions, GameTicking | Ported as CMU CS-0031.
2569 | b2526366e25b456b3d8e58949d20bd0602700168 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2570 | 57b73101c98bdb2159ea65ec12c965c4ddc8b06e | Lizard Unhappy (#42594) | Deferred | Interactions | CMU and RMC voice prototypes still reference the affected asset.
2571 | 275facfbb15778c428ce619ce51f969e657a3e39 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2572 | e4e9371c73ffd2e78c38e74d13452396969ebe63 | Update RT to 272.0.0 (#42694) | Superseded | — | CMU already pins a newer RobustToolbox generation.
2573 | 1f8365fe9db8a1f7ccc9fb5d18c92a9e6c9eda32 | Log Criminal Status changes for admin panel (#42691) | PortCandidate | Interactions, Gamerules | Focused administrative audit logging.
2574 | 338503b58e8eac887edeb7f289358f89dc117529 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2575 | 407664a5364b4638be97936e9f77018fec9824ed | Add Cyborg crew indicator (#37038) | Deferred | Interactions, Gamerules | Larger crew-monitoring and borg presentation feature.
2576 | 236eaa1fe13292455a48d57456d1870290b894ce | Automatic changelog update | Irrelevant | — | Generated changelog only.
2577 | 4537da55b002db34e47040ca09984f1883097c59 | Estoc DMR made Nukie Only (#42698) | Deferred | Shooting, Gamerules | Weapon availability and balance policy.
2578 | aad8729176cf12df59896b2521bae8e3eb31ccdd | Automatic changelog update | Irrelevant | — | Generated changelog only.
2579 | 2a268a5c25df8ac156c75a719a02b10fdbe8e02c | Add fontconfig and pipewire to shell.nix (#42700) | PortCandidate | — | Isolated developer-environment dependency correction.
2580 | 801b024e65cb726348596746d873e578b91cb727 | [Admin] Made admin log be high if the buyer is not from expected faction for a store. (#42687) | PortCandidate | Interactions, Gamerules | Useful suspicious-purchase logging; reconcile expected factions with CMU stores.
2581 | 750441e94d63e1978d88525e514b3225bace9d54 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2582 | 947faf411a7e9ceb9db507e5edfb3215852ddea8 | Remove InternalsComponent from BaseSimpleMob (#42705) | Deferred | Medical, Physics | Base-mob physiology policy differs and is Nubody-adjacent.
2583 | 1d96eb2e7c6ee8e4047fc4d09fdbd761f3b89312 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2584 | c54ba1c61f2e252a3fead8d19bb56b1fc8f3f3ce | Fix debug assert when using Control Entity verb on inanimate objects (#42525) | PortCandidate | Movement, Interactions, Physics | CMU's MakeSentient path lacks body-type initialization; physics behavior needs focused tests.
2585 | 9393d624d7f196d8ff9f24bc31bfa0d96d0c1dda | Ghost types  (#37949) | Deferred | Medical, Interactions, Gamerules | Broad ghost, mind, and body prototype redesign.
2586 | cb587ce73401123cb6c0c03ee44b105de80f8cee | Automatic changelog update | Irrelevant | — | Generated changelog only.
2587 | 93e276d6e7ea1c6d6146068d76494106a0d6b46d | Remove writing apps from the Mime PDA (#42706) | PortCandidate | Interactions, Gamerules | Small isolated loadout content correction.
2588 | 1ac141dac2229e6ff91b812ed8bcfda95ff8180a | Automatic changelog update | Irrelevant | — | Generated changelog only.
2589 | e533921cb2a10f255f82127e7a847a66c9dc3334 | Entity Table Probabilities API (#41920) | Deferred | GameTicking, Gamerules | Broad entity-table contract change used by event rules.
2590 | 9a47e0f61da0d5c6d52f9cb8052b832042b9503c | Stable merge (#42732) | Deferred | Movement, Medical, Interactions, GameTicking | Effective first-parent delta is six files, +9/-5, mixing Nubody, gibbing, ghosting, and feedback reconciliations.
2591 | 728f3eac2a03f66b386e26be4608180820d515c7 | Sent fax now tells where it was sent from (#41108) | PortCandidate | Interactions | Focused fax usability and administrative-context improvement.
2592 | 66615bf6aab832183c9e9495941f2cc7c6bfba50 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2593 | a0e6d0553aac174de0ad817b973aecbf651e4af4 | Atmos GetAirflowDirections API (#42668) | Deferred | Physics | New API has no CMU consumer and belongs with later atmospherics work.
2594 | 118b83165bda29e3651e97796976558f3cd42bb0 | Gas device power switches use switch buttons (#42619) | Deferred | Interactions, Physics | Depends on the index-2482 SwitchButton framework.
2595 | ce97c45dc29de1333702c4b71846843736289d21 | Admin log new state on emitter toggle (#42736) | PortCandidate | Interactions, Physics, Gamerules | Focused administrative logging improvement.
2596 | c42a04b81b69a07dd5cd6fd572190c1be2d84a30 | Automatic changelog update | Irrelevant | — | Generated changelog only.
2597 | a85711a1cdeed59378c8deabccf0a9a005c6806f | Give HoP genpop enter and leave access (#42729) | PortCandidate | Interactions, Gamerules | Small isolated access correction, subject to CMU access policy.
2598 | 1fa968e34183391f533c6425d0f0c567647e6a8f | Atmospherics DeltaPressure Bulk Processing (#41553) | Deferred | Physics, GameTicking | Large atmospherics performance and behavior change requiring profiling and simulation tests.
2599 | fae9b35aaac5b50fbb87dc689e5260fd70c0ecf4 | Clarify documentation on Atmospherics heat capacity APIs (#42747) | Irrelevant | Physics | Documentation and analyzer annotation only, with no runtime behavior.
~~~

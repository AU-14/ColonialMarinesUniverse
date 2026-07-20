# SS14 upstream inventory: wave 0010

Audit date: 2026-07-20

- Pinned baseline: `59633f6dc50e77dda8cefa344d87c7b01e06a810`
- Pinned target: `40ca2c7f90d11d27be5457d177c133f0947d1c08`
- Range: ordered first-parent commits, zero-based indices 1800 through 1999
- Columns: index | full SHA | exact upstream subject | disposition | core-system areas | rationale

`Ported (CS-####)` links an accepted core-system change to the durable audit, while
plain `Ported` identifies accepted non-core work. `PortCandidate` retains target
behavior that still needs integration. `AlreadyPresent`
means CMU already has equivalent behavior. `Deferred` preserves behavior pending
focused reconciliation. `Superseded` means another target or local architectural
change replaces the commit. `Irrelevant` identifies commits with no standalone
behavior to port.

~~~text
1800 | d021604f4296bf75284f1ffd421a9f74d4fa44e6 | Make gameticker spawn code more modular (#41588) | Deferred | GameTicking, Gamerules | Spawn extraction changes the RMC-divergent GameTicker API; reconcile it with downstream spawn callers.
1801 | 972e23b6943831b31408c4c7b7edcd3321e4be17 | Packed Station - General Fixes (#41592) | Deferred | — | The generated Packed map rewrite needs target-final CMU map reconciliation.
1802 | f16501c6e27385e8685a9109f7910f9fb16e0cd6 | Meatball Salvage wreck remake (#41589) | Deferred | — | The salvage-map replacement must be reconciled with CMU salvage content.
1803 | 9b4b3bd9efd2e1b77087d7bd955a7afe975f82f0 | Automatic changelog update | Irrelevant | — | Generated map changelog only.
1804 | 0edfe2728d35466f6a3016130beb4925fff289bd | A Very Plasma Christmas (#41573) | Deferred | — | The seasonal Plasma map rewrite needs map-policy reconciliation.
1805 | 52815bb83d40c804279b69e86b3c451459ed7d57 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1806 | 0b29702866673f0e85c27054b49677337c60cf51 | Added more lateral thrusters to Elkridge's cargo shuttle (#41570) | Deferred | Movement, Physics | The shuttle-map thruster layout needs CMU map reconciliation.
1807 | 2ecd63d8500a4b529732ea2906d483c56dc94718 | Update mothership again (#41491) | Deferred | Interactions, Gamerules | The large Xenoborg mothership map, prototype, and asset bundle crosses retained borg policy.
1808 | 415208922c00716672f79fe3397f13c0d591b9cb | fland: change empty dressers to random filled (#41318) | Deferred | — | The Fland map change needs target-final map reconciliation.
1809 | 68f4bbed12a65190d24b20eb6559aec4d4bd685f | Automatic changelog update | Irrelevant | — | Generated map changelog only.
1810 | 2330136abfd74e002b7933115e9238d7965496a4 | Fix helmet lights (#41599) | Deferred | Interactions | Battery-on-entity support depends on the index-1813 charge-helper and predicted-power cluster.
1811 | a1e6c35c82b16da3824456339a27cae347678853 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1812 | 62fbac7a13400a592f6b899a208c176630cb14d7 | Change thief backpack ui name and description with Component fields (#41583) | PortCandidate | Interactions | The retained UI configurability is small but must adapt to CMU's divergent thief UI.
1813 | 8db29b4e87fdee8ee595117b7fc89be1a7743adf | Helper method for get charge level (#41601) | Deferred | Interactions | The nine-file power-cell API migration underpins indices 1810, 1829, and 1966.
1814 | 73c8ad018370a8d8c1e89776d36da26e3a5d17d6 | Adds option to whitelist or blacklist store entries based on buyer objectives (#41493) | PortCandidate | Gamerules | The standalone condition applies cleanly and should land with objective-gated listings that consume it.
1815 | ed7c004de20e2554617b0d41315f739ccca7f0ae | Fix looking at verbs causing sounds or popups (#41609) | Deferred | Interactions | The important silent UI-open-attempt contract spans 14 divergent client, shared, and server handlers.
1816 | 673cc6ca3c7f1d542de0b9ff0422965fba5f8f2e | Automatic changelog update | Irrelevant | — | Generated changelog only.
1817 | 037a5598b7f258362f47f2a458e9fd9e0a263ebc | Properly document AtmosDeviceEnabled(Disabled)Event (#41613) | Irrelevant | Physics | XML-documentation-only delta with no runtime behavior.
1818 | 95f72ee98da77078e63bb75d86e1829f1c03853b | Felinase/Caninase Reagent Tweaks (#41527) | Deferred | Medical, Chemistry | Reagent effects and reaction balance require CMU species and chemistry policy review.
1819 | 1b6b5dddb88858983411cf981210b3c8940c7925 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1820 | dcc44b29dec2446b65a21de6c0a2f53363bb6142 | Change to add shot glasses to the bartender guidebook entry (#41618) | PortCandidate | — | One-line guidebook completeness fix.
1821 | d0b8bb12f8d07b7969dbca111114e91f3dad2738 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1822 | 334057de8ceaacaf4011bed0e0f3dc686b886491 | Add StatusIcon component to MobBaseAncestor (#41624) | PortCandidate | Medical | The one-line NPC status-icon inheritance fix needs an RMC mob-parent coverage check.
1823 | 6722a63853f8c010693801edf3cbd774f26c3557 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1824 | 4a8198a88bbf97c1acc960ca7bc25ac01f264db0 | Update Credits (#41628) | Irrelevant | — | Upstream credits metadata only.
1825 | b58a7f69a2302f42751931725b41edbde30a29b9 | Make xenoborg round end text better (#41623) | Deferred | GameTicking, Gamerules | Xenoborg rule state and text changes need retained-rule reconciliation.
1826 | 42c7daeed7293c1063296af02ab7076e957c5e7d | Remove Sloth from codeowners (#41516) | Irrelevant | — | Upstream repository ownership metadata.
1827 | 83ed95952ab8046639436b69e26d68ee3601174c | Fix EquipmentVerbs not showing up in strip menu (#41631) | AlreadyPresent | Interactions | Exact behavior is already ported and audited as CS-0019.
1828 | 4ec41cc8f01c6c40b38d7c37fd10e8071d64fd2f | Automatic changelog update | Irrelevant | — | Generated changelog only.
1829 | 937b61a8328b94522349e29a0e539aac16ba64ed | Predict borgs (#41600) | Deferred | Movement, Interactions | The 29-file client, shared, and server borg prediction refactor collides with RMC borg behavior.
1830 | cf1509b4aedb1d07fa9edec738d2deced53f5484 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1831 | 58cdf0af4965934828bf4f261c7efeb6d55d4652 | predict name identifiers (#41605) | Deferred | Interactions | The server-to-shared prediction refactor needs local identity-system reconciliation.
1832 | 3d32dab66116e5bab547f4b571a4c11da4503fb5 | Make Firespread logical (#41636) | Ported (CS-0224) | Physics | Collision fire spread now conserves mass-weighted fire stacks through the RMC-aware SetFireStacks path while retaining cancellation, caps, and fire-intensity behavior.
1833 | 04c5406e2054f4d2bd51779b16d5ac267b13f852 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1834 | 61c58a6341821f8d8b988da1899a5e5c0726a1ae | fix rcd overlay getting stuck for borg modules (#41648) | Ported (CS-0223) | Interactions | Client RCD placement now ignores transient client-side predicted held entities, preventing stuck overlays while authoritative and RMC borg RCD paths remain unchanged.
1835 | 5334e425004d2fdef2975d49044bde0253bcaf99 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1836 | 68ca82cfd74585b47f69e52b4f6ac62dff498125 | Add water flower for clowns (#41469) | Deferred | Chemistry, Interactions | The generalized equip-spray action feature spans 17 files and introduces the absent Paintable contract.
1837 | 99d27218fa27e9c40367b8691727ee1c19fce995 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1838 | 6213c51fd58fae31ebaf09edc90bf25d82c1cf22 | multi reagent bloodstream (#41489) | Deferred | Medical, Chemistry | The fundamental 25-file blood-solution, DNA, and zombie model migration needs dedicated integration.
1839 | 9bb6beb795e5d7346f0d393383d49cf762b1395c | Automatic changelog update | Irrelevant | — | Generated changelog only.
1840 | 21d039318e35f849ec55b1b27626ed2e0bb90a5c | Santa anomaly back! (#41654) | PortCandidate | Gamerules | Two-line seasonal anomaly spawn-weight restoration.
1841 | 3a43eec31db91124d8fcb9f61ab11f3ec6c4fb96 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1842 | 3eae394d244718e0a278a9f2e599dc4f3b9b2b42 | Stable into Master (#41656) | Superseded | Medical, Interactions | Effective first-parent delta is one file, +1/-1, fixing an inverted melee Damageable boolean; CMU's later nullable damage-result API replaces this path.
1843 | cf619a9ab2ca8b903e7e4416a3802bf67029ec72 | Add Changeling DNA store (#41632) | Deferred | Gamerules, Interactions | The store, currency, action, and rule feature needs CMU changeling reconciliation.
1844 | 9e85c2fdcbb952cfcdac868ddace608356a63747 | Fix Damageable API (#41657) | Superseded | Medical | The one-line boolean overload correction is obsolete under CMU's nullable DamageChangedEvent result API.
1845 | 9e32f1e92b19380564c42cb33ec32c93cd868438 | Add debug hitscan weapon (#41658) | PortCandidate | Shooting | Debug-only prototypes and assets can be reconciled independently.
1846 | bafbd1e3e07a2364695afe0220a52f4aa839a706 | New Map - Snowball (#40300) | Deferred | GameTicking, Physics | The huge map replacement plus pools and integration-test changes begins the Snowball cluster.
1847 | f93e2e3b82680a8eea64e6dd1dad0b3862ab5597 | Automatic changelog update | Irrelevant | — | Generated changelogs only.
1848 | b77a0d63683c45ca8c986dec31cfc2317e559d0f | Christmas-ifed Packed Station! (#41665) | Deferred | — | The seasonal Packed map rewrite needs map-policy reconciliation.
1849 | 20756abcfbdce8f605204dda58f80ead4e641b1e | Fix xenoborg evac calling announcment (#41437) | Deferred | GameTicking, Gamerules | Shuttle-command, round-end, and Xenoborg-rule behavior must land with the borg rule cluster.
1850 | b6821b4b45f3f54ce0a4b27697d2a077ea919cb7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1851 | a76a3c966e4e6a3cdd5f5aa0b318014f706e623c | Fix Mothership cannot use their tools and BorgSystem cleanup. (#41673) | Deferred | Interactions | Shared Borg API cleanup depends on the broad index-1829 prediction migration and RMC tool policy.
1852 | c210a7d165cc837b7810fddfe016ba75e10880a1 | Minor cleanup of crowbars.yml (#41672) | PortCandidate | — | Small prototype cleanup applies cleanly.
1853 | 6eac92a339c4498ed5ae0385c8883506103aec58 | cleanup EntityStorageSystem (#40163) | Deferred | Interactions, Physics | The five-file shared, client, and server storage rewrite crosses RMC containers and physics behavior.
1854 | e435681b578fa4d374a87e93475af59b0031f86a | Snowball: fix airlocks and windoors in the HoP office (#41675) | Deferred | Interactions | The incremental Snowball map fix depends on index 1846 map reconciliation.
1855 | 522d90e2cb2859351380a0cbfd15bc0522c9b061 | Automatic changelog update | Irrelevant | — | Generated map changelog only.
1856 | ae55204f9f245c774bcc0b8581dffbe5c2ad32ac | Move some admin components to shared (#41677) | Deferred | Interactions, Gamerules | The 19-file admin component relocation changes client and server ownership and precedes index 1997.
1857 | 8784a3e5c79a24668003504d5d3c90c441e820c9 | Change default Rat King order from 'Loose' to 'Follow' (#41680) | PortCandidate | Movement, Gamerules | The one-line default AI-order change needs confirmation against retained Rat King policy.
1858 | 967d1e2ab2e8d8fd6fa2034a0dd95f1a6a5bb8d2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1859 | dbdd7e42939cca8fbf56664e4f407fc7c1ae75d1 | Prefixes surgical caps with their color (#41681) | PortCandidate | Medical | Three prototype display-name corrections.
1860 | c80391ade29abebfb7785f6da5762227b9d1fef6 | Snowball station minor fixes (#41683) | Deferred | — | The incremental Snowball map rewrite depends on index 1846.
1861 | 7cb210261c7726b939b5746ee90db8559cee80c9 | Automatic changelog update | Irrelevant | — | Generated map changelog only.
1862 | d0a784b9e63c75c1c3e7c9207cc4a59657ca94ef | Add status effect support to Traits, change PainNumbness to be a status effect (#41646) | Deferred | Medical, Gamerules | The 15-file trait, cloning, and StatusEffectNew contract begins the indices 1877 and 1881 chain.
1863 | 42b33ddd935b5b255eaac049331f3c42f9af5176 | Reduce explosion airtight cache memory usage (#40912) | Deferred | Physics | The valuable seven-file explosion flood and cache rewrite needs dedicated correctness and performance validation.
1864 | fd2d427869b9c2566d8f82d1f32685da7e4a66ab | Minor cleanup of hypospray.yml + clearer medipen descriptions (#41682) | PortCandidate | Medical, Chemistry | Prototype cleanup and clearer medical descriptions can be reconciled independently.
1865 | 89d1100337ecd8ee8cb3400a17c82209b64f7817 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1866 | 737bd3e22ce1fc00620bfb653bfed5dfb69b1aed | Predict BedSystem (#41686) | Deferred | Medical, Interactions | The client and server to shared bed prediction rewrite must preserve RMC buckle and medical behavior.
1867 | bb2169dca42ce5339148093673fabaf396dff6a5 | Change stamina slowdown to use a percentage-based threshold (#41691) | Deferred | Movement, Medical | Converting absolute stamina slowdown thresholds to percentages is a movement-semantic migration that needs focused integration with RMC stamina limits and modifiers.
1868 | e857fd95c4302e1ec4573f0f61e281a3b0c7e278 | Change to fix wording of Pun Pun's jacket (#41695) | PortCandidate | — | One-line prototype wording fix.
1869 | b0236b7e241f47e6b0acad91683b90e12d5319c5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1870 | 706d09f398c19c7703bf69e53a99bf08910fd19c | Snowball: Head of Security's hardsuit is no longer absent (#41698) | Deferred | — | The Snowball map fix depends on index 1846.
1871 | e5f24965f0ada7039633889ff7e1c71d98a8cf1c | Automatic changelog update | Irrelevant | — | Generated map changelog only.
1872 | 0e0aec29636330bb5ffeeef4fddf73b7c7a50e42 | Cleanup of prototypes in Resources/Prototypes/Catalog/Fills/Crates/ + fixed light crate descriptions (#41697) | Deferred | Medical, Chemistry, Interactions | The broad 20-file crate rewrite needs target-final per-prototype reconciliation rather than bulk replacement.
1873 | 8b35bff5195fc5013874904bc22c5bf0ed07ddd5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1874 | 1e6ce9a0b6e5d5068ff630b35cd19195032d59ff | figurines.ftl is now sorted by department (#41701) | Irrelevant | — | Locale-only sorting churn with no behavior.
1875 | ec8ddbfd3589854a7dc13e08f9cd44740d8295f5 | Allows spesos to fit in envelopes (#41700) | PortCandidate | Interactions | Two-line envelope whitelist extension.
1876 | 4c4a162168619bc270149f07bd870f5ed7b154e3 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1877 | d6987343f7dd7be5d2eaecb5582f92d9afa146fa | Change Ephedrine, Desoxyephedrine and Hyperzine properties (#41693) | Deferred | Movement, Medical, Chemistry | The stimulant balance and status-effect rewrite depends on index 1862 and conflicts with retained RMC chemistry balance.
1878 | 5c40b501f58114536213bcde0f8d6618ac0cb9f9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1879 | 0b11625278fc35b764920d6fa55312cc752348be | Exo - Botany update and minor fixes (#41598) | Deferred | — | The large generated Exo map rewrite needs map reconciliation.
1880 | 6febe0fa588883244d4fa193d2b6626c2323904c | Automatic changelog update | Irrelevant | — | Generated changelog only.
1881 | 405bc6c8f13085b432ab59a9315cac34e9487504 | Warfarin and Hemorrhinol, Hemophilia turned into a StatusEffect (#41685) | Deferred | Medical, Chemistry | The 13-file reagent, trait, and status-effect chain depends on index 1862 and requires RMC medical-balance review.
1882 | 4ab720932364c99a246f18488874a561a9974bef | Automatic changelog update | Irrelevant | — | Generated changelog only.
1883 | 5f757bd838d58b87b6139210e6d72b15887ef07a | Add missing vox unequipped sprite for explorer mask (#41405) | PortCandidate | — | Isolated sprite and metadata completeness fix.
1884 | 88561d4e83a02f630c96196dae78e5e449be2845 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1885 | f1820045782ff7b1a358bd03026f90fdde5940e6 | Fix recharging spray painters (#41725) | Deferred | Interactions | The fix targets generalized Paintable limited charges absent locally and should land after the index-1836 substrate.
1886 | f5308cde93db6c3b78bb5d416d5a2465ef3defab | Automatic changelog update | Irrelevant | — | Generated changelog only.
1887 | 080ab214c6f190d418851df521c999bc77dcbc0d | New figurine voicelines (#41723) | PortCandidate | — | Dataset and localization content can be reconciled independently.
1888 | 8d6097be9ad3f7dea44cc7606ed6d157cc20fe98 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1889 | da3e331366d9df0053cb6df5423b1b04d854d9eb | C# 14 fixes (#41708) | Ported | — | Parallax loading now uses an explicit task array, avoiding the C# 14 sandbox violation without changing load behavior.
1890 | 3944268fe48a76293ecc4820616cc87fff8caacc | Fix shuttle FTL with UI scale (#40933) | Ported (CS-0222) | Movement, Physics | Shuttle free-position FTL now uses RelativePixelPosition and parallax uses PixelSize, aligning UI-scale coordinate spaces while retaining RMC and server FTL behavior.
1891 | ad8597c19868390c27fe61b4af6f927113362bf2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1892 | e360f6e03a214394f958635a9267fa4cc12faf74 | Remove static IoC from Replay and Shared EntryPoint (#41707) | Deferred | GameTicking | The entry-point and replay IoC change is tied to engine bootstrap APIs and needs RT-version reconciliation.
1893 | fca8d95f03d069518f14fc73720238e2e6dcf93d | Cleanup warnings: CS0114, CS0414, CS0618 (#41578) | Deferred | Physics | Mixed admin UI, markup-interface, and Throwing cleanup crosses the fork's current RT interfaces.
1894 | 3b69c10fc8a7a79274abe0b9151d9edb5d250212 | Replace Vestine-derivatives in plant mutations, change uplink prices & hypopen to reflect changes (#41716) | Deferred | Medical, Chemistry | The four-file reagent, uplink, and botany balance change belongs with index 1896.
1895 | ce0126b725ea89a10028646b63004d6424105b17 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1896 | 8054071c325d730d3c0f112c93f174a896897df7 | Vestine now Mutates Plants to Produce Vestine (#41731) | Deferred | Chemistry | The new entity effect and botany/reagent behavior span seven files and need RMC botany reconciliation.
1897 | 92263d24302d511cb86ea41d7d5e00418a3ce498 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1898 | 9268cec0c98ad7c8cb1c4c40c95999bdb696b56d | Predict ReactionMixerSystem (#41218) | Deferred | Chemistry, Interactions | The core server-to-shared prediction refactor needs focused chemistry reconciliation and prediction tests.
1899 | 432c9578be79b611b9f71ce0337aeee351fc7cbd | move GameMapPrototype and GameMapCondition to shared (#41742) | Deferred | GameTicking, Gamerules | The 19-file shared map API move touches ticker, voting, tools, tests, and retained map-manager code.
1900 | d54a431584c607bc27f7db72ab0c9fc99d4e90ff | Remove explosive component from mothership cpre (#41743) | PortCandidate | Gamerules | The small Xenoborg core prototype safety fix needs reconciliation with the retained mothership prototype.
1901 | 6de6217242d2c101d38e656574228f31177040c7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1902 | 9bd27fd24529b8e8924a86ca3031666647773039 | Adds the sticky grappling hand (#37551) | PortCandidate | Shooting, Interactions, Physics | The prototype, audio, and asset feature uses existing launcher contracts but needs tether behavior and content-policy validation.
1903 | 9e71a4212bcb7439d8706ad73b2db8c7dc03a6c6 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1904 | 49656df4102d4c74eeb17308c4da565e495c873b | Update Credits (#41750) | Irrelevant | — | Upstream credits metadata only.
1905 | dd79254a0fcd4ff6e996c2effc0ef1f964355728 | Add voice mask implant (#41551) | Deferred | Interactions, Gamerules | The 14-file identity, implant, voice, and store feature needs RMC identity and implant reconciliation.
1906 | c9ebb1b6b769242a403d546ecaf88848da396ba2 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1907 | 58f884133bcd3978411556bc55b7ece3bda71e3f | Remove Zookeeper and Boxer jobs (#41741) | Deferred | Gamerules | Upstream job-removal and migration policy must not overwrite the RMC/CMU role roster.
1908 | 113507d4b424ee4aacf36b63b448021f07ae9105 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1909 | 35ea5f18200a0cc02859760645b20af262652312 | Fixed hyperzine (micro)injector descriptions (#41755) | Deferred | Medical, Chemistry | The new 60/30-second descriptions are only correct with the deferred index-1877 stimulant rebalance.
1910 | e0299c9afaba8395df6d344b545bfb329bb4cd3b | Automatic changelog update | Irrelevant | — | Generated changelog only.
1911 | c164897ead168e7ef08bc1521d8c8b3eb74d7862 | Snap Booms (fake snap pops) (#38654) | PortCandidate | Shooting, Interactions, Physics | The three-prototype fun explosive feature needs construction and explosion-inheritance validation.
1912 | 58b23d31621fa3829b33c1c34d8dfc9af0345516 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1913 | 7b3c83c56566408c2684ca7aebd13f85a12dbeca | Fix box shuttle from overloading instantly (#41753) (Stable merge) (#41760) | Deferred | Physics | Effective first-parent delta is a non-empty emergency-box map rewrite of one file, +564/-77; reconcile it with the APC and map cluster.
1914 | 34f60c115eea800d9e59c2fc0934ea5c84f2adb1 | Add GenPop Enter/Leave to ID Card Computer. Add shuffle the accesses a bit. (#41739) | Deferred | Interactions, Gamerules | The access UI and four security-role policies need RMC access and job reconciliation.
1915 | 94327566ef87ead81864661cf66828a46b39af37 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1916 | 51b7af73f76e82fac4f5cfedc1ae2846a5cab0ff | Double bullet speeds (#34971) | Deferred | Shooting, Physics | Target changes default speed and fixture geometry while CMU explicitly retains RMC projectile speed 53 and custom ballistics.
1917 | f99b59b7ed4410a6b44e1df977ffe2de18c43605 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1918 | 5b7d3757e68de1ac65fa183378d6d594f7e1eba5 | Green Glowsticks are their own entity (#41712) | Deferred | Interactions | The ten-file prototype, table, and map migration needs target-final content reconciliation.
1919 | ee2e1eb93272ac76ad0e0191b67709c8750d2ba1 | Make xenoborg light brighter (#41580) | PortCandidate | Interactions | Two-field point-light tuning on the Xenoborg chassis.
1920 | 7cc256890b8099b5f3427dc0fb9c9a69fc78fc14 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1921 | 5c70d28927836a7bb671547bac2ea2230c0477a4 | TriggerOnUiOpen/Close (#41718) | PortCandidate | Interactions | The small shared trigger components and system hooks need BoundUI event-contract and consumer checks.
1922 | 5fdba3f0bc8648a41f52b54cbe1de96eb44ff2cb | Hitscans now have names (#41763) | PortCandidate | Shooting | Adds names to hidden hitscan prototypes for diagnostics and admin visibility.
1923 | 0eead492de323aea52e7d6c20edb7ae7bd77f3bd | Reverts Mop + Glowstick Storage Rotation (#41765) | PortCandidate | Interactions | Two prototype-field removals restore intended storage rotation.
1924 | 9d13d348b40f9831baef05e2e4079e69d461ea50 | Remove map list from station-specific jobs' descriptions (#41748) | PortCandidate | Gamerules | Two localization changes remove stale map-list wording.
1925 | d79065bdbb1184d88e34f39d2c34850d5703f4dc | Automatic changelog update | Irrelevant | — | Generated changelog only.
1926 | e8462ebe55a673589135cfe0f4b89f89b3cc1c93 | fix bagel APC powernet (#41769) | Deferred | Physics | The generated Bagel powernet map fix belongs to the APC and map cluster.
1927 | 4f2d2208f5fb60f55be5454c10295503e488460e | Misc Proper Rotation Sprites (#41764) | PortCandidate | Medical, Interactions | The 44-file asset, metadata, and prototype correction needs binary and inherited-state comparison.
1928 | 7bc1e94b479021b10342dc367426d6b606bf915d | Automatic changelog update | Irrelevant | — | Generated map changelog only.
1929 | 9806153b9388bfd749aa23b7f656b284113963f1 | fix box APC powernet (#41770) | Deferred | Physics | The generated Box powernet map fix belongs to the APC and map cluster.
1930 | 8a29a8d813fce8a43ebd512a1aff99f05ac1385c | fix CC APC powernet (#41771) | Deferred | Physics | The generated CentComm powernet map fix belongs to the APC and map cluster.
1931 | c8b71f8989eac9c28ae43245e0ef26f20d8d0c40 | fix elkridge APC powernet (#41772) | Deferred | Physics | The generated Elkridge powernet map fix belongs to the APC and map cluster.
1932 | 84ce9522b75a8b6dc702c04817d99e2288a49973 | fix fland APC powernet (#41773) | Deferred | Physics | The generated Fland powernet map fix belongs to the APC and map cluster.
1933 | 94ff45796727ee32f4e519e9ad3c70fcee63ff7b | Automatic changelog update | Irrelevant | — | Generated changelogs only.
1934 | 8dc131dec69815f727cea31600413366eadc088c | fix oasis APC powernet (#41776) | Deferred | Physics | The generated Oasis powernet map fix belongs to the APC and map cluster.
1935 | 4bfa0a6eafb05c21e37591937b016cb8fcc69f52 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1936 | 36e009f21ecb57fe6269a51c2a6cc4b7f07074e5 | Fix Bible Fast Healing (#41777) | AlreadyPresent | Medical | CMU already uses the later nullable damage result and correctly distinguishes null or empty healing, so the upstream boolean inversion cannot recur.
1937 | 5fa2028ce978ff0e0154281fa5dd3045457b7f4a | Automatic changelog update | Irrelevant | — | Generated changelog only.
1938 | fde6129e5f79d996342cd10e2cc0ab4aed8a2d56 | Allow removing species from the RNG pool of a new player's initial auto-generated character (#41678) | Deferred | GameTicking, Gamerules | CVar, preferences, and species-weight behavior needs RMC character-generation reconciliation.
1939 | 25bb98c1a2eb6568f76479fde63d518f1c8493c0 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1940 | f1443f0cd7a26747582e2f204309b02b17138827 | TriggerOn(Un)Embed (#41720) | Deferred | Shooting, Interactions | The projectile embed event refactor and new trigger types must preserve RMC projectile and embed APIs.
1941 | 32c1c44367baa69bfa0c792e0729cfc3c04749cd | fix CL AGAIN (#41782) | Irrelevant | — | Changelog formatting correction only.
1942 | 406231cdb3343a742bb0948937a11b7942d4f4ec | Merge stable into master (#41781) | Superseded | Physics, Interactions | Effective first-parent delta is non-empty at eight files, +25/-135, but exactly removes the APC overload feature; index 1943 restores the first-parent tree byte-for-byte.
1943 | 0031beca4b63e7f3107c97299fc74fdac6c6c3e4 | Reapply "Trip APCs when they exceed a power limit (#41377)" (#41789) | Deferred | Physics, Interactions | The eight-file APC overload, UI, pause, and test feature needs power-network and map-cluster reconciliation.
1944 | 9c2dc0451940fe4518ceff693eceafb2278fd245 | Clothing equipping doAfter tweak (#41732) | PortCandidate | Movement, Interactions | Adds data-driven equip-while-moving behavior that needs reconciliation with RMC inventory DoAfters and component state.
1945 | c0e90815d9e8c6b6bc86d22f624561c26adf8ad9 | Sometimes the Bagel Theater doesn't like showing up to work (#41787) | Deferred | GameTicking | The procedural-map tag and generated Bagel rewrite needs complete map reconciliation.
1946 | a99452fbb800eaf84efefa417fd83fb6fe2191ff | Automatic changelog update | Irrelevant | — | Generated map changelog only.
1947 | e8f018bd8f8609041c3252b881b5657419081b81 | Fix batteries not counting towards the battery bounty (#41792) | Deferred | Interactions | CMU has no PredictedBattery component substrate, so adding that prototype whitelist name would fail validation; defer it to the power migration.
1948 | af2fd9950cd0cdd4feda7cf83f85e9a0baea81bf | Bagel Theater will randomly spawn in partially broken (#41794) | Deferred | GameTicking | The procedural Bagel follow-up depends on index 1945.
1949 | e038b07a40a9d8d9918536090ebb82bea520c160 | Hushpup Shotgun (#41512) | PortCandidate | Shooting | The self-contained weapon, audio, asset, and uplink content needs RMC firearm-inheritance and balance reconciliation.
1950 | 4197dd352f27ffcbdbef0bbcafa071636028ad6f | Automatic changelog update | Irrelevant | — | Generated changelog only.
1951 | 3b6d0be65f24702fbdc2dc963405489488cdf446 | fix a typo in the changelog (#41798) | Irrelevant | — | Changelog-only correction.
1952 | 66b9df28df5838fc3bf7933d80cf38dec9012e4a | Snowball update (#41806) | Deferred | — | The incremental Snowball map rewrite depends on index 1846.
1953 | 7ada931549831aeb9cc1189c9f19415c48f8526f | Automatic changelog update | Irrelevant | — | Generated map changelog only.
1954 | 3a4484b702816be32ffee785310e9215094428a4 | Add paper labels to gas canisters (#41737) | PortCandidate | Interactions, Physics | The prototype and asset presentation feature should be coordinated with index-1961 paper-label bases.
1955 | 92482dfb52e3c08e5ab3ca287cce3191f7b71edd | Automatic changelog update | Irrelevant | — | Generated changelog only.
1956 | 3835489af0520e7cbd923c6682e5dd9c9c5b318d | Delete license.txt (#41805) | Irrelevant | — | Removes an upstream audio metadata file with no runtime behavior; legal files should not be deleted blindly.
1957 | 67e18a0693c501f43b15fc8705720d7b6e14f31d | Make door bolting powergaming no longer relevant anymore (#41138) | Deferred | Interactions, Physics | The emag, bolt, and force-prying policy changes need reconciliation with RMC doors and tools.
1958 | 95deb3d966f8184aa0c65b32d5ecf0ee9bccfad8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1959 | dc616f67e7436d65ba28b0e118de21b37c1f9885 | Fix mobs not being blindable (#41788) | Ported (CS-0225) | Medical | BaseSimpleMob now gains Blindable, activating existing standard and RMCSimpleMob blindness statuses while explicit RMC species and xeno data remains authoritative.
1960 | 9edf121a15e69c9112418f5cfdd3db35ccc6515a | Automatic changelog update | Irrelevant | — | Generated changelog only.
1961 | 6dbd2f6acc53a50630e9daea743353ff1612a923 | Add BasePaperLabelable and BasePaperLabelableVisualized abstract prototypes (#41807) | Deferred | Interactions | The eight-file prototype-inheritance refactor should land with index 1954 and target-final label consumers.
1962 | c5d7444af68437e74a68c604c6a458c5471f85d5 | Migrate Bot Speech Catalogs to One Folder (#41478) | PortCandidate | — | The three-file catalog organization and migration can be reconciled mechanically.
1963 | c6430203cfed08011078db23b3ca0386bee9e553 | Update the erase script to support the latest migration (#41524) | Deferred | — | This important user-erasure fix needs adaptation because upstream's ConstructionFavorites marker is already stale versus CMU's later RMC 2026 migrations.
1964 | 452fdf0165e3dd7ae2dcce39a9ce4f7f7406d2a5 | Chefs start with chef shoes (#41814) | PortCandidate | — | One-line starting-gear correction.
1965 | 5927babfbe1bfbab0910e979460512fac0d7965d | Automatic changelog update | Irrelevant | — | Generated changelog only.
1966 | 86f47958497d45c1163ff1746560be77efbba98d | Fix rigged power cells exploding early (#41813) | Deferred | Interactions, Physics | The charge-event Delta and rate contract plus predicted batteries depend on the indices 1810 and 1813 power migration; local rigging remains legacy Battery-only.
1967 | 00f86e9bd5e59e6e0a30de35bde0fc124b421b3e | Automatic changelog update | Irrelevant | — | Generated changelog only.
1968 | 2834ac2d500eb2c2963c0f5b18bf3fd1a87f79aa | Change that specifies escape via the escape shuttle rather than pods in escape objectives (#41809) | PortCandidate | Gamerules | Three objective-name clarifications need a check against CMU evacuation semantics.
1969 | 67b4ba0425ae26f577ac59c3cb4654bb5e1cbf3b | Automatic changelog update | Irrelevant | — | Generated changelog only.
1970 | 19bad6266f3fb4ccd34e9d944299da0d6530c629 | Predict RootableSystem (#41729) | Deferred | Movement, Interactions | The four-file client and server to shared prediction rewrite intersects RMC Diona and rooting behavior.
1971 | 4a460468d09be73bdfe234df356e04403cd523e9 | Improve ClothingSpeedModifier, Fix Paramedic Void Suit (#41820) | Deferred | Movement | The requireActivated flag fixes the local paramedic suit, but CMU has manual component state and RMC movement-relay hooks requiring adaptation.
1972 | b7251fbdeba7587d7d0c04eaf9a5cbbb4cecc1bb | Automatic changelog update | Irrelevant | — | Generated changelog only.
1973 | fffbc654dd9c8aa5997152b1e58b19eead243b11 | Soap, Banana peel, and Slip entity tables (#41783) | PortCandidate | Interactions, Physics | The ten-file entity-table content refactor needs RMC slip-probability and fill-inheritance validation.
1974 | 6fc487531cabba44c361873e8d3faa04619f603d | Add small cooldown to NukeKeypadEnterMessages (#41831) | Ported (CS-0220) | Interactions, Gamerules | Nuke Enter messages now enforce the target one-second server-side cooldown, blocking client message spam from brute-forcing codes while preserving the retained keypad and arming flow.
1975 | d947dd8c6c2baf3af8c3d8d3f88d21414532abb5 | Smart Fridges can contain anything edible (#41830) | PortCandidate | Chemistry, Interactions | One whitelist-component addition; verify retained RMC fridge policy.
1976 | 59d0df0e7c66e4b6d5e2decece2e138a68ec4009 | diona are now less debilitated by rooting in blood (#41642) | Deferred | Movement, Medical, Chemistry | The reagent and species balance change belongs with rooting and status-effect reconciliation.
1977 | c3c24a664ef80b2b319ce4d745e1d4e6271edf14 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1978 | 429e925608404f4bb7c8e2b71c90d57c1712edc0 | Re-sprite the Ripley (#41832) | PortCandidate | — | The binary 36-file mech resprite needs direct asset and metadata comparison.
1979 | 6e85abf26de083fe6b9b355949f58a24d9fdb5fb | Remove remote detonation/disable from the robotics console. (#41834) | PortCandidate | Interactions, Gamerules | The two-field console policy change applies cleanly but needs retained borg-control policy confirmation.
1980 | 2d803293ee147b5527bbfcdd0c49fce5abb468e7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1981 | cbbf978408117385555d96863cf16a67f36732dd | ScramOnTrigger teleportation logic rewrite (#41808) | Deferred | Movement, Interactions, Physics | The rewrite replaces cross-grid lookup with TurfSystem blocked-tile sampling and materially changes teleport semantics.
1982 | 51404cc1e45cc75211ce98c7a35b05790d611f6b | Automatic changelog update | Irrelevant | — | Generated changelog only.
1983 | 75bb75539bbe84964be0f3303dc8473d135c4a4a | Changed PullingSystem to use MobStateChangedEvent instead of UpdateMo… (#41835) | Ported (CS-0221) | Movement, Medical | PullingSystem now reacts to committed MobStateChangedEvent.NewMobState before stopping pulls, preserving RMC critical-grace and pull-event propagation.
1984 | 5944f1a817bd7acc60ee7c538449cf0a8a7c5d45 | Add KI pills to the radsuit locker (#41576) | PortCandidate | Medical | Small locker-fill content change that needs a CMU medical-loadout policy check.
1985 | b3106a40006e51aa024255cd2ad416a3078bf8f8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1986 | 6f5e6445b6c65d15300b5e5098cda64abc295931 | Preserve arbitrage by fixing a bug (#41756) | Ported (CS-0226) | Interactions | BaseStructureDispenser no longer carries an unintended static price, preventing deconstruction value duplication while retaining explicit child pricing.
1987 | bc3fd3c323afdf9fc6cb129181e1b772164d113f | Automatic changelog update | Irrelevant | — | Generated changelog only.
1988 | a761100cf5cfc79107d1707eed13c82a2e84999f | Hand labeler can always remove labels (#40330) | PortCandidate | Interactions | The useful verb, interaction, and logging refactor needs reconciliation with local handler signatures and RMC label consumers.
1989 | 6e04013cf19b57fee18e0097e8b1a8821d570333 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1990 | 594ec9acffbfff09a73468030a38af91c2ec852b | All Figurines Entity Table (#41775) | Deferred | Interactions, Gamerules | The 12-file trigger, entity-table, and prototype migration needs target-final figurine and artifact reconciliation.
1991 | 41e2a5dad3c90c69ce503517f65c10ad60f9de9e | Update Credits (#41848) | Irrelevant | — | Upstream credits metadata only.
1992 | 7d1c75c1bb8861285fdd938e391311205541fccc | Fixed Vulp Hair layering Under Hoods and Hardsuits (#41827) | PortCandidate | — | One-line species-layering fix; confirm CMU Vulp prototype inheritance.
1993 | 15968acdf6ce71aaca0b8e26aa0cab0ef98d4a7a | Don't remove borg access without power (#41844) | Deferred | Movement, Interactions | Moving access gating from power-active to mind-present lifecycle depends on the indices 1829 and 1851 Borg reconciliation.
1994 | 5457ce5ec970a219ff8ea710552a7d3f36e76903 | Automatic changelog update | Irrelevant | — | Generated changelog only.
1995 | 6b3ddc681369e6dc1d773eaa625a111cddee6dde | Five bounty arbitrage fixes (#41846) | PortCandidate | Gamerules | Five reward-value corrections should land only after CMU economy-policy comparison.
1996 | a92d280a3982aea1adea6c6fe4be5cbcef8f238e | Automatic changelog update | Irrelevant | — | Generated changelog only.
1997 | 40ae49bb8502ef7dddc63efa74f9daf7425eef16 | Killsign cleanup (#41845) | Deferred | Interactions, Gamerules | The 16-file admin-system, component, and asset cleanup depends on the index-1856 shared-component move.
1998 | 2b7361cec7fa3843287127f098409e08d3f3e031 | Automatic changelog update | Irrelevant | — | Generated admin changelog only.
1999 | a40d130f0fbbe63f21c920838869ed84c88ce711 | Edible Produce are now also Butcherable (#36786) | PortCandidate | Medical, Interactions | Alternate butchering outputs on eight produce prototypes need RMC food-inheritance and weapon-producing-plant validation.
~~~

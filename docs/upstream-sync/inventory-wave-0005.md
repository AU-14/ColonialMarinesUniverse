# SS14 upstream inventory: wave 0005

Audit date: 2026-07-20

- Pinned baseline: `59633f6dc50e77dda8cefa344d87c7b01e06a810`
- Pinned target: `40ca2c7f90d11d27be5457d177c133f0947d1c08`
- Range: ordered first-parent commits, zero-based indices 0800 through 0999
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
0800 | 3896c5be8e3fe6055cb1553788bbe04a61900a18 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0801 | fb35b52da5384ea98c2952aa0fe83d6e8fd9c13e | Ignore non-content commands in AllCommandsHavePermissions (#39336) | Irrelevant | — | Upstream command-permission test maintenance has no standalone runtime behavior to port.
0802 | a495ab908dceb4e90afd0a7fbce1bb2d4a8766ec | Allow to run `mappingclientsidesetup` and `showsubfloor` with +MAPPING permissions (#34455) | PortCandidate | Interactions, Gamerules | Retained mapping-command permission behavior should be adapted to CMU's current command registration and access flags.
0803 | ff54410d6a32dfd041a48734ec5d4e474b22cee9 | "idk" no longer shrugs, instead sanitizing to "I don't know" (#39024) | PortCandidate | Interactions | The retained chat sanitization change is small but must be reconciled with CMU speech substitutions and localization.
0804 | 7b9aee3977105bcb15be0288a5f429716dc273be | Automatic changelog update | Irrelevant | — | Generated changelog only.
0805 | 3e2152a59e06284f49e03b2ecf10a8a97663f4c5 | Improve Do Not Map test to whitelist specific prototypes per map and whitelist entire directories (#36117) | Irrelevant | — | Upstream mapping-test policy and whitelist maintenance do not provide standalone gameplay behavior.
0806 | 35bb5c633c75dd01720817ffd77b3ce2466485f2 | Make Butterflies zombie immune (#40265) | PortCandidate | Medical, Gamerules | Retained infection-immunity behavior needs adaptation to CMU's zombie and species prototype hierarchy.
0807 | 73499b2a0c6b17d86a36662679f974800de8dd3a | Automatic changelog update | Irrelevant | — | Generated changelog only.
0808 | 2601853791d8fc318d1e57e9420acb2eb7d9eac9 | Cardboard Box Weightless Fix (#40260) | Deferred (CS142 reverted by CS165) | Movement, Physics | `GravityAffected` belongs to deferred index 0506's event-based weightlessness rewrite; CMU's current gravity system already evaluates dynamic boxes without that marker.
0809 | 1736b9bb34999c7742a987c278ee3c8cd8e98e20 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0810 | 2882fd289f502b9f0b394ee067766191bdc3bf0c | Add admin shuttles (#32139) | Deferred | Movement, Physics, Gamerules | The large shuttle, map, and administration content bundle needs focused reconciliation with CMU shuttle gameplay.
0811 | ddc428b946c50a929768cf40f42e781aab81966a | Automatic changelog update | Irrelevant | — | Generated changelog only.
0812 | 8f8b307460fdc22b47c3e504864396bd5c624725 | Exo - Lighting update & more (#40199) | Deferred | Physics | This broad map and lighting update must be reconciled against CMU's divergent station maps and power layout.
0813 | 6e88b66735fefade04714903268e4d7b0691279f | Automatic changelog update | Irrelevant | — | Generated changelog only.
0814 | 46f59300acb99f3c2373bbc83ab446e0aa77621c | Laser rifle is contraband again (#40253) | Ported (CS148, `34eaa63a28`) | Shooting, Interactions | Adapted by separating lethal and practice rifles under a shared fork-compatible base, preserving CMU ammo providers and sizing while confining contraband to the lethal rifle.
0815 | f271790f755ead371859efdb34d67deb27ae3e77 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0816 | a7eb5dd22b2e10c902f1601458fe7bd1fa107cf8 | Fix darts inhand sprites (#40207) | PortCandidate | — | The retained sprite correction needs a targeted resource comparison with CMU's dart assets.
0817 | 164f8a2fad42c459a8d1dff6ee35b536f4e8a10d | Fix APC breaker toggle button prediction by setting ToggleMode True (#40273) | Ported (CS-0143) | Interactions, GameTicking | Accepted as downstream commit 370a92ae40; the compatible CMU APC button now predicts and reconciles toggle state.
0818 | 8171589f56492fbc1537332750a86af52930ede8 | fix chasmsystem resolve error (#40281) | Deferred | Movement, Physics | The chasm resolve fix depends on chasm-system code that differs or is absent in CMU and needs focused integration.
0819 | 276e4df7499dbc651f8774ce03b42850020c8c05 | No take; Only throw. (#40143) | Deferred | Interactions, Physics | The throw-only behavior intersects CMU pickup, throw, and inventory rules and should be reconciled as a focused interaction change.
0820 | 5acc1633cb2f7824174e6493b8b70de2afe2d790 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0821 | 09f17802c20658612c61601a25705754fa809af9 | Clip the WindowTitle of FancyWindows, so close buttons don't get hidden (#40272) | PortCandidate | Interactions | The retained UI clipping fix should be adapted to CMU's current FancyWindow layout.
0822 | 8a4a6ec7abace0193e0e55332dc6f247ef60c01a | Food Item Size Adjustment (#39203) | Deferred | Medical, Chemistry, Interactions | The broad food-size rebalance needs prototype-by-prototype reconciliation with RMC storage and edible content.
0823 | b9920cbdcb3acae5aecbddeb57f9b80e60273221 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0824 | 9519eb5f7a8644a35b80ed3811b3dd2e26e626f7 | Fix Linter errors (#40283) | Deferred | — | The mixed linter cleanup should be split and compared against current files before any applicable hunks are retained.
0825 | 1f1c71919b24a1fb6e24a8a379709b3cd7e4baa6 | Remove unused BulletTennis (#40285) | AlreadyPresent | Shooting, Physics | CMU has no active BulletTennis implementation, so the upstream dead-code removal is already equivalent.
0826 | ff3f0c69979dbbb795ab277d3d08f847c36e6dc4 | Reorganize and refactor drinks yml (#39221) | Deferred | Chemistry, Interactions, Physics | The drinks migration also changes physical bases, destructibility, landing/hit damage, sealing, and broken-bottle resources; its coupled RMC consumers require a dedicated data migration.
0827 | 77eca4a570f079f2d6b00b7dd1dc1bebf2c511c9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0828 | fbf65b7f748c0f96f6d8c19d3aea8c8d7d57441a | Make vending machine restocks predicted (and its sound not spammable) (#38609) | PortCandidate | Interactions, GameTicking | Retained prediction and sound-throttling behavior should be adapted to CMU's vending and restock systems.
0829 | 7396d9e54cb520b047e5b07dfc21d271bd8185f0 | Added SmartFridge circuitboards (#39879) | PortCandidate | Interactions | The circuit-board additions remain useful but require checking CMU machine-board and construction prototype IDs.
0830 | bb970970c9511b5dae7522d46ecaedd152123196 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0831 | 1245487c9a972ffa14ab3f9fc9701c5b8f663cd8 | storage and inventory toolshed commands (#39046) | PortCandidate | Interactions | The retained diagnostics need adaptation to CMU's current storage, inventory, and toolshed APIs.
0832 | d8c55aef3c067a661096e3a5aa2ea8429a3b4a99 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0833 | 321331e66413e913eb36432716e77936c5b086ae | move all the radio components and system to Shared (#40293) | Deferred | Interactions, GameTicking | The broad radio server-to-shared migration intersects RMC radio behavior and prediction ownership.
0834 | 8cc1b29ba4c014c6f0961fc2e9f536549a49b6fe | Clake frag round fix (#40294) | Deferred | Shooting | The ammunition fix depends on CMU's divergent RMC weapon and projectile prototype hierarchy.
0835 | 49fb6fdd6c5fc5c8d8dd4f8f525362f8dc227915 | Fix for can't stop pulling when cuffed (#40233) | Ported (CS-0144) | Movement, Interactions, Physics | Accepted as downstream commit 6543057731; cuff completion now releases invalid pulls while preserving explicit pull release.
0836 | 2820882754f8fa528c84fdc236c5207f72222240 | Stop microwaving! (#40132) | Irrelevant | — | The affected upstream microwave content does not map to a retained standalone CMU change.
0837 | 928e6c807903e511a1d20592226406a7e0dec69c | Edible Sound Specifier Override (#40312) | Deferred | Medical, Chemistry, Interactions | This sound override is coupled to the newer edible architecture and must land with its dependency chain.
0838 | 82e7cb020cfd65cad9ebf226b7a3b52897ba7ad4 | Delete DrinkComponent, migrate prototypes to EdibleComponent (#40308) | Deferred | Medical, Chemistry, Interactions | The large DrinkComponent migration conflicts with CMU and RMC edible prototypes and requires focused reconciliation.
0839 | 71bcda1feccc5cff360506441e9c7ff02aae7aa6 | Toilet fixes: Exception when constructing, proper seat layering (#40313) | PortCandidate | Interactions, Physics | The retained construction and layering fixes should be adapted to CMU's toilet entity hierarchy.
0840 | 79a34556e5a8a938cc3d62287705a4b5578f7abf | Automatic changelog update | Irrelevant | — | Generated changelog only.
0841 | ab40b1ab734f664d18f96066e2c6e65a515866c9 | Chameleon Projector Physics Fix (#37960) | Ported (CS-0145) | Movement, Physics | Accepted as downstream commit 206ce432ed; unanchored input movers now regain their movable kinematic-controller body.
0842 | 0ba1a7c4dd2c7fffbfce1ae276bc69cfe87c1afc | Automatic changelog update | Irrelevant | — | Generated changelog only.
0843 | bcc30813e96087a3b5b434655fce3c7be0ee47de | Cockroach Gib when Stepped on (#40103) | Deferred | Medical, Interactions, Physics | The collision-driven gib behavior needs reconciliation with CMU movement contacts, damage, and trigger systems.
0844 | 659648b03d5872d3353afcacabded20541df1e89 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0845 | a4b7cd73c5b53b67a99b60a6bcb7050e35171d57 | +1 Spam mail (#40310) | PortCandidate | Interactions | The additional spam-mail content can be retained after checking CMU paper and mail prototype IDs.
0846 | d17182c16256b08cfaea22201ae73e2be4059755 | Change listplayers command permissions to require the PII flag (#40324) | PortCandidate | Gamerules | The security correction should be adapted to CMU's administration permission flags and command registration.
0847 | 6768ff1e9125f758196cf873ad5b91362cc2c62f | Automatic changelog update | Irrelevant | — | Generated changelog only.
0848 | f1ae8ecdfefaef0086bc1d8bae1d917cdcc1f95a | Add Undergarments to Vulpkanin (#40321) | Deferred | Interactions | CMU's RMC-derived species and loadout data require a dedicated reconciliation of the upstream garment additions.
0849 | 52a4e956510ba5771d28e6e36e4ecc30578cba3b | Automatic changelog update | Irrelevant | — | Generated changelog only.
0850 | 3aece8d46ccfd3e34b4263221010ef9644125dd0 | Fix throwing objects causing pushback on the player who threw them in a not weightless environment (#40335) | AlreadyPresent | Movement, Physics | Current CMU throw recoil already gates pushback to weightless movement, matching the retained behavior.
0851 | e7dc6ae990c8c9d7e9f52b4d643af82b6b520c3f | Update Credits (#40342) | Irrelevant | — | Upstream credits metadata only.
0852 | c317fa984002ea886cdf32f559951f0512835b20 | Massively reduce how lethal Man-O-War shuttle is (#40339) | Deferred | Movement, Physics, Gamerules | This map-specific shuttle rebalance needs reconciliation with CMU maps, atmospherics, and shuttle gameplay.
0853 | 29da03b4e478213a3e5553fc5bd80e42ccb33109 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0854 | fdd4789d32ec4d9e35a02b33d64bf28b3e322a91 | Give vulps correct undergarments (#40341) | Superseded | Interactions | This corrective follow-up is replaced by later target species data and CMU's divergent RMC Vulpkanin setup.
0855 | 1908317e3cffd83c8e80e51de16c9176f1d5f0e8 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0856 | 7616b9aa1cd4be8c64b3bbe1c7f176aa27bc1fd9 | Fix Heterochromia for Vulpkanin (#40320) | Deferred | Interactions | The appearance fix depends on Vulpkanin data that diverges in the RMC-derived species implementation.
0857 | 373288571336e4756c740657d3945d887e0c97b5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0858 | 8c67c5b5a2f889efe7f65819761e55d65f733b02 | Add myself to credits (#40345) | Irrelevant | — | Upstream credits metadata only.
0859 | 9c3af67cd1535c3d9060bc74ec14b5c712ed783b | Fix wizard's recharge spell not adding charges to wands that use LimitedChargesComponent (#40347) | Ported (CS-0146) | Shooting, Interactions | Accepted as downstream commit f6f02d187c; recharge now supports limited-charge wizard wands while preserving legacy ammo providers.
0860 | cc6aa626da1cef7074399b763eb08c36147d48ae | Automatic changelog update | Irrelevant | — | Generated changelog only.
0861 | 11d434818e571e677307a4ee26b068c25f94b14f | Merge staging into master (#40356) | Deferred | Movement, Medical, Interactions, Physics, Gamerules | Effective constituents mix skin-color/Vulp migration, crawling and heat-shader reverts, and butcher/container fixes; some are equivalent or superseded while retained portions are architecture- or dependency-blocked.
0862 | 2ffe0db61f25701b5f1bc839b4705b85d377c5b0 | Linked radiation shields on bagel (#40358) | Deferred | Physics | The map-specific shielding update requires CMU Bagel and radiation-network reconciliation.
0863 | b4f4d6e2955bfa9b5be1b5cbd999c0392f2ece57 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0864 | 97d4153d84c46417c301ee8406d3c91ac7b007b4 | Add jetpacks to the Nukie Infiltrator (#39887) | Deferred | Movement, Shooting, Gamerules | The equipment addition needs reconciliation with CMU nuclear-operative loadouts and RMC movement gear.
0865 | f1d52e0c13a7d40265073efb0817615e38968175 | Plasma Armory Restock (#39763) | Deferred | Shooting, Gamerules | The armory restock changes conflict with CMU's RMC-derived weapon economy and vendor prototypes.
0866 | e05d9e944bd604e3ed021565de92d41b0a66c57e | Automatic changelog update | Irrelevant | — | Generated changelog only.
0867 | 9313c0792486e1800b14f3d0340f1f926fc7d12a | Replaced incendiary AK ammo with normal AK ammo, bagel. (#40359) | Irrelevant | — | This Bagel-specific upstream map loadout does not define a standalone retained CMU behavior.
0868 | 31d30f24f99c4a280f5a5d7f335a5504df2b94dc | Automatic changelog update | Irrelevant | — | Generated changelog only.
0869 | fd20cc2a00a9976203616d37749a620e01ee0bbf | Dark/Light Grass & Desert Astrotiles (#37867) | PortCandidate | — | The retained tile variants require a focused resource and prototype comparison with CMU terrain content.
0870 | ea89711029aacf6c41a10345b90e02bed4e3b938 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0871 | ff94d3e7ad1ba0fcf105f9a040f154a583a7331f | Added spanky to mapping codeowners (#40362) | Irrelevant | — | Upstream repository ownership metadata only.
0872 | 02061592ddb4993812d0c53ead0ee540731026c5 | Devices with access restrictions list those restrictions in their examination description (#37712) | Deferred | Interactions, Gamerules | The examination behavior depends on upstream access-system and localization changes that need focused CMU reconciliation.
0873 | e27576929f4e729ab233bef51d5d87d6e6039a06 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0874 | e0fd44da662d74bfd3fdbbe6663d2f801252cd61 | merge stable into master (#40369) | Ported (CS-0179) | Movement, Interactions, Physics | The merge's pull hotfix now raises `AttemptStopPullingEvent` by reference so cuff handlers can propagate cancellation; the effective first-parent delta was audited instead of the default merge display.
0875 | 7444c8ea4abeaf33c3fd151537d925ce69d4878a | The station AI can be destroyed (#39588) | Deferred | Medical, Gamerules | The broad station-AI health and destruction feature depends on AI architecture that diverges in CMU.
0876 | fb710208890366ce294bdf794969cde32ea47c7f | Automatic changelog update | Irrelevant | — | Generated changelog only.
0877 | 584f0aaa7bd4bd92763c1f71f78c003e9fbda58d | Clerify salamander description (#40379) | Irrelevant | — | This isolated upstream description typo has no core-system impact and was not retained as a port.
0878 | 09a197eb9162b94a7ee1f3cc78772a6b7783c47a | Detunes Ninja Stun To Actually Have Some Counterplay (#39707) | Ported (CS-0147) | Interactions | Accepted as downstream commit d988533a6d; ninja-glove generated stuns now enforce a ten-second cooldown.
0879 | 731d6ff53c5dd4c0bd5e4da23f391e02b633c7e0 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0880 | d8ab007c50abd516eb41c1617aa7803d5aa58e98 | Add missing admin changelog entry (#40395) | Irrelevant | — | Upstream changelog metadata only.
0881 | d80f53bb48e7408a7c3e2f26f4a23b9d12b749bb | Automatic changelog update | Irrelevant | — | Generated changelog only.
0882 | bc0691822a35267023cad46885c7db2ddcc6db1d | Bug fix for Station AI damaged accent (#40399) | Deferred | Medical, Gamerules | The accent correction depends on the unintegrated station-AI damage and health architecture.
0883 | 377dd6b36cc3f19f020cb7d8040d92162db3b208 | Add intellicards to AI crates (#40401) | Deferred | Interactions, Gamerules | The crate addition depends on CMU's divergent station-AI and intellicard feature set.
0884 | 138ea680763208a25bc943af63860a299e7d3ac7 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0885 | 972adcee21d9b25c2ca63b90c2649bd564fb6f9a | ``NarcolepsySystem`` refactor (#40305) | PortCandidate | Medical, GameTicking | The retained server-to-shared narcolepsy refactor needs adaptation to CMU status effects, traits, and prediction.
0886 | dfc7d183add0185b837a1cf0f1d24afb016fe2d2 | Intellicards rename to AI stored on them (#40402) | PortCandidate | Interactions, Gamerules | The rename-on-insert behavior is useful but must be adapted to CMU's current AI and container systems.
0887 | bf18b5e26b9080ebfee3da46596dbe5f17f58593 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0888 | 7ff98dd94fc0388fe6267bda0d68b7724d6e6268 | Readyall and Toggleready commands to LEC. Fix an issue with ready button desync. (#38706) | PortCandidate | Interactions, Gamerules | Retained localized commands and ready-state reconciliation need adaptation to CMU's lobby UI and permissions.
0889 | fc89f231a55d6455d467439a8bd6cd9237ee3531 | Mothership Core Prototype Cleanup (#40410) | Deferred | Gamerules | The prototype cleanup targets station-AI content whose hierarchy is not yet reconciled in CMU.
0890 | a4368264f0bc14fc96f13099a0910da0a752a3ad | Add chasm integration tests (#40286) | Superseded | Movement, Physics | Later target chasm test and system changes replace this intermediate test-only state.
0891 | ce052484280fd36a0251e824a664763c02125df8 | Fland: on evac fix delta pressure destroying the air storage cell (#40413) | Irrelevant | Physics | This Fland-specific map adjustment does not define a standalone retained CMU port.
0892 | f21c6f2030decc360f6f332d5364df66288f29c6 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0893 | 09eee5074dd0c20ea134c08df2d7357e495e1613 | Use an alias in job icons yml (#40415) | Superseded | Gamerules | Later target prototype organization replaces this intermediate alias-only cleanup.
0894 | 599b9622345255a73ad296c7f0558a0a54aa3169 | Localize vulp emotes (#40418) | Deferred | Interactions | The localization migration depends on RMC-divergent Vulpkanin emotes and species data.
0895 | 0dd1733998322140f5bcceebaf30f0ab2d511a78 | Change ``GetPryTimeModifierEvent.BaseTime`` to the TimeSpan (#40419) | PortCandidate | Interactions | The retained duration-type correction should be adapted across CMU's pry event producers and consumers.
0896 | ca47e59e434000bc452beea83ea8498efb6f24a4 | Update ``DoorComponent`` to use TimeSpans and fix comments (#40420) | PortCandidate | Interactions, Physics | The retained TimeSpan conversion needs coordinated updates across CMU's divergent door systems and prototypes.
0897 | 1dd977effde65714476abfcdfe36d40849d32601 | Remove drone lawset from ion storms (#40374) | Ported (CS150, `8a90e433f2`) | Gamerules | Removed the role-specific Drone lawset from the ion-storm random pool while preserving the lawset itself.
0898 | 684a4a382dc1d9ad96eead4fe946c758af8981e6 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0899 | 933da32da5c41d9e1aa1593ca58173eb60caffa9 | Remove Misgendering (#40425) | Deferred | Interactions | The broad grammar and localization migration needs focused reconciliation with CMU speech and character text.
0900 | e1ba33814b94b7349db4a54347c8673308f92f38 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0901 | b692b6e33e94183abf906acb79e3b5d9aa85bdeb | Antag Rolebans (#35966) | Deferred | Gamerules | The large antagonist-role-ban feature crosses CMU role assignment, administration, persistence, and UI systems.
0902 | ffb5bd7325568b6c4401d55e438debeee7eac437 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0903 | 857ae2a088d55a4be98f2ff6602e50d2982142aa | Turn the Satanic Bible's pentagram around, fix left inhand (#40234) | Superseded | — | Later target resource changes replace this intermediate sprite correction.
0904 | 9c98f5f9f40efa5e6fa8a9d801a7d679230abb81 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0905 | 1a92ada5bd5a36e22cbc8b84db7f6aeb611ee7f5 | Adds Nukie IDs and PDAs, makes Nukie IDs able to copy accesses. (#37304) | PortCandidate | Interactions, Gamerules | The retained nuclear-operative identity behavior needs adaptation to CMU IDs, PDAs, loadouts, and access copying.
0906 | 0a61f2a583d3d3bb9c7c59659d356a69bb3116bf | Automatic changelog update | Irrelevant | — | Generated changelog only.
0907 | e59bc06c25a5f9ba32810e097b3e9bd443f8d730 | Updated the cyborg weapon module's uplink description to be accurate (#40429) | PortCandidate | Shooting, Interactions | The corrected description should be retained after checking CMU cyborg module and uplink localization IDs.
0908 | 27b86bcca803a4d0807de32cc91b0a7490d63c43 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0909 | 4a815c006f7dd559556189603a52b365f50174c8 | Renames the "Integrated GPS" to "integrated GPS" (#40431) | PortCandidate | Interactions | The retained display-name capitalization correction is a small localization port.
0910 | b41ce9cce666110f4f76d3f4d17695e20efbd1ff | Stun rune Fix (#40432) | Ported (CS151, `2c7e0fff1d`) | Interactions, Physics | Bound StunOnCollide to the rune fixture already used by the trigger, restoring collision stuns.
0911 | 3844f1e7a584b113a09670059e15e71d18bd3e2b | Automatic changelog update | Irrelevant | — | Generated changelog only.
0912 | 2349898dcc124c85db07f1368e275cf95a8ab51f | Plasma: add tropico to atmos (#40436) | Irrelevant | Physics | This Tropico-specific map metadata does not define a standalone retained CMU change.
0913 | eb1bd0a565d254b7aa3d463074bf6b2a84b83016 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0914 | 940eaa46740d1d2a9d864967f1641e199a339202 | Bring vulpkanin in-line with other species on hugging (#40183) | Deferred | Interactions | The species interaction change requires reconciliation with RMC-derived Vulpkanin and hug behavior.
0915 | c4a42e556f96a88633c4af18d561a1cbdeb7a3cc | Automatic changelog update | Irrelevant | — | Generated changelog only.
0916 | 8cf5c3f6bc9f62eb7dc76de180ad029e0c79a2f6 | Add chemical analysis goggles to ChemDrobe (#40236) | Ported (CS152, `2e014193fc`) | Medical, Chemistry | Added the retained goggles entry using the pinned target's final quantity of one without pulling forward the broad medical inventory rebalance.
0917 | f13f7830d699e4ffd5c85d18d009aa0f6b6c8c55 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0918 | d9d968a4793a3d00694f13d2720127efad3915b9 | Crashed the snakeskin boots stock-market by removing their hidden no-slip properties (#40201) | Ported (CS153, `5ceb8b7635`) | Movement, Physics | Removed the boots' unadvertised NoSlip component and updated their description.
0919 | e09ea850f50b025413a89fa7fed5f2228dd7fc71 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0920 | 0e0f01542210e8103001ca4746c5de3bd64e07c3 | Rename medifab implanter to implant extractor and made it's description clearer (#40375) | Ported (CS154, `7abdca9ea6`) | Medical, Interactions | Renamed and documented the extractor together with its revolutionary guide reference, matching CMU's existing failure behavior.
0921 | 128d06518efbcdae2dd5e0e48a5c01010ab21a0c | Silence mime bags (#40317) | Ported (CS155, `48e7b622b1`) | Interactions | Added null Storage sound overrides to all three mime bags and corrected the duffel's misnested sound fields.
0922 | 5cb0917d5fc3543219a1a101509747ba98770887 | Ninja items are now highly illegal (#39855) | PortCandidate | Interactions, Gamerules | The retained contraband-level change should be checked against CMU ninja items and security rules.
0923 | 6d576fc8ceb03c380c3830257932a5a6fb925081 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0924 | 8cf9da90d3a538aa96c5c6cf436c189e188f0cf5 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0925 | fbb9c9c524e0e8cc01a90941d61e61e2275ed863 | Make ichor heal brute, burn, and toxin evenly (#39466) | Ported (CS156, `1fe085a5cf`) | Medical, Chemistry | Ported the retained EvenHealthChange distribution and reduced bloodloss healing using CMU's existing effect implementation.
0926 | 76b680b03b9f198b53c1ce51f89ebad16bc4d11a | Automatic changelog update | Irrelevant | — | Generated changelog only.
0927 | 9d0a7b77296b623d975c9d79319a42a5ed3d1a91 | Add contraband levels for several reagents (#40426) | Deferred | Chemistry, Gamerules | The broad reagent contraband classification needs reconciliation with CMU chemistry, guidebook, and security data.
0928 | b2c8565a2d3e27cda851b3710dbee8a07f2af178 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0929 | d95b5da7d2440df9cb1a834e0fbe7814982ba886 | Added Cutting Slicing and Executing options to the cane blade (#40311) | PortCandidate | Shooting, Interactions | The retained cane-blade verbs and damage behavior should be adapted to CMU's weapon inheritance.
0930 | c19cdad7871c3ebe64cb73e757be6cd9f7fd937f | Automatic changelog update | Irrelevant | — | Generated changelog only.
0931 | 5a67e3c26a23f0d6432c5a88e4b8df7e5dbf1f51 | Made all tarantulas able to drag entities (#40433) | Ported (CS157, `a17139ed5a`) | Movement, Interactions, Physics | Added a handless Puller to MobSpiderBase so all intended tarantula descendants can drag entities.
0932 | 393e6cbc07c86d5196fb4b20c5188e657a7aa277 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0933 | e1da7ec9c59101c579774c5dc056199e920d8b18 | Better thief objectives (#39867) | PortCandidate | Gamerules | The retained objective improvements need reconciliation with CMU thief roles, objectives, and item pools.
0934 | 867d0f5130378eff30749166d3280114cc4b408f | Automatic changelog update | Irrelevant | — | Generated changelog only.
0935 | ed89c0e06196a3b3ab0b466ec6dd7ebee2742c9d | adds ConveyorMask colission mask to it's fixture component (#40439) | Ported (CS158, `8000305358`) | Movement, Physics, Interactions | Canonicalized the fixture layer as ConveyorMask; the prior expanded flags resolved to the same bits, so current door behavior is unchanged.
0936 | 5c54d199a81359873ada5e7577c05a29ad92475d | Update engine to v267.1.0 (#40445) | Irrelevant | — | RobustToolbox is independently rebased and engine updates are outside this content inventory.
0937 | 4f311d6c44c8a0fb8778f8bafd017958fc46344d | Fixes some refuling welder typos (#40447) | PortCandidate | Chemistry, Interactions | The retained welder text corrections should be applied against CMU's current localization keys.
0938 | c075c89cd0632709b6c73249b7f7ff269c7d2555 | oasis warp fix (#40454) | Deferred | Movement, Physics | The Oasis map warp correction needs final map and transform reconciliation with CMU.
0939 | 2b411b244e681431accb1d74846c46044d90b54d | The Experimental Lecter 8 (#40372) | Deferred | Shooting, Gamerules | The weapon, ammunition, assets, and balance bundle must be reconciled with RMC's divergent firearms stack.
0940 | c2fb4a126fed743a22f589b87cc72d60adc9efaa | Automatic changelog update | Irrelevant | — | Generated changelog only.
0941 | 512f28458c304070918c8658682ad1b92323ce25 | fix chasm heisentest (#40456) | Irrelevant | Movement, Physics | This upstream-only test adjustment has no standalone runtime behavior to port.
0942 | 0c7b1e9163888e87664b061085912510f32b321b | Update Oasis Teg (#40463) | Deferred | Physics | The Oasis map power change requires reconciliation with CMU's map and atmospherics state.
0943 | 11e965cd99e01d60121f2ebb1f5cc520b8835abf | Automatic changelog update | Irrelevant | — | Generated changelog only.
0944 | 7c650da7d7659eec0be135ccd3eaef9787e9fb34 | fix disposal pipes deleting contents when welded (#40451) | Ported (CS159, `8ec334a0fc`) | Interactions, Physics | Replaced direct deletion with the destructible lifecycle on all fourteen disposal-pipe graph exits so welded pipes preserve their contents.
0945 | 5b255d13c6f5130e962dc9e944cab9a879f97a8c | Renames the radar console computer board to "mass scanner computer board" (#40430) | PortCandidate | Interactions | The clearer board name should be retained through CMU's current prototype and localization IDs.
0946 | 4796c92609faadd38f27f4ad5611441bcd8de4e2 | Inhand Sprites for Clear Glass (#40427) | PortCandidate | Chemistry, Interactions | The retained in-hand states require adaptation to CMU's reorganized drink prototypes and sprite assets.
0947 | 63c468d963e59205894b4f7edbb60916bda10d54 | fixed localization text for vulp shock ear (inner) color (#40412) | Superseded | Interactions | CMU's RMC-derived Vulpkanin customization data replaces this upstream locale-only state.
0948 | 365d12a4e9b49a24cf04f8e10ed8f5208cf4c149 | moves magic number from SharedMoverController to InputMoverComponent (#40411) | PortCandidate | Movement | The retained movement constant should move with CMU's component configuration after checking RMC mover extensions.
0949 | e6e47b599deb7d4a04fc177a706d22e8acb10271 | Added AI console to amber (#40393) | Superseded | Gamerules | The target later removes or replaces the affected Amber map state, so this intermediate map edit is obsolete.
0950 | 886b365099fcb092aa0b616a245b9484c52a14bc | Automatic changelog update | Irrelevant | — | Generated changelog only.
0951 | a5129c141c91a5358c8d2990d152b142fe8cdf37 | Don't overwrite values that are mid-edit in air alarm window (#40338) | PortCandidate | Interactions | The retained focus guard should be adapted to CMU's current air-alarm controls and update loop.
0952 | 1c74e1e100df066430797630e937441dfbec8a9b | Automatic changelog update | Irrelevant | — | Generated changelog only.
0953 | a746c3cc0fce3c08e46a6f955c02f170c7c884a3 | Show hand labeler label text on examine (#40334) | PortCandidate | Interactions | The retained examine detail should be adapted to CMU's hand-labeler component and localization.
0954 | 85f3cc7583ba4e9b9d20ead11b7baaf6b8535551 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0955 | ae22c7c3d085d1c7359cbcd07d18755d1b38e733 | Fix RCD errors (#40278) | AlreadyPresent | Interactions | CMU's current RCD path already uses the safe index lookup retained by this upstream fix.
0956 | b85fed759a73460ce8e4bf17b5d691bc13fa66bd | Fixing a syntax error (#40473) | Superseded | Gamerules | The affected objective entries are absent from CMU and later target objective data replaces this syntax-only state.
0957 | 9893aca467425b433baa648505388604c69bb41a | Update Credits (#40478) | Irrelevant | — | Upstream credits metadata only.
0958 | cc4cab5677316487319a5b89e6d111a116175a20 | Fix explosion grid alignment for static grids (#40193) | PortCandidate | Shooting, Physics | The retained static-grid explosion alignment fix needs targeted adaptation because target-final mass assignment differs by path.
0959 | 8c16b4580b7a48aa3f9bc581a4d1044ba427044d | Fix render target caching in overlays (#40181) | Deferred | Physics | The client-renderer cache change depends on graphics APIs and overlay architecture that require focused reconciliation.
0960 | f9243dfdd7ec4e26877343ce6d95b2104cc11078 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0961 | b6797afe52fbeac57e8d694061026887ade99107 | Move TestPair & PoolManager to engine (#36797) | Deferred | GameTicking | The test-infrastructure move is coupled to RobustToolbox and cannot be applied as a content-only port.
0962 | 7678251ad58b7182afc66658b9c9b4d8122b1bb3 | Average min+max in MaterialArbitrageTest  (#39578) | Irrelevant | — | Upstream-only test arithmetic has no standalone gameplay behavior.
0963 | 818a715822b494046cf0ca2122f8fee5df8c4d23 | prevent repeat TriggerOnCollide triggers (#40428) | Deferred | Shooting, Interactions, Physics | The collision guard depends on the newer shared trigger framework and must land with that architecture.
0964 | d5face573d450b9c41b8f61f1fc91a2e7eb9dd98 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0965 | 2245235db1fa4eada023ce3302ad93b52cefc4c2 | Add date formatting to admin-notes-unbanned (#40484) | PortCandidate | Gamerules | The retained date formatting should be adapted to CMU's admin-note localization and persistence model.
0966 | c7b239bcbb2152f69de9d5f5384bf98e7bb98abc | Automatic changelog update | Irrelevant | — | Generated changelog only.
0967 | eabb00a1e2906e32221781ce91c28608db4d6609 | Changed corpsman description (#40486) | Ported (CS160, `2dd455ac9f`) | Medical, Gamerules | Ported the clearer nuclear-operative medic objective as the companion to CS131's corrected corpsman title; RMC corpsman jobs are separate.
0968 | 0ac83937c9959cca8bc0ed69e11f63a65151b235 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0969 | 92f246058c5ca25f411da3fa8ea32d44266b6371 | bugfix - correcting poster damage resistances (#40489) | Deferred | Interactions, Physics | Depends on the missing `Card` damage modifier set from deferred index 0333; applying the poster hunk alone would create an unresolved prototype reference.
0970 | 08c1b2c9be452cdc0218dee21fd69734b55bde3c | Automatic changelog update | Irrelevant | — | Generated changelog only.
0971 | 29e1f6cddf7d3936dd613bd710da9d36f39603a3 | Fool players with status command (#40460) | Superseded | GameTicking, Gamerules | CMU lacks the affected Decoy rule and later target gamerule state replaces this intermediate behavior.
0972 | 83fe0279642c770adc3b60e04e723045c4dbe5ef | Removed suspicion antags from antags.ftl (#40493) | PortCandidate | Gamerules | The stale localization removal should be checked against CMU's retained antagonist roster and locale keys.
0973 | 2824334a1ee1fd25487d2c7331b8b42d80b582ee | Health increase for station AI cores (#40487) | Deferred | Medical, Gamerules | CMU's AI core health and appearance diverge substantially from target-final and require a deliberate balance decision.
0974 | b58bf396bc5c89b26a3c1d4cc2cc01e352baa594 | add silicon smite (#40452) | Deferred | Interactions, Gamerules | The smite feature depends on station-AI, silicon, action, and administration systems not yet reconciled.
0975 | c70d2cfb9ff54bf89336c963bef8ec9076cff032 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0976 | c7f5545a4630178c440e2fb792dadce219d74dc5 | Vulpkanin Admin Smite (#40360) | Superseded | Interactions, Gamerules | The affected upstream Vulpkanin path diverges in RMC, while the reusable appearance-clone correction is already present.
0977 | c0b1eae1625f2e61c13eeae44551c86d9e33fefb | Automatic changelog update | Irrelevant | — | Generated changelog only.
0978 | a26bafacb1b2d81b40a19274edda40aca14cb696 | Shuttle UI now properly goes into pilot mode only when using the UI (#40491) | Ported (CS164, `e268122681`) | Movement, Interactions, Physics | Moved TryPilot from the cancellable attempt event to the post-open event and documented the event-phase contract.
0979 | f5cad5f12f0eafa70fc5e45e0451bd0d0c7f3ca9 | Automatic changelog update | Irrelevant | — | Generated changelog only.
0980 | 95d91283a3a2cb831e4dd6c3ea21d3ea55e700a0 | Fixes all departamental typos (both just in comments) (#40502) | PortCandidate | — | The applicable comment-only spelling corrections can be retained without changing runtime behavior.
0981 | d79fb62d8d8715db8d23da068c36afd7c16d4bfa | Fixes suprise typo in the guidebook (#40501) | PortCandidate | — | The retained guidebook typo correction should be applied to CMU's matching documentation text.
0982 | c1a21693fad74eec8f2cb4945be0b962ede228a1 | Cleanup warnings: Use TransformSystem for anchoring (#39778) | PortCandidate | Shooting, Physics, Gamerules | Only applicable target-final anchoring hunks, notably the rod path, should be adapted; absent systems must be skipped.
0983 | fd40888b0e9e9c6529afd82330c9026f6f476c75 | Cleanup warnings: CS0067, CS8509, CS8073 (#39770) | PortCandidate | Interactions | Applicable warning cleanups should be selected narrowly because CMU still uses several dependencies removed upstream.
0984 | a3ddba6f4248a3c568cf76a7260ab58b3709cf8a | Cleanup - Use `RemoveAllChildren()` over `DisposeAllChildren()` (#39848) | PortCandidate | Medical, Interactions | The retained UI cleanup should be adapted only where CMU still has the matching child-disposal pattern.
0985 | 8e9aa1dbb6121c4fdf04db51ebe79611f1f871c1 | Merge stable into master (#40512) | Ported (CS-0180) | Interactions, Physics | The merge's chameleon-projector hotfix blacklists doors, hidden subfloor entities, catwalks, walls, and windows; its effective first-parent delta was hidden by default merge display.
0986 | 3f575a64f3ff0fbcaa308ad55670c76ec7b2a5d8 | Fire helmets alone no longer prevent you from heating up while on fire (#40481) | Ported (CS161, `05a10d0f17`) | Medical, Physics | Ported the regular and atmos fire-helmet heating/cooling coefficients without changing RMC firefighter equipment.
0987 | 04d71da982dddfb5a791f37ba7cc2ceeca74b47d | Automatic changelog update | Irrelevant | — | Generated changelog only.
0988 | 3ee7d81944aebedc183862961b123ce0508c445b | Target Dummies Now Show Damage Numbers from Projectiles to User (#40101) | AlreadyPresent | Shooting, Medical, Interactions | RMC's `RMCDamagePopupSystem` already consumes `ProjectileDamageDealtEvent` and sends shooter-specific feedback; applying the upstream origin-based popup would duplicate projectile damage numbers.
0989 | eee5751a22b028ef1210d648944fd9e4bc45fd81 | TriggerOnPlayerSpawnComplete and ExplosionOnTrigger (#39820) | Deferred | GameTicking, Gamerules, Interactions, Physics | These trigger additions depend on the newer trigger framework and spawn lifecycle architecture.
0990 | 2f7b73e830c70219e4ac70f8494e115a4a70aede | Weather On Trigger (#40505) | Deferred | GameTicking, Gamerules, Interactions | Weather triggering depends on the unintegrated shared trigger and gamerule dependency chain.
0991 | 329908df925045718dbbe6d5e5941fe878a9839f | Agent ID verbs now don't require you to pick it up (#40524) | Deferred | Interactions, Gamerules | Depends on deferred index 0382's voice-lock and `UIRequiresLock` protections; removing CMU's sole possession gate first would broaden remote ID editing access.
0992 | dddb6163f5c256b2d9b6c38959c2690c89f330cd | Fix SpawnAndDeleteEntityCountTest Entities and last assert being incorrect (#40511) | AlreadyPresent | — | CMU's current integration test already contains the corrected entity set and final assertion.
0993 | 320e67a4110639ebd1d2110a9f84292007a00c1c | Predict identity (#40185) | Deferred | GameTicking, Gamerules, Interactions | The large identity server-to-shared prediction migration crosses mind, access, UI, and RMC identity systems.
0994 | 005683d074605d08b4dd3733c8183b4191abd241 | Miscellaneous Food/Drink/Edible fixes (#40060) | Deferred | Medical, Chemistry, Interactions | The mixed edible fixes depend on the unported DrinkComponent migration and divergent CMU food prototypes.
0995 | 7102da139b74776b5d1b0875ccaab2bad0fe141f | Fix dev crash when alt+clicking portals (#37540) | Ported (CS162, `e888eac87e` + `a0a1672c14`) | Movement, Interactions, Physics | Adapted the crash fix with stable subject capture and client-side destination existence/Nullspace guards, without pulling forward the broader portal refactor.
0996 | fabef941c2228b1073d06c3429cbc99ae10e62ad | Move circuit tiles and faux tiles to the cutter machine (#37982) | Deferred | Interactions | The recipe and migration bundle requires reconciliation with CMU lathe categories, materials, and tile content.
0997 | 1e219aaf493a6fff113fb539786259f826c360aa | Automatic changelog update | Irrelevant | — | Generated changelog only.
0998 | c55b41dff859a63f1885e2e3e05b67bc1b49da28 | bunch of small cleanups (#40529) | Superseded | Medical | Most hunks are absent or already reflected in CMU, and later target cleanup state replaces the remainder.
0999 | 7c39b4595f9512aa49ae5085fce5f39988b89d7f | Added diagnostic huds to the engi-vend (#40461) | Ported (CS163, `7519ec844d`) | Medical, Interactions | Added four existing diagnostic HUDs to EngiVend while leaving RMC vendor inventories untouched.
~~~

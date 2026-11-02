# Distress Signal FOB map support

`cmu.fob.reserve_enabled` defaults to **false**, is server-only, and is latched at round start.
All nine currently selectable `DistressSignal.supportedPlanets` entries are authored. Commented-out
planets and other presets are not enabled. Only primary Marine destinations select a FOB; tactical,
third-party-only and non-primary pads do not. Force-on-Force faction overrides do not apply in Distress Signal.

The first valid friendly landing fixes the faction's reserve center for the round. Deployable equipment
may be assembled on valid ground within a **40-tile Euclidean radius** of that center. Moving the terminal
or landing elsewhere never moves this area or issues another bank, weapon allocation or shipment.
Normal construction terrain, collision and operator checks still apply. Authored exclusions cover nearby
dropship footprints, including Alamo and the Omaha/Midway ground decks and their landing offsets.
Single-LZ maps are supported. A malformed layout keeps legacy protection and does not activate the pilot.

No terrain, doors, access restrictions, assault passages or existing spawn entities are changed by these
map additions. Only inert relay markers and FOB metadata are added. The two-gun budget, finite ammunition,
startup warnings, destructibility, one-time logistics and contested evacuation rules are unchanged.
Player-built layouts still need combat trials; free placement cannot guarantee uncovered attack lanes.

## Coverage

| Colony | Primary FOB sites | Relay markers |
| --- | --- | --- |
| Hope's Retreat | HopesRetreat, Octanis | 24 |
| Bosenmori Basho Redux | WesternPad, EasternPad | 17 |
| Port Nereid | Spaceport, CargoDepot | 15 |
| Sorokyne Strata | Sorokyne | 12 |
| Solaris Ridge | Communications, Engineering | 20 |
| Fort McNeil | FortMcNeil | 12 |
| Raijin Hydroelectric | Airstrip, Aerodrome | 21 |
| Shivas' Snowball | Research, Valley | 16 |
| LV624 Lazarus Landing | Robotics, Nexus | 17 |

## Authoring and validation

Add `CMUFobMap.sites` to the surface grid, referencing existing eligible dropship destinations.
Each destination gets `CMUFobSite` with a unique ID, `utilityExclusions`, and `relays`. Place anchored
`CMUFobRelayMarker` entities on clear, reachable surface floor. Set `CMUFobMapAnchor.relayName` to a
localized key in `fob.ftl`, then reference the saved marker UID from the site's relay list with `tier`
(`Near`, `Intermediate`, `Far`) and an approach group. Markers are hidden outside the map editor;
physical repair relays appear only for the selected site in an enabled round.

Each tier needs at least four distinct markers and two approach groups. A marker may be shared between
LZ pools, including with different tiers. Every relay must be **more than 50 tiles from every configured
LZ center**: the 40-tile deployment radius plus the longest support gun's ten-tile reach. This is checked
at runtime before legacy defenses are replaced. The authored positions remain more than 62 tiles away; shrinking the deployment radius does not move relay objectives.
Changing deployment or weapon range requires rechecking the relay clearance and placements.

Keep the same approach labels for each route branch across all three tiers, so a near/intermediate or
intermediate/far pair cannot offer the same branch under two different names. Approach labels describe
branches of the real route network, not guaranteed independent routes from the ramp. Roads, bridges and map chokepoints may be shared before alternatives diverge. Static route review
blocks hard obstructions, locked private shortcuts, preplaced resin doors, water and an 18-tile buffer
around initial threat/hive leaders. It is conservative, not an in-game navigation or combat simulation.
Map edits must recheck accessible paths and usable deployment ground; runtime validation only checks
references, grid anchoring, primary ownership, tier diversity and distance.

Pairs remain near/near before 60 minutes, near/intermediate at 60–90 minutes and intermediate/far afterward.
A job keeps its two alternatives until completion or expiry. Completing either adds reserve once;
unavailable pairs retry after 30 seconds without a free reward.

## Authored routes

Coordinates are surface-grid tile centers. The LZ ID and tier/group columns identify the actual map references.

### Hope's Retreat

Surface: `/Maps/CMU14/Planets/HopesRetreat/HopesRetreatMultiZ0.yml`. Retreat uses the east and west colony roads; Octanis uses the northern cavern branches and research approaches. Octanis patrols share the initial starport exit before splitting. Southern hive starts are excluded.

| LZ ID | Center | Near / intermediate / far walking distance |
| --- | --- | --- |
| HopesRetreat | (111.5, 62.5) | 67–137 / 128–192 / 197–222 tiles |
| Octanis | (15.5, 44.5) | 98–152 / 154–267 / 201–306 tiles |

| Relay | UID | Position | Eligibility (LZ: tier/group) |
| --- | --- | --- | --- |
| East road | 1000000026 | (110.5, -3.5) | HopesRetreat: Near/East |
| Central road | 1000000003 | (88.5, -6.5) | HopesRetreat: Near/East |
| West road | 1000000004 | (46.5, -9.5) | HopesRetreat: Near/West |
| West clearing | 1000000005 | (52.5, -14.5) | HopesRetreat: Near/West |
| Southeast road | 1000000006 | (103.5, -45.5) | HopesRetreat: Intermediate/East |
| Southern road | 1000000007 | (85.5, -55.5) | HopesRetreat: Intermediate/East |
| Engineering approach | 1000000008 | (49.5, -41.5) | HopesRetreat: Intermediate/West |
| Southwest junction | 1000000009 | (49.5, -58.5) | HopesRetreat: Intermediate/West |
| Southeast cave mouth | 1000000010 | (109.5, -84.5) | HopesRetreat: Far/East |
| Southeast caves | 1000000011 | (110.5, -93.5) | HopesRetreat: Far/East |
| East cavern junction | 1000000012 | (26.5, -64.5) | HopesRetreat: Far/West |
| Octanis approach | 1000000013 | (13.5, -53.5) | HopesRetreat: Far/West |
| North cavern west | 1000000014 | (-48.5, 10.5) | Octanis: Near/West |
| North cavern bend | 1000000015 | (-54.5, -3.5) | Octanis: Near/West |
| North cavern east | 1000000016 | (-19.5, -16.5) | Octanis: Near/East |
| East cavern mouth | 1000000017 | (-12.5, -21.5) | Octanis: Near/East |
| Research west approach | 1000000018 | (-64.5, -27.5) | Octanis: Intermediate/West |
| Research south approach | 1000000019 | (-48.5, -40.5) | Octanis: Intermediate/West |
| Octanis north approach | 1000000020 | (6.5, -52.5) | Octanis: Intermediate/East |
| Octanis administration approach | 1000000021 | (17.5, -54.5) | Octanis: Intermediate/East |
| Research south cavern | 1000000022 | (-52.5, -60.5) | Octanis: Far/West |
| Southwest cavern | 1000000023 | (-34.5, -74.5) | Octanis: Far/West |
| East cavern south | 1000000024 | (27.5, -69.5) | Octanis: Far/East |
| Retreat southwest approach | 1000000025 | (50.5, -62.5) | Octanis: Far/East |

### Bosenmori Basho Redux

Surface: `/Maps/CMU14/Planets/BosenmoriBasho/BosenmoriBashoMultiZ0.yml`. Western LZ patrols split between the western river crossing and north-sector road. Eastern LZ starts with south-sector and north-sector alternatives, then reaches filtration, mining and field research. LZ3 is not a primary destination and does not receive a FOB.

| LZ ID | Center | Near / intermediate / far walking distance |
| --- | --- | --- |
| WesternPad | (37.5, 43.5) | 97–140 / 159–191 / 197–259 tiles |
| EasternPad | (153.5, 36.5) | 79–118 / 139–199 / 199–252 tiles |

| Relay | UID | Position | Eligibility (LZ: tier/group) |
| --- | --- | --- | --- |
| West riverbank | 56424 | (20.5, 110.5) | WesternPad: Near/West |
| Western crossing | 56425 | (39.5, 111.5) | WesternPad: Near/West; EasternPad: Intermediate/West |
| North sector west | 56426 | (97.5, 80.5) | WesternPad: Near/East; EasternPad: Near/North |
| North sector road | 56427 | (100.5, 97.5) | WesternPad: Near/East; EasternPad: Near/North |
| Northwest barrens | 56428 | (20.5, 136.5) | WesternPad: Intermediate/West |
| Mining approach | 56429 | (42.5, 139.5) | WesternPad: Intermediate/West |
| Filtration west approach | 56430 | (102.5, 124.5) | WesternPad: Intermediate/East; EasternPad: Intermediate/North |
| Filtration north approach | 56431 | (116.5, 135.5) | WesternPad: Intermediate/East |
| Mining western clearing | 56432 | (47.5, 164.5) | WesternPad: Far/West; EasternPad: Far/West |
| Mining northern clearing | 56433 | (68.5, 175.5) | WesternPad: Far/West |
| Field research south | 56434 | (120.5, 163.5) | WesternPad: Far/East; EasternPad: Far/North |
| Field research east | 56435 | (145.5, 174.5) | WesternPad: Far/East; EasternPad: Far/North |
| Southwest sector south | 56436 | (94.5, 5.5) | EasternPad: Near/West |
| Southwest sector junction | 56437 | (94.5, 16.5) | EasternPad: Near/West |
| Northern crossing | 56438 | (125.5, 114.5) | EasternPad: Intermediate/North |
| West river approach | 56439 | (69.5, 103.5) | EasternPad: Intermediate/West |
| Pilgrim path | 56440 | (75.5, 155.5) | EasternPad: Far/West |

### Port Nereid

Surface: `/Maps/CMU14/Planets/PortNereid/portneroidmultiz0.yml`. Patrols use the habitation roads, northern jungle and processor approaches. Cargo also has initial southern jungle alternatives. Northern routes share the existing canal crossing before separating; no new bridges or sealed shortcuts are added.

| LZ ID | Center | Near / intermediate / far walking distance |
| --- | --- | --- |
| Spaceport | (92.5, 55.5) | 105–144 / 151–169 / 167–299 tiles |
| CargoDepot | (-3.5, 34.5) | 95–221 / 174–216 / 232–259 tiles |

| Relay | UID | Position | Eligibility (LZ: tier/group) |
| --- | --- | --- | --- |
| Medical north approach | 1000000005 | (77.5, 119.5) | Spaceport: Near/East; CargoDepot: Intermediate/East |
| Northeast jungle south | 1000000006 | (112.5, 116.5) | Spaceport: Near/East; CargoDepot: Intermediate/East |
| Habitation west road | 1000000007 | (24.5, 103.5) | Spaceport: Near/West; CargoDepot: Near/West |
| Habitation north road | 1000000008 | (37.5, 118.5) | Spaceport: Near/West |
| Northeast jungle road | 1000000009 | (107.5, 131.5) | Spaceport: Intermediate/East; CargoDepot: Far/East |
| Port north approach | 1000000010 | (125.5, 137.5) | Spaceport: Intermediate/East; CargoDepot: Far/East |
| Northwest jungle east | 1000000011 | (19.5, 133.5) | Spaceport: Intermediate/West; CargoDepot: Intermediate/West |
| Processor southwest | 1000000012 | (32.5, 144.5) | Spaceport: Intermediate/West; CargoDepot: Intermediate/West |
| Disposals north | 1000000013 | (115.5, 145.5) | Spaceport: Far/East |
| East perimeter | 1000000014 | (132.5, 154.5) | Spaceport: Far/East |
| Northwest jungle west | 1000000015 | (-5.5, 134.5) | Spaceport: Far/West; CargoDepot: Far/West |
| Western processor approach | 1000000016 | (-7.5, 149.5) | Spaceport: Far/West; CargoDepot: Far/West |
| South jungle clearing | 1000000017 | (54.5, -2.5) | CargoDepot: Near/East |
| Southeast jungle coast | 1000000018 | (65.5, -2.5) | CargoDepot: Near/East |
| Habitation southwest | 1000000019 | (-5.5, 109.5) | CargoDepot: Near/West |

### Sorokyne Strata

Surface: `/Maps/CMU14/Planets/SorokyneStrata/sorokyne0.yml`. The single Marine LZ uses marsh and outpost approaches, progressing to northern outpost and parts-storage passages. Third-party landing pads do not select a FOB. The primary pad has no floor at its center: patrol access was checked from its existing groundside edge, and its multi-deck offset is included in the landing exclusion.

| LZ ID | Center | Near / intermediate / far walking distance |
| --- | --- | --- |
| Sorokyne | (10.5, 108.5) | 79–109 / 133–163 / 155–251 tiles |

| Relay | UID | Position | Eligibility (LZ: tier/group) |
| --- | --- | --- | --- |
| Marsh crash south | 1000000010 | (15.5, 42.5) | Sorokyne: Near/South |
| Marsh center | 1000000011 | (44.5, 43.5) | Sorokyne: Near/South |
| Telecomms vehicle yard | 1000000012 | (65.5, 64.5) | Sorokyne: Near/North |
| Flight control approach | 1000000013 | (82.5, 85.5) | Sorokyne: Near/North |
| Near south landing | 1000000014 | (38.5, 15.5) | Sorokyne: Intermediate/South |
| Relay marshes | 1000000015 | (67.5, 15.5) | Sorokyne: Intermediate/South |
| Outpost decks | 1000000016 | (95.5, 105.5) | Sorokyne: Intermediate/North |
| North outpost west | 1000000017 | (97.5, 124.5) | Sorokyne: Intermediate/North |
| Parts storage caves | 1000000018 | (107.5, 44.5) | Sorokyne: Far/South |
| South outpost east | 1000000019 | (126.5, 53.5) | Sorokyne: Far/South |
| North outpost east | 1000000020 | (125.5, 127.5) | Sorokyne: Far/North |
| Northeast outpost passage | 1000000021 | (147.5, 122.5) | Sorokyne: Far/North |

### Solaris Ridge

Surface: `/Maps/CMU14/Planets/SolarisRidge/SolarisRidgeMultiZ0.yml`. Communications patrols split between northern colony roads and medical/central roads. Engineering patrols use the southwest caves and eastern engineering/filtration approaches before moving deeper into the colony. Initial hive areas remain excluded.

| LZ ID | Center | Near / intermediate / far walking distance |
| --- | --- | --- |
| Communications | (26.5, -19.5) | 101–153 / 157–192 / 196–215 tiles |
| Engineering | (32.5, -119.5) | 92–162 / 123–281 / 252–279 tiles |

| Relay | UID | Position | Eligibility (LZ: tier/group) |
| --- | --- | --- | --- |
| Medical east approach | 30054 | (79.5, -65.5) | Communications: Near/South; Engineering: Intermediate/North |
| Dormitory road | 30055 | (105.5, -61.5) | Communications: Near/South |
| North colony road | 30056 | (102.5, -22.5) | Communications: Near/North |
| Northern clearing | 30057 | (116.5, -24.5) | Communications: Near/North; Engineering: Far/North |
| Central colony road | 30058 | (111.5, -91.5) | Communications: Intermediate/South; Engineering: Intermediate/North |
| Office approach | 30059 | (124.5, -105.5) | Communications: Intermediate/South |
| North cave approach | 30060 | (138.5, -29.5) | Communications: Intermediate/North |
| Hydroponics approach | 30061 | (149.5, -64.5) | Communications: Intermediate/North; Engineering: Far/North |
| South colony road | 30062 | (102.5, -138.5) | Communications: Far/South |
| Filtration approach | 30063 | (121.5, -125.5) | Communications: Far/South |
| Eastern colony road | 30064 | (151.5, -90.5) | Communications: Far/North |
| Lambda cave approach | 30065 | (167.5, -84.5) | Communications: Far/North |
| Southwest cave bend | 30066 | (81.5, -162.5) | Engineering: Near/South |
| Mining eastern approach | 30067 | (65.5, -188.5) | Engineering: Near/South |
| Engineering east approach | 30068 | (95.5, -134.5) | Engineering: Near/North |
| Filtration west approach | 30069 | (105.5, -116.5) | Engineering: Near/North |
| Eta approach | 30070 | (103.5, -153.5) | Engineering: Intermediate/South |
| Research west approach | 30071 | (120.5, -180.5) | Engineering: Intermediate/South |
| Research southern passage | 30072 | (133.5, -196.5) | Engineering: Far/South |
| Research eastern passage | 30073 | (151.5, -176.5) | Engineering: Far/South |

### Fort McNeil

Surface: `/Maps/CMU14/Planets/FortMcNeil/FortMcNeilMultiZ0.yml`. The single Marine LZ sends patrols north along the eastern road/smuggler approach or south past hydroponics. Later alternatives reach the northern perimeter and southern drill/central caves. Routes avoid the initial western and central hive areas.

| LZ ID | Center | Near / intermediate / far walking distance |
| --- | --- | --- |
| FortMcNeil | (150.5, 21.5) | 95–146 / 125–157 / 165–217 tiles |

| Relay | UID | Position | Eligibility (LZ: tier/group) |
| --- | --- | --- | --- |
| Eastern road north | 1000000002 | (95.5, 51.5) | FortMcNeil: Near/North |
| Smuggler approach | 1000000003 | (111.5, 76.5) | FortMcNeil: Near/North |
| Hydroponics south approach | 1000000004 | (87.5, 27.5) | FortMcNeil: Near/South |
| Southeast exterior | 1000000005 | (84.5, 11.5) | FortMcNeil: Near/South |
| Northern exterior east | 1000000006 | (85.5, 81.5) | FortMcNeil: Intermediate/North |
| Northern drill approach | 1000000007 | (77.5, 89.5) | FortMcNeil: Intermediate/North |
| Southern exterior east | 1000000008 | (75.5, 9.5) | FortMcNeil: Intermediate/South |
| Southern drill approach | 1000000009 | (55.5, 29.5) | FortMcNeil: Intermediate/South |
| Northern exterior west | 1000000010 | (53.5, 81.5) | FortMcNeil: Far/North |
| Northwest drill passage | 1000000011 | (49.5, 79.5) | FortMcNeil: Far/North |
| Western exterior south | 1000000012 | (49.5, 43.5) | FortMcNeil: Far/South |
| Central caves east | 1000000013 | (36.5, 42.5) | FortMcNeil: Far/South |

### Raijin Hydroelectric

Surface: `/Maps/CMU14/Planets/Trijent/trijent.yml`. Airstrip patrols split between the eastern valley and southern dam tunnels. Aerodrome patrols start at the cargo/container routes and treatment approaches, then move toward hydroponics and the southern dam. Existing bridges and tunnels remain unchanged.

| LZ ID | Center | Near / intermediate / far walking distance |
| --- | --- | --- |
| Airstrip | (34.5, -26.5) | 88–111 / 99–151 / 180–208 tiles |
| Aerodrome | (21.5, -206.5) | 84–120 / 115–144 / 153–230 tiles |

| Relay | UID | Position | Eligibility (LZ: tier/group) |
| --- | --- | --- | --- |
| Northwest valley | 34730 | (98.5, -50.5) | Airstrip: Near/East |
| Labs valley | 34731 | (102.5, -19.5) | Airstrip: Near/East |
| Western dam caves | 34732 | (8.5, -96.5) | Airstrip: Near/South |
| West tunnel | 34733 | (46.5, -108.5) | Airstrip: Near/South |
| North dam riverbank | 34734 | (111.5, -48.5) | Airstrip: Intermediate/East |
| Dam east valley | 34735 | (149.5, -60.5) | Airstrip: Intermediate/East |
| Telecomms valley north | 34736 | (18.5, -115.5) | Airstrip: Intermediate/South; Aerodrome: Far/North |
| South tunnel junction | 34737 | (77.5, -112.5) | Airstrip: Intermediate/South; Aerodrome: Far/North |
| Wilderness road | 34738 | (175.5, -51.5) | Airstrip: Far/East |
| Crashsite approach | 34739 | (181.5, -83.5) | Airstrip: Far/East |
| South tunnel entrance | 34740 | (86.5, -150.5) | Airstrip: Far/South; Aerodrome: Intermediate/North |
| Hydro valley north | 34741 | (100.5, -156.5) | Airstrip: Far/South |
| Cargo valley east | 34742 | (76.5, -173.5) | Aerodrome: Near/North |
| Container yard | 34743 | (81.5, -182.5) | Aerodrome: Near/North |
| Treatment south approach | 34744 | (93.5, -212.5) | Aerodrome: Near/East |
| Treatment east approach | 34745 | (120.5, -209.5) | Aerodrome: Near/East |
| Hydro valley west | 34746 | (87.5, -157.5) | Aerodrome: Intermediate/North |
| Hydroponics loading approach | 34747 | (137.5, -182.5) | Aerodrome: Intermediate/East |
| Southeast riverbank | 34748 | (143.5, -202.5) | Aerodrome: Intermediate/East |
| Dam southern riverbank | 34749 | (104.5, -119.5) | Aerodrome: Far/East |
| Dam valley south | 34750 | (99.5, -131.5) | Aerodrome: Far/East |

### Shivas' Snowball

Surface: `/Maps/CMU14/Planets/ShivasSnowball/ShivasSnowballMultiZ0.yml`. Research LZ patrols split between northern colony approaches and the southern camp passages. Valley LZ patrols use warehouse/southwest valley and camp alternatives, then progress through the colony and junkyard. Eastern hive starts are excluded.

| LZ ID | Center | Near / intermediate / far walking distance |
| --- | --- | --- |
| Research | (22.5, 138.5) | 74–115 / 117–167 / 174–223 tiles |
| Valley | (23.5, 32.5) | 68–98 / 120–186 / 149–205 tiles |

| Relay | UID | Position | Eligibility (LZ: tier/group) |
| --- | --- | --- | --- |
| Colony northwest clearing | 1000000007 | (86.5, 132.5) | Research: Near/North; Valley: Intermediate/North |
| Colony north approach | 1000000008 | (98.5, 143.5) | Research: Near/North; Valley: Intermediate/North |
| Camp west passage | 1000000009 | (63.5, 82.5) | Research: Near/South; Valley: Near/North |
| Camp east passage | 1000000010 | (76.5, 77.5) | Research: Near/South; Valley: Near/North |
| Colony central approach | 1000000011 | (127.5, 126.5) | Research: Intermediate/North; Valley: Far/North |
| Colony east road | 1000000012 | (142.5, 118.5) | Research: Intermediate/North; Valley: Far/North |
| Junkyard north approach | 1000000013 | (98.5, 63.5) | Research: Intermediate/South |
| South administration approach | 1000000014 | (105.5, 70.5) | Research: Intermediate/South |
| Colony east clearing | 1000000015 | (153.5, 95.5) | Research: Far/North |
| Garage east approach | 1000000016 | (166.5, 82.5) | Research: Far/North |
| Junkyard central | 1000000017 | (123.5, 42.5) | Research: Far/South; Valley: Intermediate/East |
| Junkyard south | 1000000018 | (133.5, 26.5) | Research: Far/South; Valley: Intermediate/East |
| Southwest valley east | 1000000019 | (88.5, 33.5) | Valley: Near/East |
| Warehouse caves east | 1000000020 | (87.5, 16.5) | Valley: Near/East |
| Research south approach | 1000000021 | (157.5, 31.5) | Valley: Far/East |
| Research west clearing | 1000000022 | (161.5, 57.5) | Valley: Far/East |

### LV624 Lazarus Landing

Surface: `/Maps/CMU14/Planets/LV624/LV624MultiZ0.yml`. Robotics patrols use western barrens and the central/north jungle, progressing to northern barrens and temple approaches. Nexus patrols use eastern barrens and central/north jungle. Previously authored relays inside the expanded deployment circles have been relocated.

| LZ ID | Center | Near / intermediate / far walking distance |
| --- | --- | --- |
| Robotics | (-51.5, -70.5) | 187–210 / 213–279 / 284–316 tiles |
| Nexus | (65.5, -77.5) | 156–209 / 179–271 / 213–240 tiles |

| Relay | UID | Position | Eligibility (LZ: tier/group) |
| --- | --- | --- | --- |
| West barrens south | 23659 | (-29.5, -7.5) | Robotics: Near/West; Nexus: Intermediate/West |
| Southwest caves mouth | 23660 | (-38.5, -2.5) | Robotics: Near/West |
| North jungle west | 23661 | (4.5, -29.5) | Robotics: Near/East; Nexus: Near/West |
| Central road north | 23662 | (7.5, -43.5) | Robotics: Near/East; Nexus: Near/West |
| West barrens north | 23663 | (-14.5, 5.5) | Robotics: Intermediate/West |
| Central barrens west | 23664 | (-1.5, 7.5) | Robotics: Intermediate/West |
| East barrens | 23665 | (29.5, -0.5) | Robotics: Intermediate/East |
| Eastern riverbank | 23666 | (53.5, -12.5) | Robotics: Intermediate/East; Nexus: Near/East |
| North barrens | 23667 | (29.5, 26.5) | Robotics: Far/West; Nexus: Far/West |
| North pass | 23668 | (20.5, 33.5) | Robotics: Far/West |
| East cave approach | 23669 | (60.5, 26.5) | Robotics: Far/East; Nexus: Far/East |
| Temple northern passage | 23670 | (51.5, 26.5) | Robotics: Far/East; Nexus: Far/East |
| Southeast barrens south | 23671 | (73.5, -13.5) | Nexus: Near/East |
| Southeast barrens north | 23672 | (80.5, -3.5) | Nexus: Intermediate/East |
| Temple approach | 23673 | (58.5, 17.5) | Nexus: Intermediate/East |
| Central barrens south | 23674 | (12.5, -8.5) | Nexus: Intermediate/West |
| Containers approach | 23675 | (26.5, 13.5) | Nexus: Far/West |

## Verification and rollout

The source audit checks every supported planet and all surface primary destinations, including upper/lower
map declarations; YAML references/entity counts and unrelated map data are checked against the pre-edit maps.
`DistressSiteSupportsPhysicalDeploymentAndRelayProgression` exercises each of the sixteen LZs through real
map loading, landing, physical terminal/platform deployment near the outer boundary, repeat landings and
all three relay tiers. Existing FOB regressions cover flag-off/other-preset behavior, logistics, equipment,
repair interruption and evacuation. `RelayInsideDeploymentAreaRejectsLayoutAndRetainsLegacy` covers a bad
placement failing closed. Successful checks do not replace live multiplayer balance trials on each map.

Keep the flag off until staff select a trial round. Observe patrol round-trip times, build patterns,
reserve starvation, gun ammunition use and boarding pressure. Use the existing per-round FOB trial report
and test all ships' ramps and ground-level footprints before a general rollout.

Verified on 2026-11-01: Shared, Client, Server and integration builds succeeded. Sixty unique FOB/sentry
scenarios passed across the full run and focused rerun, covering all sixteen primary LZs, with no engine
errors or unresolved skipped cases. YAML/reference, localization, geometry, encoding and final diff checks passed.

Reproduce the regression scope with:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUFob|FullyQualifiedName~DropshipSentryRegressionTest'
```

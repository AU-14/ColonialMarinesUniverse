# SUNDIAL recovery sectors

These are seven alternative incidents in the colony's hinterland. They are not seven simultaneous
losses of the same machine, or new established faction history. A scenario picks a coherent biome,
landform and recovery footprint; changing its seed still generates different terrain and routes.

SUNDIAL is a trial extraction rig for deposits too scattered for conventional colony machinery.
Its cutter and paired calibration controller must return together. Govfor recovers the assembly
and its records; the recovery assignment is not authorization for a field trial.

## Select a story

```text
cmu-expedition scenarios
cmu-expedition scenario CMUBlackwaterReach 42
cmu-expedition status <map ID>
cmu-expedition briefing <map ID>
cmu-expedition open <map ID>
cmu-expedition-visit <map ID>
```

The LZ opens and announces automatically when ready. Wait for ready status before visiting. One generation job and three expedition maps
are allowed at once. Use existing map tools to remove an unused map only after evacuating it.
From the server console, append the connected username to the visit command.

| Scenario ID | Sector | Environment | Recovery footprint |
| --- | --- | --- | --- |
| `CMUBlackwaterReach` | Blackwater Reach | Woodland / RiverValley | CrashRecovery |
| `CMUReedwakeBasin` | Reedwake Basin | SwampJungle / Wetlands | SurveyCamp |
| `CMUAshfallScar` | Ashfall Scar | BurnedWoodland / Ridgeline | CrashRecovery |
| `CMUWhiteoutShelf` | Whiteout Shelf | Tundra / Fjord | BrokenConvoy |
| `CMUGlasswaterStrand` | Glasswater Strand | Beach / Coast | CrashRecovery |
| `CMUCairnPass` | Cairn Pass | Mountain / Highlands | BrokenConvoy |
| `CMUHollowSignal` | Hollow Signal | Woodland / LakeCountry | LostRelay |

## Stories and discoveries

| Sector | Initial account | What the evidence adds |
| --- | --- | --- |
| Blackwater Reach | A transport vanished in bad weather over old prospecting trails. | The pilot followed a decommissioned beacon, and a cargo restraint released before impact. |
| Reedwake Basin | An overdue shipment last checked in at a temporary wetland survey clearing. | A contractor attempted an unauthorized demonstration, then the crew shut down the clogged rig and withdrew. |
| Ashfall Scar | A transport was lost in a burned forest. | The forest fire predates the crash; an auxiliary battery rack overheated before impact. |
| Whiteout Shelf | A forced landing became an unsuccessful overland delivery. | The crew followed the abandonment procedure, leaving the machine to evacuate an injured colleague. |
| Glasswater Strand | A coastal forced landing left equipment beside the surf. | An unauthorized salvage party removed the transmitter and tried to obscure ownership. |
| Cairn Pass | A delivery crew abandoned a loaded carrier on an old survey route. | Dispatch treated a request for assessment as permission to use a route never cleared for freight. |
| Hollow Signal | A lost cargo tag appeared beside a repeating rescue transmission. | Someone rebroadcast an old drill recording; the miner was deliberately unloaded at the relay. |

Each story places exactly three small evidence items:
- A readable orders sheet at the LZ's east edge, containing the mission, local history and shared recovery instructions.
- A readable field record at the first secondary natural landmark.
- A recorder beside the miner, with a retained transcript available through the detailed Examine verb.

The miner also has a scenario-specific detailed Examine entry. The papers use the existing Paper UI;
the recorder and machine use RMC lore examination. Placement uses dry, connected site centers and
objective approaches. Notes add no structures to wilderness landmarks. The recorder remains a
carryable item; the prototype miner currently remains a static prop.

## Current implementation boundary

Crash recovery chooses one of six damaged aircraft derivatives. The source geometry is the existing
Fallujah map (`CMU14/Shuttles/alamo.yml`) or gunship map (`_RMC14/Shuttles/dynamic_gunship.yml`).
Only hull/floor geometry and licensed art are reused; flight machinery is not imported into the wreck.

| Baseline | Derivative | Section layout |
| --- | --- | --- |
| Fallujah | Longhaul cargo transport | Extended aft cargo section |
| Fallujah | Relief utility transport | Forward section displaced from the cargo body |
| Fallujah | Surveyor field transport | Torn port assembly |
| Gunship | Kestrel gunship | Separated aft section |
| Gunship | Escort strike transport | Broken forward section |
| Gunship | Pathfinder scout gunship | Detached port engine side |

The seed also chooses missing panels, floor breaches, heading and scattered cargo. Nearby rock or
water can determine the impact heading. Scorching extends across the wider impact area and strips
the approach; cliff strikes add loose rock, while shoreline wrecks leave the water intact. The status
and briefing commands report the generated aircraft and impact type. Examples:

```text
cmu-expedition generate Mountain 42 Highlands CrashRecovery
cmu-expedition scenario CMUAshfallScar 43
cmu-expedition scenario CMUGlasswaterStrand 44
```

These stories add readable evidence, location identity and selectable generation presets. Crew
movements, weather, tides, controller separation, machine operation, evacuation, rewards and mission
completion remain narrative context. CrashRecovery now builds damaged Fallujah/gunship derivatives
and scars the surrounding terrain; BrokenConvoy still uses the earlier carrier footprint. Native fire
pockets ignite when the LZ opens. An experimental armed scavenger squad can be added explicitly with
`cmu-expedition-ai <map ID>`. The planet selection console remains future work.

Scenario definitions live in `Content.CMU/Resources/Prototypes/CMU14/Expeditions/scenarios.yml`.
All player-facing prose lives in `Content.CMU/Resources/Locale/en-US/CMU14/expedition-stories.ftl`.
Terrain-only `generate` commands remain available and do not attach a named story.

## Verification

```text
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~CMUExpeditionMapTest
```

`NamedScenariosPlaceReadableEvidenceOnDryConnectedSites` materializes every preset, checks resolved
paper and examination text, dry accessibility, target lore, sector names, and cleanup. Existing map
tests cover native RMC shorelines, bounded generation, cancelled jobs and explicit LZ publication.
`InvalidProfilesAndDeletedBuildsDoNotLeaveTheGeneratorBusy` also rejects an unknown scenario.

Manual checks: generate two seeds for a selected story, walk from the LZ to the field record and
miner, read both papers, inspect the recorder and miner using the detailed Examine verb, and check
that the selected sector name appears in dropship destinations. Test real extraction once recovery
mechanics exist.

# CMU threat port checklist

This checklist tracks the threat rewrite on `PvE`. Charger and Tank were intentionally removed by the follow-up request. Paths are relative to the repository root; numbers are one-based source lines.

## Core

- [x] Remove the summoner module, UI, actions, prototypes, locale, and sprite. The remaining ghost/victory integrations now use the threat component: `Content.Server/Ghost/GhostSystem.cs:526`, `Content.CMU/Server/Threats/Rules/ThreatRuleHelper.cs:96`.
- [x] Provide common, Hunter, Smoker, Boomer, Screamer, Jockey, Spitter, and Witch entities with prototype-configurable health and speed: `Content.CMU/Resources/Prototypes/CMU14/Threats/Threat/mobs.yml:1`.
- [x] Exclude Charger and Tank from the new caste definitions and actions: `Content.IntegrationTests/_CMU14/Threats/ThreatPrototypeTest.cs:46`.
- [x] Give Jockey the dwarf mob body: `Content.CMU/Resources/Prototypes/CMU14/Threats/Threat/base.yml:71`, `Content.CMU/Resources/Prototypes/CMU14/Threats/Threat/mobs.yml:139`.
- [x] Give each caste its requested colored eyes using the existing one-pixel human eye texture and an unshaded eye layer; no light is emitted: `Content.CMU/Resources/Prototypes/CMU14/Threats/Threat/mobs.yml`, `Content.CMU/Client/Threats/Mobs/Threat/CMUThreatEyesVisualsSystem.cs`, `Content.IntegrationTests/_CMU14/Threats/ThreatPrototypeTest.cs`.
- [x] Chase non-threat living targets using melee HTN plus a persistent NPC melee target so plan churn cannot leave the threat passive: `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Ai.cs`, `Content.CMU/Resources/Prototypes/CMU14/Threats/Threat/commands.yml`.
- [x] Equip special/common threat from civilian-vendor outfits and provide a separate armored NSPA police common variant: `Content.CMU/Resources/Prototypes/CMU14/Threats/Threat/mobs.yml`.
- [x] Path through powered and unpowered doors using the existing pry system. Powered airlocks already multiply pry time in `Content.Shared/Doors/Systems/SharedAirlockSystem.cs:99`: `Content.CMU/Resources/Prototypes/CMU14/Threats/Threat/base.yml:28`.
- [x] Keep targeted abilities server-authoritative and range-limited to at most eight tiles: `Content.CMU/Resources/Prototypes/CMU14/Threats/Threat/actions.yml:14`, `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.cs:173`.
- [x] Share timed pin damage and automatic release on death, stun, deletion, excess distance, or a shove to attacker/victim: `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Pins.cs:12`, `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Pins.cs:216`.
- [x] Keep ability execution separate from AI decisions so AI and player actions call the same methods: `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Ai.cs:152`, `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Hunter.cs:11`.
- [x] Expose cooldowns, ranges, damage, health, speed, and durations through components/prototypes: `Content.CMU/Shared/Threats/Mobs/Threat/CMUThreatComponents.cs:50`, `Content.CMU/Resources/Prototypes/CMU14/Threats/Threat/mobs.yml:1`.

## Castes

- [x] Hunter uses the runner's native leap action and remains latched, attacking until the hunter dies: `Content.CMU/Resources/Prototypes/CMU14/Threats/Threat/mobs.yml`, `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Hunter.cs`, `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Pins.cs`.
- [x] Hunter prefers isolated survivors and caps leap speed toward its target: `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Ai.cs:60`, `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Hunter.cs:62`.
- [ ] Hunter has a high-speed leap, but no separate arc/crouch animation or obstacle/corner positioning: `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Hunter.cs:18`.
- [x] Smoker line-of-sight grab, pull/choke, and HSDP smoke on activation and death: `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Smoker.cs:14`, `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.cs:128`.
- [x] Smoker tongue has directionally rotated beam segments; damaging a segment or obstructing the line releases the victim: `Content.Shared/_RMC14/Line/LineSystem.cs`, `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Pins.cs`, `Content.CMU/Resources/Prototypes/CMU14/Threats/Threat/effects.yml`.
- [ ] Smoker validates line of sight instantly rather than firing a traveling tongue projectile: `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Smoker.cs:14`.
- [x] Boomer cone bile and horde attraction; death randomly creates an IED blast, small boiler acid cloud, or small boiler neurotoxin cloud: `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Boomer.cs`, `Content.CMU/Resources/Prototypes/CMU14/Threats/Threat/effects.yml`.
- [x] Boomer bile briefly blurs the victim's vision: `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Boomer.cs:74`.
- [ ] Boomer AI does not yet explicitly choose grouped survivors over isolated survivors: `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Ai.cs:47`.
- [x] Screamer calls only after taking damage; civilian and police commons rise from the ground before chasing a nearby human: `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Screamer.cs`, `Content.CMU/Client/Threats/Mobs/Threat/CMUThreatEmergenceSystem.cs`.
- [x] Jockey uses a dwarf body, remains positioned on its victim, deals ride damage, and forces changing random movement directions: `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Jockey.cs`, `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Pins.cs`.
- [ ] Jockey has no dedicated struggle input; limited victim movement is supplied by movement modifiers: `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Pins.cs:41`.
- [x] Spitter creates a timed, expanding damage pool within eight tiles: `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Spitter.cs:16`.
- [x] Spitter now fires a colliding projectile with a client arc; impact/endpoint creates a networked pool whose sprite grows with its radius: `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Spitter.cs:17`, `Content.CMU/Client/Threats/Mobs/Threat/CMUThreatAcidVisualsSystem.cs:21`.
- [ ] Spitter AI targets a survivor's location rather than scoring groups, pins, or chokepoints: `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Ai.cs:166`.
- [x] Witch sleeps until nearby survivors or damage raise irritation, then enrages: `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.Ai.cs:148`.
- [ ] Witch does not yet react to flashlights or noise.

## Feedback and debugging

- [x] Ability actions use existing icons and configurable sounds; emergence and acid have client visuals: `Content.CMU/Resources/Prototypes/CMU14/Threats/Threat/actions.yml:1`, `Content.CMU/Client/Threats/Mobs/Threat/CMUThreatEmergenceSystem.cs:18`.
- [x] Dedicated docile, attack, hurt, and death vocal collections are wired to threat behavior; idle audio is spaced out, hurt audio is chance/cooldown limited, and the generic human deathgasp is removed: `Content.CMU/Shared/Threats/Mobs/Threat/CMUThreatComponents.cs`, `Content.CMU/Server/Threats/Mobs/Threat/CMUThreatSystem.cs`, `Content.CMU/Resources/Prototypes/CMU14/Threats/Threat/sounds.yml`.
- [ ] No dedicated threat-only admin debug commands, state inspection UI, or ability/pin lifecycle events have been added. Existing prototype spawn tools can spawn each caste.

## Verification

- [x] The full server, client, and integration-test build completed with zero errors using an isolated artifacts directory.
- [x] The YAML linter found no prototype YAML or static-reference errors with the freshly built game assemblies.
- [x] The new prototype integration test at `Content.IntegrationTests/_CMU14/Threats/ThreatPrototypeTest.cs:1` compiles with the integration-test project.
- [ ] Multiplayer, collision, pathfinding, and balance still need in-game validation.

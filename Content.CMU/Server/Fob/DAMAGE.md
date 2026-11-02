# FOB weapon damage snapshot

Updated 2026-11-02 after the direct-rocket damage increase. The feature is enabled by default for validated Distress Signal rounds.

## Warhead behavior

The rocket now deals **1,100 blunt direct damage with 35 armor piercing**. The existing RMC explosion remains at maximum intensity 10, slope 10 and total intensity 30. Heavy-sentry and fence damage are unchanged.

A direct hit on a T3 registers a separate damage allowance for that rocket: the target's actual critical threshold plus 10, limited to below its death threshold. Ordinary armor applies first. The allowance is applied before active shields, so shields can absorb the hit. Both the direct hit and its delayed explosion consume this same allowance. Nearby splash-only targets do not receive the direct payload or its allowance.

This is a limit on damage per rocket, not on the victim's accumulated damage. Other attacks and later rockets remain lethal, and a wounded T3 can die from its first rocket. The allowance remains associated with the original projectile UID after deletion so delayed blast processing cannot add a lethal second hit. Global damage multipliers still apply normally; the figures below use baseline settings.

## T3 direct hits

Measured using actual projectile collisions followed by the actual rocket explosion, facing toward and away from the shooter. Every listed unshielded T3 took the same combined damage from either facing. The direct hit spent the allowance, so the same rocket added no further blast damage to its primary victim. The critical threshold remains caste-specific; the gameplay tests also verify actual critical state after critical grace expires.

| Caste / strain | Rocket combined damage | Nominal firing DPS | Healthy target result |
| --- | ---: | ---: | --- |
| Boiler | 660.0 | 132.0 | Just above crit |
| Praetorian | 660.0 | 132.0 | Just above crit |
| Ravager | 660.0 | 132.0 | Just above crit |
| Crusher | 810.0 | 162.0 | Just above crit |
| Despoiler | 660.0 | 132.0 | Just above crit |
| Crusher Charger | 790.0 | 158.0 | Just above crit |
| Ravager Berserker | 600.0 | 120.0 | Just above crit |
| Ravager Hedgehog | 660.0 | 132.0 | Just above crit |
| Praetorian Valkyrie | 660.0 | 132.0 | Just above crit |
| Praetorian Dancer | 660.0 | 132.0 | Just above crit |
| Praetorian Oppressor | 660.0 | 132.0 | Just above crit |
| Boiler Sapper | 600.0 | 120.0 | Just above crit |

The rocket fires once every five seconds, carries twelve rockets, takes twelve seconds to reload and warns for three seconds before each launch. DPS excludes startup, warning time, reloads, travel, misses and obstruction. Active shields and additional defensive effects can reduce health damage below these figures.

## T2 direct hits

Carrier, Hivelord, Lurker, Spitter, Warrior, Burrower and Reaper each received **1,100 direct damage** in the actual collision probe, as did the measured Vampire, Venator and Alchemist strains. This exceeds their baseline death thresholds: a direct rocket can kill them outright. Subsequent damage to the corpse varies with death/gib processing and is not counted as useful damage. The direct payload has a nominal firing DPS of 220 against these targets.

## Heavy sentry and fence

- Heavy sentry: **40 damage/hit, 100 firing DPS** against the base T2/T3 castes above; 35 armor piercing, 100-round drum, 7-second reload and 1.5-second acquisition. Frontal Charger armor reduces this to **30.1/hit, 75.2 DPS**. Active defensive effects can reduce it further.
- Electric fence: **12 shock per triggered contact**, at most once per two seconds per section (**6 DPS ceiling**). The cooldown is shared between targets; this is not a continuous damage aura.

## Splash and armor

Blast-only damage was not increased. For example, the prior point-blank measurements were 115.1 blast damage to a Boiler, 32.9 to a Ravager and 19.5–21.7 to a Crusher depending on facing. More distant splash can be lower. The base xeno prototype has an existing 2x explosion coefficient, followed by caste explosion resistance and ordinary armor. The new primary-target allowance does not change those calculations for nearby xenos.

## Verification

The 44-case temporary measurement probe used `SharedProjectileSystem.ProjectileCollide` and the normal trigger/explosion path, with plated ground and a shooter placed in front of or behind each target. It passed in `.codex/test-results/Content.IntegrationTests-20261102-033403-740/` and was removed from the permanent suite.

Permanent behavior coverage:

- `RocketDirectHitBarelyCritsHealthyT3IncludingItsDelayedBlast`: actual critical state for Boiler, Crusher and frontal Charger after direct hit plus explosion; all three passed in `.codex/test-results/Content.IntegrationTests-20261102-032918-401/`.
- `RocketDamageLimitDoesNotProtectAgainstOtherAttacksOrAnotherRocket`: unrelated damage still applies and a follow-up rocket kills the wounded target.
- `RocketCriticalLimitPreservesActiveShieldsAndNormalSplash`: a shielded primary remains below crit while a nearby xeno receives ordinary splash.
- `SensorRocketKeepsOriginalTargetThenCommitsToLastPosition`: all four hit/range/roof/sensor cases passed again.

The latter six permanent cases and the temporary probe passed together (7 passed, none failed/skipped) in `.codex/test-results/Content.IntegrationTests-20261102-033403-740/`. Shared, Client and Server built successfully, with repository warnings remaining.

Current behavioral check command:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~RocketDirectHitBarelyCritsHealthyT3IncludingItsDelayedBlast|FullyQualifiedName~RocketDamageLimitDoesNotProtectAgainstOtherAttacksOrAnotherRocket|FullyQualifiedName~RocketCriticalLimitPreservesActiveShieldsAndNormalSplash|FullyQualifiedName~SensorRocketKeepsOriginalTargetThenCommitsToLastPosition'
```

Occupied multiplayer combat still needs a live trial.

# Expedition equipment, perception and logistics

The [squad management panel](SQUADS.md) adds spawn/order controls, doctrines, supply runners,
medical collection areas and live tactical diagnostics. Open it with `cmu-squads`.

These actions use native weapons, items, storage, interaction checks and do-afters. They apply to
uncontrolled expedition agents, including squads spawned on ordinary maps. Taking player control
cancels AI actions. Compilation and content-reference checks do not verify in-game behavior.

## Commands

```text
cmu-expedition-ai here 6 mixed uscm
cmu-expedition-ai here 3 rocketeer
cmu-expedition-ai here 2 sniper rmc
```

The fourth argument selects clothing and completes with Tab: `scavenger`, `uscm`, `rmc` (TWE),
`upp` (SPP), `pmc`, `clf`, `cmb`, `lacn`, `ccaf`, `uacg`, `prodigy`.
Clothing does not change weapons, team, IFF identity or orders. Use the existing
`cmu-expedition-orders` friendly/target actions to change affiliations. The default enemy is GOVFOR.
Status reports include the applied outfit, supply transfers, flare use, vision decisions and bipod attempts.

## Weapons and retention

- Native full auto is selected only when the gun supports it. AI volleys still limit rounds;
  chambering, recoil, skills, native fire delays and finite ammunition remain in force.
- Native bipods deploy when stationary with a distant visible target and a safe lane. Their native
  deployment time and movement/turn interruption still apply. A deployment attempt is not proof of a brace.
- Breachers prefer close range and use up to six shots per volley, subject to native pumping and fire rate.
- The `sniper` variant carries the native M42A family weapon and uses its aimed shot when stable,
  with one distant visible threat. New danger, damage, movement, loss of sight or control transfer
  cancels focus. The existing `marksman` variant keeps its SPR.
- Friendly lanes permit native IFF pass-through only on known non-rocket weapons with matching
  factions and enabled IFF. Trigger interlocks remain blocking. Explosives retain blast checks.
- Swaps stow the previous gun in its sling or accessible storage before releasing it. A one-handed
  backup may retain the primary in the other hand when storage is blocked; it fires unwielded
  until storage becomes available. Wielding requires genuinely empty hands and cannot discard
  another gun or supplies to make room. A blocked two-handed swap is rolled back.
  Only an empty disposable rocket tube is intentionally discarded. Quiet agents also secure
  remembered dropped weapons within reach while carrying a backup.
- Empty combatants try a loaded backup, safe reload or nearby compatible ammunition first.
  Without those, they seek actual shelter and use short, collision-checked escape steps to
  increase distance or reduce exposure when shelter is unavailable. An active escape step is
  retained through incoming hits; stopped or failed escapes are reconsidered once per second.
  They stay within their leash, retain native melee self-defense, and resume ordinary orders
  and supply routes once contact expires. Trapped agents cannot manufacture an escape or ammunition.

This integrates native [attachment toggles](https://github.com/AU-14/ColonialMarinesUniverse/blob/master/Content.Shared/_RMC14/Attachable/Systems/AttachableToggleableSystem.cs)
and [aimed shots](https://github.com/AU-14/ColonialMarinesUniverse/blob/master/Content.Shared/_RMC14/Weapons/Ranged/AimedShot/SharedRMCAimedShotSystem.cs).
It does not implement every arbitrary attachment or custom ammunition type.

## Hostile armed vehicles

Agents can observe real vehicle hulls with mounted guns, including armed Humvees, tanks and APCs.
Identity comes from native vehicle factions, drivers, gunners, living passengers and interior ownership.
A friendly identity vetoes hostility. Unknown, neutral, unarmed, totaled and actively cooking-off
vehicles are excluded from automatic targeting. Existing friendly/target orders still apply.

Rocketeers prioritize a visible hostile armed vehicle while carrying a loaded tube. Their expedition
tube fires native anti-tank ammunition; penetration, vehicle damage and cook-off use native rules.
Launch range and blast checks use the first hull impact instead of the centre of a large vehicle.
A wall or another body before the hull blocks the launch. Shots require a safe backblast and friendly
blast clearance; the squad rocket cooldown remains 20 seconds. A blocked rocketeer can search a
short, reachable launch position within roughly three metres, without abandoning a covering commitment
or chasing an unseen vehicle. Rifles can engage hulls but retain their native armor effectiveness.

## Sight and flares

Target recognition requires geometric sight, smoke clearance and sufficient light (except within
1.5 metres). The server estimates light from ambient map light, roofs and unoccluded point lights.
It caches light snapshots for 0.25 seconds. Native flare burn/fade state supplies its animated radius.
Client light masks cannot be reproduced exactly: masked/directional lights count only very close to
the source. This is a conservative server estimate, not pixel-identical client vision.

An unsuppressed native muzzle flash can reveal a noisy, fixed shot position for 1.2 seconds.
Ordinary guns may shoot that position with no entity lock; unseen movement is not tracked.
Rockets and aimed sniper locks require a visible target. Walls, smoke and friendly lanes still apply.

Every kit carries one native M94 pack containing eight real flares. Agents draw individual flares
through native item-slot ejection, then ignite and throw them toward a dark contact or route,
with a shared 30-second cooldown after a successful throw. Damage, a close rush, movement and blocked
throws interrupt handling. Flares are consumed, and replacement flares must be found or shared.

## Resupply and communication

Belts, the second-pocket pouch and backpacks are searched in that order for storage and consumption.
Quiet, unordered agents collect useful reserves: six spare magazines per carried gun or 24 shells,
two HE and two smoke grenades, eight fresh flares and one stored dressing. Packed flares count
toward reserves; a replacement pack is collected only with two or fewer flares remaining. Sharing
loose flares or surplus packs retains at least two flares for the donor. Capacity and whitelists
may stop collection earlier; these are targets, not guaranteed inventory sizes. A full shotgun still
recognizes compatible reserve shells. No ammunition or grenade is created by resupply behavior.

Nearby accessible bags, dead bodies and crates can supply items. Living/critical inventories are
excluded; opening crates uses native lock, weld and access checks. Retrieval uses an adjacent reachable
position, has a five-second bound and interrupts for combat/orders. Loaded replacement expedition
rocket tubes can be collected when the rocketeer has no loaded tube and has room.

Quiet squadmates within unobstructed interaction range can transfer compatible surplus. Donors keep
a reserve; ownership, need and recipient capacity are rechecked at completion. Transfers move the
actual item, without cloning it, and use a bounded handling pause.

Radio contact data continues silently every two seconds under native channel/range checks.
Audible callouts share a 25-second cooldown among friendlies within 40 metres, including other squads.
Continuously reported contacts are not announced again. After 60 seconds without reports an enemy
may become a new contact. Phrase pools reflect stress, injury, aggression, a close rush or medic role.

## In-game verification still required

1. Spawn all specialist variants and all ten outfits. Check complete clothing, belt/pouch/pack,
   one primary, sidearm and finite starting supplies. Swap under full storage, empty both weapons,
   knock agents down and disarm them. Check no useful gun is abandoned and recovery respects ownership.
   In particular, empty a breacher's shotgun with both free and occupied suit storage: it must
   sling/store the shotgun or retain it while firing the pistol one-handed. Repeat with a full
   backpack/belt and a utility item occupying the other hand. Exhaust all carried ammunition
   under fire in open ground and near doors/corners: verify escape progress, blocked-route retries,
   no walk back to the exposed spawn point, and renewed combat after a real resupply.
2. In daylight and dark rooms, test roofs, walls, windows, trees, point lights, fading flares and smoke.
   An unseen moving enemy must not be tracked. Fire once in darkness, then move: flash shots must
   remain near the original position and expire after 1.2 seconds. Repeat with a suppressor.
3. Expend ammunition and offer loose magazines, shotgun shells, open/closed/locked/welded crates,
   dead bodies and living inventories. Check real item counts, belt-first storage, native access,
   reserve limits and later reloads. Interrupt retrieval and sharing with combat and orders.
4. Compare deployed/undeployed bipods and supported fire modes. Damage, move and obscure a focusing
   sniper. Test IFF on/off, mismatched factions and trigger interlocks. No unsafe shot should bypass
   native checks. Verify native shotgun pumping and recoil while advancing into useful range.
5. Spawn a hostile armed Humvee, APC and tank with a rocketeer. Test stationary and moving hulls,
   firing from several angles, safe impact range, nearby friendlies and rear backblast blockers.
   Change crew/factions to friendly, remove the mounted weapon, total the vehicle and start cook-off:
   targeting must stop. Unarmed/neutral vehicles must not become automatic enemies.
6. Maintain contact with two friendly squads for 60 seconds, cycling targets and moving them.
   Check no repeated unchanged-contact lines and no more than three audible announcements while
   continuously co-located; silent report counters must continue. Test the no-report reset separately.
7. Measure update/search cost with six and twelve agents under simultaneous contact, nearby armed
   vehicles, dense trees and many point lights. Record stuck moves, rejected trigger reasons and
   supply/vision counters. No runtime performance or balance result is claimed from compilation.

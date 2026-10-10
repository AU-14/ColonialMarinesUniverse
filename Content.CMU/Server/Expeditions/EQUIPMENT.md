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

The default six-member `mixed` squad now includes a veteran rifleman, machine gunner, medic,
breacher, rocketeer and scout. `patrol` and `defense` provide mobile and defensive combined-arms
compositions. Individual role presets remain available. Faction outfits use native mobile armor
for scouts, skirmishers, medics and rocketeers, and native heavier choices for support/breach roles
where that faction has one. Clothing retains native protection and movement penalties.

All fourteen kits have finite, role-sized ammunition reserves: most carry four primary magazines,
assault/rich kits carry five, poor pistol kits carry three pistol magazines, machine gunners carry
three spare boxes, and breachers carry 24 spare slugs. These are additional to loaded ammunition.
Sidearms and their spare magazines occupy the utility pouch, leaving pack space for utilities and reserves.
Support roles use logistics packs; medics use a medical pack and a second stabilizer in their pouch.
Scouts, skirmishers, rocketeers, breachers, snipers and medics carry two smoke grenades.

Starting supplies fit their native container grids: the five-slot ammo belt holds at most five
magazines or shell handfuls; the large general pouch fits a normal pistol and small spare magazine.
The 21-by-two pack grid holds each role's utilities, including the medic's larger supply list.
Armor storage is available for later resupply but is not needed to fit a starting kit. Standard
packs accept at most normal-sized items, so large rifles use suit storage when stowed; spare grid
space does not override item-size limits or faction armor's native storage restrictions.

## Weapons and retention

- Loaded rifles, SMGs, shotguns and machine guns remain preferred at ordinary room distances.
  The sniper may draw its sidearm inside two metres. An empty primary can still yield to a loaded
  backup immediately; after contact expires, the preferred reloadable primary is drawn again so
  the normal reload behavior can restore it before the next engagement.
- Native full auto is selected only when the gun supports it. AI volleys still limit rounds;
  chambering, recoil, skills, native fire delays and finite ammunition remain in force.
- Visible contested fights use longer finite automatic volleys: up to 18 rounds in the open
  and ten from a held angle, followed by a short reassessment. Distant unthreatened targets keep
  ordinary bursts. Low ammunition and semi-automatic weapons retain their ordinary volley sizes.
  Tracking a visible opponent through a burst pause no longer adds a second aim delay; a recently
  seen opponent reappearing at the angle can interrupt that pause. Lost sight still stops entity
  targeting, and the existing brief muzzle-flash response only uses its observed fixed position.
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

Rocketeers spawn with one preloaded disposable RPG-36 in the human `suitstorage` slot, alongside
their SMG and stored pistol. The rocket is inside the tube; there are no loose spare rockets.
They prioritize a visible hostile armed vehicle while carrying a loaded tube. Their expedition
tube fires native anti-tank ammunition; penetration, vehicle damage and cook-off use native rules.
Launch range and blast checks use the first hull impact instead of the centre of a large vehicle.
A wall or another body before the hull blocks the launch. Shots require a safe backblast and friendly
blast clearance; the squad rocket cooldown remains 20 seconds. A blocked rocketeer can search a
short, reachable launch position within roughly three metres, without abandoning a covering commitment
or chasing an unseen vehicle. Rifles can engage hulls but retain their native armor effectiveness.

A currently visible shooter who recently wounded the rocketeer or a nearby squadmate is also a
rocket opportunity. Infantry opportunities use the same bounded launch-position search and squad
lane-clearing as vehicles. Launches still require the native six-metre maximum impact distance,
minimum separation, clear backblast, friendly blast clearance and the existing squad cooldown.

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
with a shared 30-second cooldown after a successful throw. Damage, a close rush and blocked
throws interrupt handling. Safe movement can continue during preparation; the current throw lane
and range are rechecked. The landing area rejects overlapping lit flares and friendly throws in
preparation or flight, even when the distant objective itself is still dark. Walls separate coverage,
and a flare near the end of its fade can be replaced. Flares remain finite inventory items.

The active gun can fire during flare or grenade preparation when native one-handed use permits it.
Cover moves, flanks and grenade approaches keep the rifle ready. Actual grenade handling retains its
movement/damage interruption and hand requirements; two-handed-only guns cannot bypass their grip.
Cancelled hand items retain cleanup ownership if native storage/drop is temporarily blocked.

## Resupply and communication

Belts, both pocket pouches, armor storage and backpacks are searched in that order for storage and consumption.
Directly equipped guns in any inventory slot are considered alongside held and stored weapons.
Quiet, unordered agents collect useful reserves: six spare magazines per carried gun or 24 shells,
two HE and two smoke grenades, eight fresh flares and one stored dressing. Packed flares count
toward reserves; a replacement pack is collected only with two or fewer flares remaining. Sharing
loose flares or surplus packs retains at least two flares for the donor. Capacity and whitelists
may stop collection earlier; these are targets, not guaranteed inventory sizes. A full shotgun still
recognizes compatible reserve shells. No ammunition or grenade is created by resupply behavior.

Nearby accessible bags, dead bodies and crates can supply items. When all carried loaded guns and
compatible reserves are exhausted, critical bodies can also supply useful weapons and ammunition.
Recovery from critical condition cancels that permission. Healthy inventories remain excluded;
opening crates uses native lock, weld and access checks. Retrieval uses an adjacent reachable
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

Reload response: magazine handling defaults to 0.8 seconds, and shell handling to at least
0.3 seconds or the weapon's native insert delay, whichever is longer. Empty weapons can request
rearming before the optional planning tick; an interrupted reload retries after 0.75 seconds
instead of the generic eight-second action penalty. Existing safety and interruption checks
still apply. A shotgun stops topping off once it has a live round and a visible enemy. Successful
reloads immediately ready the rifle and resume a safe known target without a second aim delay;
native weapon readiness, ammunition and fire-rate checks remain active.

Held ranged corners can use a recent, locally observed position for a grenade attempt.
The position is frozen in map coordinates and expires six seconds after observation;
radio reports alone cannot authorize this throw. When the direct throw lane is blocked,
the agent searches stances within three metres with a real clear throw lane, a ground
route and no increase in sampled exposure. A wholly sheltered route can proceed without
a shooter; an exposed route requires actual covering fire through approach and preparation.
The action has a five-second bound and rechecks contact, route, support and explosive safety.
If no suitable HE stance exists, a reachable landing on the near side can provide smoke.
Melee contacts do not trigger this obscuring fallback. The grenade is primed only after
the native throw succeeds; collision, friction and moving bodies still determine its real
landing. The planner does not bend trajectories around walls or assume a ricochet.

Manual verification (not run locally): empty a rifle and shotgun with compatible reserves,
with and without contact. Check actual magazine/shell consumption, full quiet top-offs,
single-shell combat reloads, prompt resumed fire, and recovery after damage, lost covering
fire, occupied hands, a rush and an explicit order. Native insert delays must still be honored.

Quiet loadout readiness selects reloadable empty carried guns, including stored backups, and partial
tubes for finite native reloads, then restores normal weapon preference. Ammunition in directly equipped
pockets or a hand is recognized. Full tubes do not start another insertion. Native pump weapons are
cycled after firing; legacy open/empty chambers are handled conditionally, without invoking the RMC
chamber's unrelated unique action. A live RMC reserve-chamber round counts as usable ammunition.

All fourteen kits carry a finite M5 bayonet. An exhausted soldier threatened by a nearby faster
pursuer, a fresh failed escape, or an attacker already landing contact hits can stop fleeing and
select its best carried melee weapon. The final closing step stays within 1.5 metres of that decision
and respects Hold, terrain and known danger. Native attack range, cooldown and obstruction still apply.
Gaining ammunition or losing the nearby threat releases the knife hand and restores normal behavior.

Self-treatment owns its hands before native unwielding finishes deleting the virtual grip.
Preparation waits up to 0.6 seconds for a free hand and retains that ownership through the native
healing callback, preventing weapon readiness from re-wielding between preparation and the dose.
Blocked preparation releases the claim and backs off for one second. Native damage events marked
non-interrupting, such as ongoing wound damage, do not count as fresh attacks. Actual hits, immediate
hazards, loss of safe shelter and explicit orders can still interrupt treatment; a sound report alone
cannot. Completion applies and consumes the actual dressing through the existing healing system.

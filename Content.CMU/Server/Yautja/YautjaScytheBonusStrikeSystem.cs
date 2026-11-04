using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared.Popups;
using Content.Shared.Effects;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaScytheBonusStrikeSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedColorFlashEffectSystem _colorFlash = default!;

    // CMU14: CM-SS13 (yaut_weapons.dm:473-480) lands the extra strike with a plain `..()` re-entry, which returns to
    // the PARENT attack proc and therefore cannot roll again. The SS14 follow-up is a real attack through the melee
    // pipeline, so that hit's own MeleeHitEvent reaches this handler; this set marks the swing as already a follow-up
    // so it never rolls a third hit.
    private readonly HashSet<EntityUid> _landingFollowUp = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaScytheBonusStrikeComponent, MeleeHitEvent>(
            OnScytheMeleeHit,
            after: [typeof(SharedRMCMeleeWeaponSystem)]);
    }

    private void OnScytheMeleeHit(Entity<YautjaScytheBonusStrikeComponent> ent, ref MeleeHitEvent args)
    {
    if (_landingFollowUp.Contains(args.User))
        return;

    if (!args.IsHit ||
        args.HitEntities.Count == 0 ||
        ent.Comp.Chance <= 0f ||
        !_random.Prob(ent.Comp.Chance))
    {
        return;
    }

        if (!_landingFollowUp.Add(args.User))
            return;

        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-scythe-bonus-strike-others"),
            args.User,
            Filter.PvsExcept(args.User),
            true,
            PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-scythe-bonus-strike-self"), args.User, args.User, PopupType.MediumCaution);

        // CMU14: the original hit keeps its normal damage - the bonus is a second ordinary hit, not doubled damage.
        // CM-SS13 re-enters the attack immediately in the same proc (`..()`), so this is scheduled with no delay; one
        // tick is the closest an SS14 attack can get to that synchronous re-entry.
        var user = args.User;
        var weapon = ent.Owner;
        var target = args.HitEntities[0];

        var delay = ent.Comp.FollowUpDelay;
        Timer.Spawn(delay, () =>
        {
            try
            {
                // The target may have died, been deleted or moved out of reach during the tick; AttemptLightAttack
                // re-validates reach and weapon state, so only a missing entity has to be checked here.
                if (TerminatingOrDeleted(user) || TerminatingOrDeleted(weapon) || TerminatingOrDeleted(target))
                    return;

                if (!TryComp(weapon, out MeleeWeaponComponent? melee))
                    return;

                // The swing that rolled this follow-up set BOTH melee cooldowns, and AttemptAttack refuses while
                // either is in the future: the user's RMCMeleeUserCooldownComponent (its very first gate, via
                // SyncUserMeleeCooldown) and the weapon's own MeleeWeaponComponent.NextAttack. Clearing only the
                // weapon's left the user cooldown rejecting the strike, which is why it never landed. CM-SS13's ..()
                // re-entry never consults a cooldown at all.
                var now = _timing.CurTime;
                if (melee.NextAttack > now)
                {
                    melee.NextAttack = now;
                    Dirty(weapon, melee);
                }

                if (TryComp(user, out RMCMeleeUserCooldownComponent? userCooldown) &&
                    userCooldown.NextAttack > now)
                {
                    userCooldown.NextAttack = now;
                    Dirty(user, userCooldown);
                }

                // The follow-up is an ordinary light attack, not bonus damage folded into the first swing.
                var landed = _melee.AttemptLightAttack(user, weapon, melee, target, requireCombatMode: false);

                // Its hit sound and flash are played client-side from the predicted attack, which a server-scheduled
                // strike never reaches, so raise both here: the weapon's own soundHit and the melee colour helper's
                // default red.
                if (landed)
                {
                    if (melee.HitSound is { } hitSound)
                        _audio.PlayPvs(hitSound, target);

                    _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { target }, Filter.Pvs(target));
                }
            }
            finally
            {
                _landingFollowUp.Remove(user);
            }
        });
    }
}

using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaScytheBonusStrikeSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;

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

        Timer.Spawn(TimeSpan.Zero, () =>
        {
            try
            {
                // The target may have died, been deleted or moved out of reach during the tick; AttemptLightAttack
                // re-validates reach and weapon state, so only a missing entity has to be checked here.
                if (TerminatingOrDeleted(user) || TerminatingOrDeleted(weapon) || TerminatingOrDeleted(target))
                    return;

                if (!TryComp(weapon, out MeleeWeaponComponent? melee))
                    return;

                // The original swing already passed the combat-mode gate, and CM-SS13 re-enters the parent attack
                // without re-checking intent, so the follow-up must not require combat mode again.
                _melee.AttemptLightAttack(user, weapon, melee, target, requireCombatMode: false);
            }
            finally
            {
                _landingFollowUp.Remove(user);
            }
        });
    }
}

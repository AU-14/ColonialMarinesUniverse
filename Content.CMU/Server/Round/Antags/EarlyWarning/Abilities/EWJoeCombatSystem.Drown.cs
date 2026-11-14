using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;

// COUPLING-03: Drown, its do-after and the drowning messages. Subscriptions stay in EWJoeCombatSystem.cs.
public sealed partial class EWJoeCombatSystem
{
    #region Drown

    private void OnDrown(Entity<EWJoeComponent> ent, ref EWJoeDrownActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;

        // W7 1B Overpressure: on xenofauna (no lungs to fill) Drown is a quick burst, no hold and no grab,
        // on Drown's own cooldown. Without the kit it refuses as before.
        if (HasComp<EWJoeOverpressureComponent>(ent) && IsXenofauna(target))
        {
            if (target == ent.Owner || !_mobState.IsAlive(target))
                return;

            args.Handled = true;
            Overpressure(ent, target);
            _liturgy.AbilityLanded(ent, target);
            return;
        }

        if (!CanDrown(ent, target, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), target, ent, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.DrownDelay, new EWJoeDrownDoAfterEvent(), ent, target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            DistanceThreshold = 1.5f,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-drown-start", ("target", target)), target, ent);

        // text pass A: the victim feels it begin; the rest comes every other second of the hold
        _messages.Sense(target, Loc.GetString("cmu-ew-joe-drown-victim-start"));
        var drowning = EnsureComp<EWJoeDrowningComponent>(target);
        drowning.Joe = ent;
        drowning.Beat = 0;
        drowning.Until = _timing.CurTime + ent.Comp.DrownDelay + TimeSpan.FromSeconds(2);
    }

    private void OnDrownDoAfter(Entity<EWJoeComponent> ent, ref EWJoeDrownDoAfterEvent args)
    {
        // the hold is over, finished or broken
        if (args.Args.Target is { } held)
            RemComp<EWJoeDrowningComponent>(held);

        if (args.Handled || args.Cancelled || args.Args.Target is not { } target)
            return;

        args.Handled = true;

        if (!CanDrown(ent, target, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), target, ent, PopupType.SmallCaution);
            return;
        }

        Drown(ent, target);
        _liturgy.AbilityLanded(ent, target);
    }

    /// <summary>
    /// Text pass A: every other second of a hold the victim's airway fills a little more, and twice the
    /// signal tells them to relax. A marker whose hold ended without its event is dropped.
    /// </summary>
    private void TellDrowning(TimeSpan now)
    {
        var stale = new List<EntityUid>();
        var query = EntityQueryEnumerator<EWJoeDrowningComponent>();
        while (query.MoveNext(out var victim, out var drowning))
        {
            if (now >= drowning.Until)
            {
                stale.Add(victim);
                continue;
            }

            drowning.Beat++;
            if (drowning.Beat % 2 == 0)
                _messages.Sense(victim, _messages.Pick(EWJoeMessageSystem.DrownAirway));
            if (drowning.Beat == 3 || drowning.Beat == 6)
                _messages.Whisper(victim, _messages.Pick(EWJoeMessageSystem.DrownVoice));
        }

        foreach (var victim in stale)
        {
            RemComp<EWJoeDrowningComponent>(victim);
        }
    }

    /// <summary>A living organic humanoid in the Joe's aggressive grab.</summary>
    public bool CanDrown(Entity<EWJoeComponent> joe, EntityUid target, out string reason)
    {
        reason = "cmu-ew-joe-drown-invalid";
        if (!Exists(target) || !_mobState.IsAlive(target))
            return false;

        if (HasComp<SynthComponent>(target))
        {
            reason = "cmu-ew-joe-drown-synthetic";
            return false;
        }

        // ABIL-10: the one rule for who is a person to the Joe (no yautja, threats or drones)
        if (!EWJoeTargetRules.IsOrganic(EntityManager, target))
            return false;

        if (!IsGrabbedBy(joe, target))
        {
            reason = "cmu-ew-joe-needs-grab";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Balance A-5: lungs to the dead stage (asphyxiation that keeps ticking), 30 s of sleep that cannot
    /// be shaken off, the victim counts as dead for objectives, and the Joe heals a share of its own
    /// dead threshold. Public so tests can skip the do-after.
    /// </summary>
    public void Drown(Entity<EWJoeComponent> joe, EntityUid target)
    {
        _messages.Sense(target, Loc.GetString("cmu-ew-joe-drown-victim-end"));
        _medical.DamageOrgan<LungsComponent>(target, joe.Comp.DrownLungDamage, Asphyxiation);
        _status.TryAddStatusEffectDuration(target, ForcedSleeping, joe.Comp.DrownSleep);

        // dead for objectives only while the sleep lasts; the sweep below lifts it again
        var drowned = EnsureComp<EWJoeDrownedComponent>(target);
        drowned.Until = _timing.CurTime + joe.Comp.DrownSleep;
        if (!HasComp<IsDeadICComponent>(target))
        {
            EnsureComp<IsDeadICComponent>(target);
            drowned.AddedDeadIc = true;
        }

        // HealEvenly heals by a negative amount
        if (_thresholds.TryGetDeadThreshold(joe, out var dead))
            _damageable.HealEvenly(joe.Owner, -(dead.Value * joe.Comp.DrownHealFraction), origin: joe);

        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-drown-done", ("target", target)), target, joe);
        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"Early Warning Joe {ToPrettyString(joe):joe} drowned {ToPrettyString(target):target}");
    }

    #endregion
}

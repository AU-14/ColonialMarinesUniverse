using System.Numerics;
using Content.Shared._RMC14.Entrenching;
using Content.Shared.Actions.Components;
using Content.Shared.Climbing.Components;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Combat;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Wall;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;

// COUPLING-03: the Sculpted Hands moves. The action handlers only check and report; the effects are
// public (Sweep, Slam, StartChoke/StopChoke, Throw) so other systems can drive them directly.
// Subscriptions stay in EWJoeCombatSystem.cs.
public sealed partial class EWJoeCombatSystem
{
    #region Sculpted Hands

    private void OnSweep(Entity<EWJoeComponent> ent, ref EWJoeSweepActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        if (!_mobState.IsAlive(target))
            return;

        args.Handled = true;

        // ARCH-05: Exalted lengthens the knockdown, unless the target is inside its CC-immunity window (FLOW-06)
        TimeSpan? duration = null;
        if (_liturgy.TryConsumeEmpowered(ent, target, ccBonus: true, "Sweep") && TryComp(ent, out EWJoeLiturgyComponent? liturgy))
            duration = ent.Comp.SweepKnockdown * liturgy.EmpoweredSweepMultiplier;

        Sweep(ent, target, duration);
        _liturgy.AbilityLanded(ent, target, hardCc: true);
    }

    /// <summary>
    /// Leg Sweep: knocks <paramref name="target"/> down for <paramref name="duration"/> (the Joe's
    /// <see cref="EWJoeComponent.SweepKnockdown"/> when null). No grab and no liveness check; callers
    /// that need them check first.
    /// </summary>
    public void Sweep(Entity<EWJoeComponent> joe, EntityUid target, TimeSpan? duration = null)
    {
        // ECS-11: xenofauna get the Joe's stun multiplier before RMC's own reduction
        var knockdown = EWJoeTargetRules.ScaleStun(EntityManager, target, duration ?? joe.Comp.SweepKnockdown, joe.Comp.XenofaunaStunMultiplier);
        _stun.TryKnockdown(target, knockdown);
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-sweep-done", ("target", target)), target, joe);
    }

    /// <summary>The action targets the obstacle; the victim is whoever the Joe is holding.</summary>
    private void OnSlam(Entity<EWJoeComponent> ent, ref EWJoeSlamActionEvent args)
    {
        if (args.Handled)
            return;

        var obstacle = args.Target;
        if (!IsSlamObstacle(obstacle))
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-slam-no-obstacle"), ent, ent, PopupType.SmallCaution);
            return;
        }

        if (!TryGetGrabbed(ent, out var victim))
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-needs-grab"), ent, ent, PopupType.SmallCaution);
            return;
        }

        var victimPos = _transform.GetMapCoordinates(victim);
        var obstaclePos = _transform.GetMapCoordinates(obstacle);
        if (victimPos.MapId != obstaclePos.MapId || (obstaclePos.Position - victimPos.Position).Length() > ent.Comp.SlamRange)
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-slam-too-far"), ent, ent, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;
        var empowered = _liturgy.TryConsumeEmpowered(ent, victim, ccBonus: true, "Slam");
        Slam(ent, victim, obstacle, empowered);
        _liturgy.AbilityLanded(ent, victim, hardCc: true);
    }

    /// <summary>Walls, tables, railings, barricades: anything the RMC obstacle-slam impact treats as hard.</summary>
    public bool IsSlamObstacle(EntityUid uid)
    {
        return Exists(uid)
            && (HasComp<WallComponent>(uid) || HasComp<ClimbableComponent>(uid) || HasComp<BarricadeComponent>(uid) || _tag.HasTag(uid, WallTag));
    }

    /// <summary>
    /// Releases the grab and throws the victim into the obstacle. RMC's obstacle slamming turns the
    /// impact into speed-scaled damage, knockback and the metal slam; the Joe adds the flat A-7 damage
    /// and arms the paralyze/slow bonus that fires on the hit. <paramref name="empowered"/> (ARCH-05, an Exalted
    /// Joe) adds the Liturgy's damage and paralyze multipliers.
    /// </summary>
    public void Slam(Entity<EWJoeComponent> joe, EntityUid victim, EntityUid obstacle, bool empowered = false)
    {
        StopChoke(joe, "slammed");
        _rmcPulling.TryStopUserPullIfPulling(joe, victim);

        var paralyze = joe.Comp.SlamParalyze;
        var amount = (float) joe.Comp.SlamDamage;
        if (empowered && TryComp(joe, out EWJoeLiturgyComponent? liturgy))
        {
            paralyze *= liturgy.EmpoweredSlamParalyzeMultiplier;
            amount *= liturgy.EmpoweredSlamDamageMultiplier;
        }

        // ECS-11: xenofauna get the Joe's stun multiplier before RMC's own reduction
        paralyze = EWJoeTargetRules.ScaleStun(EntityManager, victim, paralyze, joe.Comp.XenofaunaStunMultiplier);

        // the control effect must not depend on the physics contact: a victim already knocked down
        // (Leg Sweep, the grab itself) has no MidImpassable in its mask and slides over tables and
        // railings without a ThrowDoHitEvent, so paralyze and slow are applied here and the RMC
        // bonus only stacks on a real impact
        _stun.TryParalyze(victim, paralyze, true);
        _slow.TrySlowdown(victim, joe.Comp.SlamSlow);
        _obstacleSlam.ApplyBonuses(victim, paralyze, joe.Comp.SlamSlow);
        var damage = new DamageSpecifier(_proto.Index(Blunt), amount);
        _damageable.TryChangeDamage(victim, damage, origin: joe);
        // W7 2A Chitin Crack: the impact cracks a xeno's carapace (no change against humans)
        TryChitinCrack(joe, victim);

        var offset = _transform.GetMapCoordinates(obstacle).Position - _transform.GetMapCoordinates(victim).Position;
        var direction = offset.LengthSquared() < 0.01f ? _transform.GetWorldRotation(joe).ToWorldVec() : offset.Normalized();
        var distance = Math.Max(1f, offset.Length() + 0.5f);
        // uncompensated, like RMC's own slams: the impact speed is the nominal one and the obstacle stops the body
        _throwing.TryThrow(victim, direction * distance, joe.Comp.SlamThrowSpeed, user: joe, compensateFriction: false);

        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-slam-done", ("target", victim), ("obstacle", obstacle)), victim, joe, PopupType.MediumCaution);
    }

    private void OnChoke(Entity<EWJoeComponent> ent, ref EWJoeChokeActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (HasComp<EWJoeChokeComponent>(ent))
        {
            StopChoke(ent, "released");
            return;
        }

        if (!TryGetGrabbed(ent, out var target))
        {
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-needs-grab"), ent, ent, PopupType.SmallCaution);
            return;
        }

        BeginChoke(ent, target, args.Action);
        _liturgy.AbilityLanded(ent, target);
    }

    /// <summary>
    /// Starts (or retargets) the choke on <paramref name="target"/>, which must be in the Joe's
    /// aggressive grab. Stamina drains every second in <see cref="Update"/> until the grip breaks or
    /// <see cref="StopChoke"/> is called; on xenofauna with the Pin kit (W7 2B) it is a <see cref="Pin"/>
    /// instead. No popup on refusal; returns false when there is no grab.
    /// </summary>
    public bool StartChoke(Entity<EWJoeComponent> joe, EntityUid target)
    {
        if (!IsGrabbedBy(joe, target))
            return false;

        BeginChoke(joe, target, null);
        return true;
    }

    /// <summary>The choke-start body shared by the action and <see cref="StartChoke"/>.</summary>
    private void BeginChoke(Entity<EWJoeComponent> joe, EntityUid target, Entity<ActionComponent>? action)
    {
        var choke = EnsureComp<EWJoeChokeComponent>(joe);
        choke.StruggleExtended = false;
        choke.Target = target;
        // ABIL-05: the wheel shows a running choke as active
        if (action is { } act && Exists(act))
        {
            choke.Action = act;
            _actions.SetToggled(new Entity<ActionComponent?>(act, act.Comp), true);
        }

        // W7 2B: with the Pin kit, a choke on xenofauna is a pin (root and damage over time)
        if (HasComp<EWJoePinComponent>(joe) && IsXenofauna(target))
        {
            Pin(joe, target);
            return;
        }

        RemComp<EWJoePinningComponent>(joe);
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-choke-start", ("target", target)), target, joe, PopupType.MediumCaution);
    }

    public void StopChoke(EntityUid joe, string why)
    {
        if (!TryComp(joe, out EWJoeChokeComponent? choke))
            return;

        if (choke.Action is { } action)
            _actions.SetToggled(new Entity<ActionComponent?>(action, null), false);

        RemComp<EWJoeChokeComponent>(joe);
        RemComp<EWJoePinningComponent>(joe);
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-choke-stop"), joe, joe);
    }

    private void OnThrow(Entity<EWJoeComponent> ent, ref EWJoeThrowActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        if (!RequireGrab(ent, target))
            return;

        args.Handled = true;
        var empowered = _liturgy.TryConsumeEmpowered(ent, target, ccBonus: true, "Throw");
        Throw(ent, target, empowered);
        _liturgy.AbilityLanded(ent, target);
    }

    /// <summary>
    /// Releases the grab and throws the target along the Joe's facing. <paramref name="empowered"/> (ARCH-05)
    /// always arms the RMC obstacle slam, so whatever the body hits paralyzes and slows it.
    /// </summary>
    public void Throw(Entity<EWJoeComponent> joe, EntityUid target, bool empowered = false)
    {
        StopChoke(joe, "thrown");
        _rmcPulling.TryStopUserPullIfPulling(joe, target);

        var direction = _transform.GetWorldRotation(joe).ToWorldVec();
        if (direction.LengthSquared() < 0.01f)
            direction = Vector2.UnitY;

        _throwing.TryThrow(target, direction.Normalized() * joe.Comp.ThrowDistance, joe.Comp.ThrowSpeed, user: joe, compensateFriction: true);
        if (empowered && TryComp(joe, out EWJoeLiturgyComponent? liturgy))
        {
            // ECS-11: xenofauna get the Joe's stun multiplier before RMC's own reduction
            var paralyze = EWJoeTargetRules.ScaleStun(EntityManager, target, liturgy.EmpoweredThrowSlamParalyze, joe.Comp.XenofaunaStunMultiplier);
            _obstacleSlam.ApplyBonuses(target, paralyze, liturgy.EmpoweredThrowSlamSlow);
        }

        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-throw-done", ("target", target)), target, joe, PopupType.MediumCaution);
    }

    #endregion
}

using Content.Server.CMU14.Combat.CQC;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared._RMC14.Stun;
using Content.Shared.CMU14.Combat.CQC;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Combat;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;
using Content.Shared.Examine;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Combat;

/// <summary>
/// ARCH-06 (node N13): the Early Warning effects the CQC sequences call, by id, registered into
/// <see cref="CMUCqcSystem"/>'s registry (Sweep, Desecrate, Slam, ChokeOpen, Unmaking; Grip is generic). Every effect
/// goes through the same public method the matching hotbar ability uses (COUPLING-03), so a sequence and the hotbar
/// cannot drift apart, and each keeps its xenofauna branch. An effect returns false when it could not land at all,
/// or when the user is not an Early Warning Joe; the sequence then keeps its steps. <c>empowered</c> (ARCH-12) is true
/// when an Exalted Joe fires a sequence whose effect has an empowered branch (Sweep, Desecrate, Slam); the other
/// effects have no empowered branch and ignore it.
/// </summary>
public sealed class EWJoeCqcEffectsSystem : EntitySystem
{
    [Dependency] private EWJoeCombatSystem _combat = default!;
    [Dependency] private CMUCqcSystem _cqc = default!;
    [Dependency] private RMCDazedSystem _dazed = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _obstacles = new();
    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        base.Initialize();

        // the effects with an empowered branch: only these spend an Exalted Joe's empower
        _cqc.RegisterEffect("Sweep", Sweep, usesEmpowered: true);
        _cqc.RegisterEffect("Desecrate", Desecrate, usesEmpowered: true);
        _cqc.RegisterEffect("Slam", Slam, usesEmpowered: true);
        _cqc.RegisterEffect("ChokeOpen", ChokeOpen);
        _cqc.RegisterEffect("Unmaking", Unmaking);
    }

    /// <summary>
    /// Rebuke: the Leg Sweep, with the sequence's own knockdown; empowered, it is lengthened like the hotbar's.
    /// The xenofauna stun scaling happens inside <see cref="EWJoeCombatSystem.Sweep"/>.
    /// </summary>
    private bool Sweep(EntityUid user, EntityUid target, CMUCqcSequencePrototype sequence, bool empowered)
    {
        if (!TryComp(user, out EWJoeComponent? comp) || !_mobState.IsAlive(target))
            return false;

        var joe = new Entity<EWJoeComponent>(user, comp);
        var duration = sequence.Duration;
        if (empowered && TryComp(joe, out EWJoeLiturgyComponent? liturgy))
            duration = (duration ?? joe.Comp.SweepKnockdown) * liturgy.EmpoweredSweepMultiplier;

        _combat.Sweep(joe, target, duration);
        return true;
    }

    /// <summary>Profane: Desecrate, empowered when the sequence says so or the Joe is Exalted.</summary>
    private bool Desecrate(EntityUid user, EntityUid target, CMUCqcSequencePrototype sequence, bool empowered)
    {
        if (!TryComp(user, out EWJoeComponent? comp) || target == user || !_mobState.IsAlive(target))
            return false;

        _combat.Desecrate((user, comp), target, sequence.Empowered || empowered);
        return true;
    }

    /// <summary>
    /// Penance: the Slam, into the nearest wall, table, railing or barricade within the slam range of the target;
    /// empowered, with the Liturgy's damage and paralyze multipliers.
    /// </summary>
    private bool Slam(EntityUid user, EntityUid target, CMUCqcSequencePrototype sequence, bool empowered)
    {
        if (!TryComp(user, out EWJoeComponent? comp) || !_mobState.IsAlive(target))
            return false;

        var joe = new Entity<EWJoeComponent>(user, comp);
        var origin = _transform.GetMapCoordinates(target);
        _obstacles.Clear();
        _lookup.GetEntitiesInRange(target, joe.Comp.SlamRange, _obstacles);

        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        foreach (var obstacle in _obstacles)
        {
            if (obstacle == target || obstacle == joe.Owner || !_combat.IsSlamObstacle(obstacle))
                continue;

            var position = _transform.GetMapCoordinates(obstacle);
            if (position.MapId != origin.MapId)
                continue;

            var distance = (position.Position - origin.Position).LengthSquared();
            if (distance >= bestDistance)
                continue;

            best = obstacle;
            bestDistance = distance;
        }

        if (best is not { } wall)
            return false;

        _combat.Slam(joe, target, wall, empowered);
        return true;
    }

    /// <summary>Silence: the choke (or the Pin on xenofauna), on the body already held.</summary>
    private bool ChokeOpen(EntityUid user, EntityUid target, CMUCqcSequencePrototype sequence, bool empowered)
    {
        return TryComp(user, out EWJoeComponent? comp) && _combat.StartChoke((user, comp), target);
    }

    /// <summary>
    /// Unmaking: a Saturate-lite. Everyone alive in a short cone towards the target and in the Joe's line of sight
    /// is dazed, and the target is knocked down. River-Melded thralls are spared, as Saturate spares them. Nothing
    /// is stripped. The knockdown is scaled for xenofauna (ECS-11) and goes through the CC window (RB-13).
    /// </summary>
    private bool Unmaking(EntityUid user, EntityUid target, CMUCqcSequencePrototype sequence, bool empowered)
    {
        if (!HasComp<EWJoeComponent>(user) || !_mobState.IsAlive(target))
            return false;

        var origin = _transform.GetMapCoordinates(user);
        var targetPos = _transform.GetMapCoordinates(target);
        var toTarget = targetPos.MapId == origin.MapId ? targetPos.Position - origin.Position : System.Numerics.Vector2.Zero;
        var facing = toTarget.LengthSquared() < 0.01f ? _transform.GetWorldRotation(user) : toTarget.ToWorldAngle();
        var halfAngle = Angle.FromDegrees(sequence.ConeDegrees / 2f);

        _mobs.Clear();
        _lookup.GetEntitiesInRange(origin, sequence.Range, _mobs, LookupFlags.Uncontained);

        foreach (var mob in _mobs)
        {
            if (mob.Owner == user || !_mobState.IsAlive(mob, mob.Comp) || EWJoeTargetRules.IsMachineAlly(EntityManager, mob))
                continue;

            var offset = _transform.GetMapCoordinates(mob).Position - origin.Position;
            if (mob.Owner != target && !EWJoeTargetRules.InCone(offset, facing, halfAngle))
                continue;

            if (!_examine.InRangeUnOccluded(user, mob.Owner, sequence.Range + 0.5f))
                continue;

            _dazed.TryDaze(mob, sequence.DazeDuration, refresh: true);
        }

        // the target is always caught, even standing just outside the reach
        _dazed.TryDaze(target, sequence.DazeDuration, refresh: true);
        // RB-13: the knockdown is hard CC, scaled for xenofauna (ECS-11) and for the target's window
        _cqc.ApplyHardCc(user, target, sequence.Duration ?? TimeSpan.FromSeconds(1), CMUHardCcKind.Knockdown);
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-liturgy-unmaking", ("target", target)), target, PopupType.LargeCaution);
        return true;
    }
}

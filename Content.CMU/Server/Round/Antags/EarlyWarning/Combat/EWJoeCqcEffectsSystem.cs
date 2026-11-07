using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Stun;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Combat;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;
using Content.Shared.Examine;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Combat;

/// <summary>
/// ARCH-06 (node N13): the effect registry the CQC sequences call, by id. Every effect goes through the same
/// public method the matching hotbar ability uses (COUPLING-03), so a sequence and the hotbar cannot drift
/// apart, and each keeps its xenofauna branch. An effect returns false when it could not land at all; the
/// sequence then keeps its steps. <c>empowered</c> (ARCH-12) is true when an Exalted Joe fires a sequence whose
/// effect has an empowered branch (Sweep, Desecrate, Slam); the other effects have no empowered branch and ignore it.
/// </summary>
public sealed class EWJoeCqcEffectsSystem : EntitySystem
{
    [Dependency] private EWJoeCombatSystem _combat = default!;
    [Dependency] private RMCDazedSystem _dazed = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private FiremanCarrySystem _fireman = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public delegate bool CqcEffect(Entity<EWJoeComponent> joe, EntityUid target, EWJoeCqcSequencePrototype sequence, bool empowered);

    private readonly Dictionary<string, CqcEffect> _effects = new();

    /// <summary>The effects with an empowered branch: only these spend an Exalted Joe's empower.</summary>
    private readonly HashSet<string> _empowerable = new() { "Sweep", "Desecrate", "Slam" };
    private readonly HashSet<EntityUid> _obstacles = new();
    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        base.Initialize();

        _effects["Sweep"] = Sweep;
        _effects["Grip"] = Grip;
        _effects["Desecrate"] = Desecrate;
        _effects["Slam"] = Slam;
        _effects["ChokeOpen"] = ChokeOpen;
        _effects["Unmaking"] = Unmaking;
    }

    public bool Has(string effect)
    {
        return _effects.ContainsKey(effect);
    }

    public IEnumerable<string> Effects => _effects.Keys;

    /// <summary>Whether <paramref name="effect"/> has an empowered branch, so an Exalted Joe spends its empower on it.</summary>
    public bool UsesEmpowered(string effect)
    {
        return _empowerable.Contains(effect);
    }

    public bool TryApply(string effect, Entity<EWJoeComponent> joe, EntityUid target, EWJoeCqcSequencePrototype sequence, bool empowered = false)
    {
        return _effects.TryGetValue(effect, out var apply) && apply(joe, target, sequence, empowered);
    }

    /// <summary>
    /// Rebuke: the Leg Sweep, with the sequence's own knockdown; empowered, it is lengthened like the hotbar's.
    /// The xenofauna stun scaling happens inside <see cref="EWJoeCombatSystem.Sweep"/>.
    /// </summary>
    private bool Sweep(Entity<EWJoeComponent> joe, EntityUid target, EWJoeCqcSequencePrototype sequence, bool empowered)
    {
        if (!_mobState.IsAlive(target))
            return false;

        var duration = sequence.Duration;
        if (empowered && TryComp(joe, out EWJoeLiturgyComponent? liturgy))
            duration = (duration ?? joe.Comp.SweepKnockdown) * liturgy.EmpoweredSweepMultiplier;

        _combat.Sweep(joe, target, duration);
        return true;
    }

    /// <summary>Seize: the pull goes straight to an aggressive grab, skipping the Machine Grip's windup.</summary>
    private bool Grip(Entity<EWJoeComponent> joe, EntityUid target, EWJoeCqcSequencePrototype sequence, bool empowered)
    {
        return TryComp(joe, out CanFiremanCarryComponent? carry) && _fireman.TryAggressiveGrab((joe.Owner, carry), target);
    }

    /// <summary>Profane: Desecrate, empowered when the sequence says so or the Joe is Exalted.</summary>
    private bool Desecrate(Entity<EWJoeComponent> joe, EntityUid target, EWJoeCqcSequencePrototype sequence, bool empowered)
    {
        if (target == joe.Owner || !_mobState.IsAlive(target))
            return false;

        _combat.Desecrate(joe, target, sequence.Empowered || empowered);
        return true;
    }

    /// <summary>
    /// Penance: the Slam, into the nearest wall, table, railing or barricade within the slam range of the target;
    /// empowered, with the Liturgy's damage and paralyze multipliers.
    /// </summary>
    private bool Slam(Entity<EWJoeComponent> joe, EntityUid target, EWJoeCqcSequencePrototype sequence, bool empowered)
    {
        if (!_mobState.IsAlive(target))
            return false;

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
    private bool ChokeOpen(Entity<EWJoeComponent> joe, EntityUid target, EWJoeCqcSequencePrototype sequence, bool empowered)
    {
        return _combat.StartChoke(joe, target);
    }

    /// <summary>
    /// Unmaking: a Saturate-lite. Everyone alive in a short cone towards the target and in the Joe's line of sight
    /// is dazed, and the target is knocked down. River-Melded thralls are spared, as Saturate spares them. Nothing
    /// is stripped. The knockdown is scaled for xenofauna (ECS-11).
    /// </summary>
    private bool Unmaking(Entity<EWJoeComponent> joe, EntityUid target, EWJoeCqcSequencePrototype sequence, bool empowered)
    {
        if (!_mobState.IsAlive(target))
            return false;

        var origin = _transform.GetMapCoordinates(joe);
        var targetPos = _transform.GetMapCoordinates(target);
        var toTarget = targetPos.MapId == origin.MapId ? targetPos.Position - origin.Position : System.Numerics.Vector2.Zero;
        var facing = toTarget.LengthSquared() < 0.01f ? _transform.GetWorldRotation(joe) : toTarget.ToWorldAngle();
        var halfAngle = Angle.FromDegrees(sequence.ConeDegrees / 2f);

        _mobs.Clear();
        _lookup.GetEntitiesInRange(origin, sequence.Range, _mobs, LookupFlags.Uncontained);

        foreach (var mob in _mobs)
        {
            if (mob.Owner == joe.Owner || !_mobState.IsAlive(mob, mob.Comp) || HasComp<RiverMeldedComponent>(mob))
                continue;

            var offset = _transform.GetMapCoordinates(mob).Position - origin.Position;
            if (mob.Owner != target && !EWJoeTargetRules.InCone(offset, facing, halfAngle))
                continue;

            if (!_examine.InRangeUnOccluded(joe.Owner, mob.Owner, sequence.Range + 0.5f))
                continue;

            _dazed.TryDaze(mob, sequence.DazeDuration, refresh: true);
        }

        // the target is always caught, even standing just outside the reach
        _dazed.TryDaze(target, sequence.DazeDuration, refresh: true);
        var knockdown = EWJoeTargetRules.ScaleStun(EntityManager, target, sequence.Duration ?? TimeSpan.FromSeconds(1), joe.Comp.XenofaunaStunMultiplier);
        _stun.TryKnockdown(target, knockdown);
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-liturgy-unmaking", ("target", target)), target, PopupType.LargeCaution);
        return true;
    }
}

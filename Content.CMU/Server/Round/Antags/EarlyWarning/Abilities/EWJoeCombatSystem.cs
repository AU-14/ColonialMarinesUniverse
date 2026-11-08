using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.CMU14.Surrender;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Damage.ObstacleSlamming;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stamina;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.CMU14.Surrender;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Climbing.Components;
using Content.Shared.Wall;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Phase E4 close-quarters abilities: Drown (level 1) and the four Sculpted Hands moves plus
/// Judgement (level 2). Everything that needs a victim in the Joe's hands checks the RMC aggressive
/// grab: the Joe must be pulling the target and the grab must have reached the aggressive stage.
/// </summary>
public sealed partial class EWJoeCombatSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private EWJoeMessageSystem _messages = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private EWJoeAbilitySystem _ability = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private FiremanCarrySystem _fireman = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private CMUChemicalMedicalSystem _medical = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCPullingSystem _rmcPulling = default!;
    [Dependency] private RMCObstacleSlammingSystem _obstacleSlam = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private RMCStaminaSystem _stamina = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private CMUSurrenderSystem _surrender = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";
    private static readonly EntProtoId ForcedSleeping = "StatusEffectForcedSleeping";

    private readonly HashSet<Entity<MobStateComponent>> _targets = new();
    private TimeSpan _nextChokeTick;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeComponent, EWJoeDrownActionEvent>(OnDrown);
        SubscribeLocalEvent<EWJoeComponent, EWJoeDrownDoAfterEvent>(OnDrownDoAfter);
        SubscribeLocalEvent<EWJoeComponent, EWJoeSweepActionEvent>(OnSweep);
        SubscribeLocalEvent<EWJoeComponent, EWJoeSlamActionEvent>(OnSlam);
        SubscribeLocalEvent<EWJoeComponent, EWJoeChokeActionEvent>(OnChoke);
        SubscribeLocalEvent<EWJoeComponent, EWJoeThrowActionEvent>(OnThrow);
        SubscribeLocalEvent<EWJoeComponent, EWJoeJudgementActionEvent>(OnJudgement);
        SubscribeLocalEvent<EWJoeDropsSurrenderComponent, ActionPerformedEvent>(OnHostileActionPerformed);
        SubscribeLocalEvent<EWJoeRemovedEvent>(OnJoeRemoved);
    }

    /// <summary>A hostile ability used with raised hands ends the surrender.</summary>
    private void OnHostileActionPerformed(Entity<EWJoeDropsSurrenderComponent> ent, ref ActionPerformedEvent args)
    {
        if (HasComp<EWJoeComponent>(args.Performer) && HasComp<CMUSurrenderComponent>(args.Performer))
            _surrender.LowerHands(args.Performer);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextChokeTick)
            return;

        _nextChokeTick = now + TimeSpan.FromSeconds(1);

        // drownings wear off
        var recovered = new List<(EntityUid Victim, bool AddedDeadIc)>();
        var drownedQuery = EntityQueryEnumerator<EWJoeDrownedComponent>();
        while (drownedQuery.MoveNext(out var uid, out var drowned))
        {
            if (now >= drowned.Until)
                recovered.Add((uid, drowned.AddedDeadIc));
        }

        foreach (var (victim, addedDeadIc) in recovered)
        {
            if (addedDeadIc)
                RemComp<IsDeadICComponent>(victim);
            RemComp<EWJoeDrownedComponent>(victim);
        }

        TellDrowning(now);

        var chokes = new List<(EntityUid Joe, EWJoeChokeComponent Choke)>();
        var query = EntityQueryEnumerator<EWJoeChokeComponent>();
        while (query.MoveNext(out var uid, out var choke))
        {
            chokes.Add((uid, choke));
        }

        foreach (var (joe, choke) in chokes)
        {
            if (!TryComp(joe, out EWJoeComponent? comp) || !IsGrabbedBy((joe, comp), choke.Target) || !_mobState.IsAlive(choke.Target))
            {
                StopChoke(joe, "the grip broke");
                continue;
            }

            _stamina.DoStaminaDamage(choke.Target, comp.ChokeStaminaPerSecond);
        }
    }

    #region Grab

    /// <summary>The Joe is pulling <paramref name="target"/> and the grab is aggressive.</summary>
    public bool IsGrabbedBy(Entity<EWJoeComponent> joe, EntityUid target)
    {
        return TryComp(joe, out PullerComponent? puller)
            && puller.Pulling == target
            && _fireman.IsAggressivelyGrabbed(target);
    }

    private bool RequireGrab(Entity<EWJoeComponent> joe, EntityUid target)
    {
        if (IsGrabbedBy(joe, target))
            return true;

        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-needs-grab"), joe, joe, PopupType.SmallCaution);
        return false;
    }

    #endregion

    #region Drown

    private void OnDrown(Entity<EWJoeComponent> ent, ref EWJoeDrownActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
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

    /// <summary>The body the Joe holds in an aggressive grab, if any.</summary>
    private bool TryGetGrabbed(Entity<EWJoeComponent> joe, out EntityUid victim)
    {
        victim = default;
        if (!TryComp(joe, out PullerComponent? puller) || puller.Pulling is not { } pulled || !IsGrabbedBy(joe, pulled))
            return false;

        victim = pulled;
        return true;
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

    #region Sculpted Hands

    private void OnSweep(Entity<EWJoeComponent> ent, ref EWJoeSweepActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        if (!_mobState.IsAlive(target))
            return;

        args.Handled = true;
        _stun.TryKnockdown(target, ent.Comp.SweepKnockdown);
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-sweep-done", ("target", target)), target, ent);
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
        Slam(ent, victim, obstacle);
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
    /// and arms the paralyze/slow bonus that fires on the hit.
    /// </summary>
    public void Slam(Entity<EWJoeComponent> joe, EntityUid victim, EntityUid obstacle)
    {
        StopChoke(joe, "slammed");
        _rmcPulling.TryStopUserPullIfPulling(joe, victim);

        // the control effect must not depend on the physics contact: a victim already knocked down
        // (Leg Sweep, the grab itself) has no MidImpassable in its mask and slides over tables and
        // railings without a ThrowDoHitEvent, so paralyze and slow are applied here and the RMC
        // bonus only stacks on a real impact
        _stun.TryParalyze(victim, joe.Comp.SlamParalyze, true);
        _slow.TrySlowdown(victim, joe.Comp.SlamSlow);
        _obstacleSlam.ApplyBonuses(victim, joe.Comp.SlamParalyze, joe.Comp.SlamSlow);
        var damage = new DamageSpecifier(_proto.Index(Blunt), joe.Comp.SlamDamage);
        _damageable.TryChangeDamage(victim, damage, origin: joe);

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

        var choke = EnsureComp<EWJoeChokeComponent>(ent);
        choke.Target = target;
        // ABIL-05: the wheel shows a running choke as active
        if (Exists(args.Action))
        {
            choke.Action = args.Action;
            _actions.SetToggled(new Entity<ActionComponent?>(args.Action, args.Action.Comp), true);
        }
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-choke-start", ("target", target)), target, ent, PopupType.MediumCaution);
    }

    public void StopChoke(EntityUid joe, string why)
    {
        if (!TryComp(joe, out EWJoeChokeComponent? choke))
            return;

        if (choke.Action is { } action)
            _actions.SetToggled(new Entity<ActionComponent?>(action, null), false);

        RemComp<EWJoeChokeComponent>(joe);
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
        Throw(ent, target);
    }

    /// <summary>Releases the grab and throws the target along the Joe's facing.</summary>
    public void Throw(Entity<EWJoeComponent> joe, EntityUid target)
    {
        StopChoke(joe, "thrown");
        _rmcPulling.TryStopUserPullIfPulling(joe, target);

        var direction = _transform.GetWorldRotation(joe).ToWorldVec();
        if (direction.LengthSquared() < 0.01f)
            direction = Vector2.UnitY;

        _throwing.TryThrow(target, direction.Normalized() * joe.Comp.ThrowDistance, joe.Comp.ThrowSpeed, user: joe, compensateFriction: true);
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-throw-done", ("target", target)), target, joe, PopupType.MediumCaution);
    }

    #endregion

    #region Judgement

    private void OnJudgement(Entity<EWJoeComponent> ent, ref EWJoeJudgementActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var hit = Judge(ent);
        var message = hit == 0
            ? Loc.GetString("cmu-ew-joe-judgement-none")
            : Loc.GetString("cmu-ew-joe-judgement-hit", ("count", hit));
        _popup.PopupEntity(message, ent, ent);
    }

    /// <summary>
    /// Test of Faith as a cone (balance A-1): every living target within range, inside the cone around
    /// the Joe's facing and in line of sight is flashed. No mutual-facing test.
    /// </summary>
    public int Judge(Entity<EWJoeComponent> joe)
    {
        var origin = _transform.GetMapCoordinates(joe);
        var facing = _transform.GetWorldRotation(joe);
        var halfAngle = Angle.FromDegrees(joe.Comp.JudgementConeDegrees / 2f);
        var range = joe.Comp.JudgementRange;

        _targets.Clear();
        _lookup.GetEntitiesInRange(origin, range, _targets, LookupFlags.Uncontained);

        var hit = 0;
        foreach (var target in _targets)
        {
            if (target.Owner == joe.Owner || !_mobState.IsAlive(target, target.Comp))
                continue;

            var toTarget = _transform.GetMapCoordinates(target).Position - origin.Position;
            if (!EWJoeTargetRules.InCone(toTarget, facing, halfAngle))
                continue;

            if (!_examine.InRangeUnOccluded(joe, target, range))
                continue;

            if (_ability.TryFlash(joe, target, judgement: true))
                hit++;
        }

        return hit;
    }

    #endregion

    private void OnJoeRemoved(ref EWJoeRemovedEvent args)
    {
        StopChoke(args.Joe, "stood down");
    }
}

using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Combat;
using Content.Server.CMU14.Surrender;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Damage.ObstacleSlamming;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Stamina;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
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
using Content.Shared.Weapons.Melee.Events;
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
/// <remarks>
/// COUPLING-03: split into partials. This file holds the dependencies, every subscription
/// (<see cref="Initialize"/>), the tick and the grab helpers. The effects live in
/// <c>EWJoeCombatSystem.Hands.cs</c> (Sweep, Slam, Choke, Throw), <c>EWJoeCombatSystem.Drown.cs</c>
/// and <c>EWJoeCombatSystem.Judgement.cs</c>; ECS-05 Desecrate in <c>EWJoeCombatSystem.Desecrate.cs</c> and
/// the W7 xeno branches (Industrial Grip, Overpressure, Chitin Crack, Pin) in <c>EWJoeCombatSystem.Xeno.cs</c>.
/// Add new subscriptions here only, never in a partial.
/// </remarks>
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
    // RB-13: knockdowns and paralyzes go through EWJoeLiturgySystem.ApplyHardCc, not the stun system directly
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TagSystem _tag = default!;
    // ECS-05 Desecrate and the W7 xeno branches (N10/N11)
    [Dependency] private SharedBoneSystem _bone = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _bodyIndex = default!;
    [Dependency] private SharedBodyZoneTargetingSystem _zoneTargeting = default!;
    [Dependency] private RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private EWJoeArmorShredSystem _shred = default!;
    // FLOW-02: every second of a hold (choke or pin) resets the victim's struggle
    [Dependency] private EWJoeMachineGripSystem _grip = default!;
    // ARCH-05/FLOW-06 (N12): each handler reports a landed ability to the Liturgy meter and asks it for Exalted
    [Dependency] private EWJoeLiturgySystem _liturgy = default!;

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
        SubscribeLocalEvent<EWJoeComponent, EWJoeDesecrateActionEvent>(OnDesecrate);
        // W7 0A: (EWJoeComponent, MeleeHitEvent) belongs to EWJoeScourSystem, so the grip has its own component
        SubscribeLocalEvent<EWJoeIndustrialGripComponent, MeleeHitEvent>(OnIndustrialGripHit);
        // W7 2B: a pin breaks when the Joe takes enough damage holding it
        SubscribeLocalEvent<EWJoePinningComponent, DamageChangedEvent>(OnPinnerDamaged);
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

            // W7 2B: a pin on xenofauna roots and bruises instead of draining stamina it does not have
            if (HasComp<EWJoePinningComponent>(joe))
                PinTick((joe, comp), choke.Target);
            else
                ChokeTick((joe, comp), choke);

            // FLOW-02: the hold resets the body's struggle against the grip. RB-18 (FLOW-17): at most once per hold,
            // shared with every other source (machete hits); the grip system keeps the count.
            if (TryComp(choke.Target, out FiremanCarriableComponent? carriable) && carriable.BreakingFree)
                _grip.ExtendStruggle(choke.Target);
        }
    }

    /// <summary>
    /// One second of a choke: drains <see cref="EWJoeComponent.ChokeStaminaPerSecond"/>. RB-17: the choke knocks the
    /// victim out at most once per hold. Once RMC's stamina has reached its knockout tier during the hold, later ticks
    /// stop at <see cref="EWJoeChokeComponent.StaminaFloorAfterKnockout"/>, so RMC's own regeneration can lift the
    /// victim out of the knockout and the choke cannot push it back in.
    /// </summary>
    private void ChokeTick(Entity<EWJoeComponent> joe, EWJoeChokeComponent choke)
    {
        var target = choke.Target;
        if (!TryComp(target, out RMCStaminaComponent? stamina))
            return;

        var amount = joe.Comp.ChokeStaminaPerSecond;
        if (choke.KnockedOut)
            amount = Math.Min(amount, stamina.Current - choke.StaminaFloorAfterKnockout);

        if (amount > 0)
            _stamina.DoStaminaDamage((target, stamina), amount);

        // RMC knocks out at the last tier (stamina below 10); the level is set by DoStaminaDamage and RMC's own tick
        if (!choke.KnockedOut && stamina.TierThresholds.Length > 0 && stamina.Level >= stamina.TierThresholds.Length - 1)
        {
            choke.KnockedOut = true;
            _grip.MarkChokeKnockedOut(target);
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

    /// <summary>The body the Joe holds in an aggressive grab, if any.</summary>
    private bool TryGetGrabbed(Entity<EWJoeComponent> joe, out EntityUid victim)
    {
        victim = default;
        if (!TryComp(joe, out PullerComponent? puller) || puller.Pulling is not { } pulled || !IsGrabbedBy(joe, pulled))
            return false;

        victim = pulled;
        return true;
    }

    #endregion

    private void OnJoeRemoved(ref EWJoeRemovedEvent args)
    {
        StopChoke(args.Joe, "stood down");
    }
}

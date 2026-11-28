using Content.Server.Administration.Logs;
using Content.Server.CMU14.Combat.CQC;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.CMU14.Combat.Drown;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Combat.Drown;

/// <summary>
/// Drown for any body with a <see cref="CMUDrownComponent"/>: the <see cref="EWJoeDrownActionEvent"/> action on an
/// aggressively grabbed, living organic starts a hold (<see cref="EWJoeDrownDoAfterEvent"/>); when it finishes the victim's
/// lungs reach their dead stage, it sleeps and counts as dead for objectives while it sleeps, and the drowner heals a share
/// of its own dead threshold. The victim is told what is happening during the hold (text pass A). A landed drowning is a
/// CQC ability (<see cref="CMUCqcSystem.RegisterAbility"/>).
/// </summary>
/// <remarks>
/// <see cref="CMUDrownAttemptEvent"/> is raised on the drowner first, so an owner can take the attempt over (the Early
/// Warning Joe's Overpressure burst on xenofauna). Subscriptions: (CMUDrownComponent, EWJoeDrownActionEvent) and
/// (CMUDrownComponent, EWJoeDrownDoAfterEvent). Server only.
/// </remarks>
public sealed class CMUDrownSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CMUCqcSystem _cqc = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private FiremanCarrySystem _fireman = default!;
    [Dependency] private CMUChemicalMedicalSystem _medical = default!;
    [Dependency] private EWJoeMessageSystem _messages = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;

    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";
    private static readonly EntProtoId ForcedSleeping = "StatusEffectForcedSleeping";

    private readonly List<(EntityUid Victim, bool AddedDeadIc)> _recovered = new();
    private readonly List<EntityUid> _stale = new();
    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUDrownComponent, EWJoeDrownActionEvent>(OnDrown);
        SubscribeLocalEvent<CMUDrownComponent, EWJoeDrownDoAfterEvent>(OnDrownDoAfter);
    }

    private void OnDrown(Entity<CMUDrownComponent> ent, ref EWJoeDrownActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;

        // an owner may take the attempt over (the Early Warning Joe's Overpressure burst on xenofauna)
        var attempt = new CMUDrownAttemptEvent(target);
        RaiseLocalEvent(ent, ref attempt);
        if (attempt.Handled)
        {
            args.Handled = true;
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
        var drowning = EnsureComp<CMUDrowningComponent>(target);
        drowning.Joe = ent;
        drowning.Beat = 0;
        drowning.Until = _timing.CurTime + ent.Comp.DrownDelay + TimeSpan.FromSeconds(2);
    }

    private void OnDrownDoAfter(Entity<CMUDrownComponent> ent, ref EWJoeDrownDoAfterEvent args)
    {
        // the hold is over, finished or broken
        if (args.Args.Target is { } held)
            RemComp<CMUDrowningComponent>(held);

        if (args.Handled || args.Cancelled || args.Args.Target is not { } target)
            return;

        args.Handled = true;

        if (!CanDrown(ent, target, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), target, ent, PopupType.SmallCaution);
            return;
        }

        Drown(ent, target);
        _cqc.RegisterAbility(ent, target);
    }

    /// <summary>A living organic humanoid in <paramref name="user"/>'s aggressive grab.</summary>
    public bool CanDrown(EntityUid user, EntityUid target, out string reason)
    {
        reason = "cmu-ew-joe-drown-invalid";
        if (!Exists(target) || !_mobState.IsAlive(target))
            return false;

        if (HasComp<SynthComponent>(target))
        {
            reason = "cmu-ew-joe-drown-synthetic";
            return false;
        }

        // ABIL-10: the one rule for who is a person (no yautja, threats or drones)
        if (!EWJoeTargetRules.IsOrganic(EntityManager, target))
            return false;

        if (!IsGrabbedBy(user, target))
        {
            reason = "cmu-ew-joe-needs-grab";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Balance A-5: lungs to the dead stage (asphyxiation that keeps ticking), a sleep that cannot be shaken off, the
    /// victim counts as dead for objectives, and the drowner heals a share of its own dead threshold. Public so tests can
    /// skip the do-after.
    /// </summary>
    public void Drown(Entity<CMUDrownComponent> user, EntityUid target)
    {
        _messages.Sense(target, Loc.GetString("cmu-ew-joe-drown-victim-end"));
        _medical.DamageOrgan<LungsComponent>(target, user.Comp.DrownLungDamage, Asphyxiation);
        _status.TryAddStatusEffectDuration(target, ForcedSleeping, user.Comp.DrownSleep);

        // dead for objectives only while the sleep lasts; the sweep in Update lifts it again
        var drowned = EnsureComp<CMUDrownedComponent>(target);
        drowned.Until = _timing.CurTime + user.Comp.DrownSleep;
        if (!HasComp<IsDeadICComponent>(target))
        {
            EnsureComp<IsDeadICComponent>(target);
            drowned.AddedDeadIc = true;
        }

        // HealEvenly heals by a negative amount
        if (_thresholds.TryGetDeadThreshold(user, out var dead))
            _damageable.HealEvenly(user.Owner, -(dead.Value * user.Comp.DrownHealFraction), origin: user);

        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-drown-done", ("target", target)), target, user);
        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(target):target} was drowned by {ToPrettyString(user):user}");
    }

    private bool IsGrabbedBy(EntityUid user, EntityUid target)
    {
        return TryComp(user, out PullerComponent? puller)
            && puller.Pulling == target
            && _fireman.IsAggressivelyGrabbed(target);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        _nextTick = now + TimeSpan.FromSeconds(1);

        // drownings wear off
        _recovered.Clear();
        var drownedQuery = EntityQueryEnumerator<CMUDrownedComponent>();
        while (drownedQuery.MoveNext(out var uid, out var drowned))
        {
            if (now >= drowned.Until)
                _recovered.Add((uid, drowned.AddedDeadIc));
        }

        foreach (var (victim, addedDeadIc) in _recovered)
        {
            if (addedDeadIc)
                RemComp<IsDeadICComponent>(victim);
            RemComp<CMUDrownedComponent>(victim);
        }

        _recovered.Clear();
        TellDrowning(now);
    }

    /// <summary>
    /// Text pass A: every other second of a hold the victim's airway fills a little more, and twice the signal tells
    /// them to relax. A marker whose hold ended without its event is dropped.
    /// </summary>
    private void TellDrowning(TimeSpan now)
    {
        _stale.Clear();
        var query = EntityQueryEnumerator<CMUDrowningComponent>();
        while (query.MoveNext(out var victim, out var drowning))
        {
            if (now >= drowning.Until)
            {
                _stale.Add(victim);
                continue;
            }

            drowning.Beat++;
            if (drowning.Beat % 2 == 0)
                _messages.Sense(victim, _messages.Pick(EWJoeMessageSystem.DrownAirway));
            if (drowning.Beat == 3 || drowning.Beat == 6)
                _messages.Whisper(victim, _messages.Pick(EWJoeMessageSystem.DrownVoice));
        }

        foreach (var victim in _stale)
        {
            RemComp<CMUDrowningComponent>(victim);
        }

        _stale.Clear();
    }
}

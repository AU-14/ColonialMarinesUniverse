using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Combat.Grip;
using Content.Shared.CMU14.Fireman;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Combat.Grip;

/// <summary>
/// N5 (FLOW-02) Machine Grip, for any body with a <see cref="CMUMachineGripComponent"/>. The body's first Ctrl-click on a
/// valid target warns the victim and, after a short windup the victim breaks by moving away, goes straight to an
/// aggressive grab through <see cref="FiremanCarrySystem.TryAggressiveGrab"/>. Every other grabber keeps RMC's two-step
/// grab. Also holds the xeno grab to its cap and cooldown, and lets the body's holds restart a victim's struggle.
/// </summary>
/// <remarks>
/// The single owner of <see cref="CMUMachineGripComponent"/>'s ComponentStartup (records the mob's own values and applies
/// the grants: the upgrade delay, the escape multiplier and, with <see cref="CMUMachineGripComponent.XenoGrab"/>, the xeno
/// grab with pull immunity) and ComponentShutdown (puts them back). Owners change the grants through
/// <see cref="SetEscapeMultiplier"/>, <see cref="SetXenoGrab"/> and <see cref="RestoreBase"/>.
/// </remarks>
public sealed class CMUMachineGripSystem : EntitySystem
{
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private FiremanCarrySystem _fireman = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private RMCPullingSystem _rmcPulling = default!;
    [Dependency] private RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private SkillsSystem _skills = default!;

    private readonly List<(EntityUid Grabber, EntityUid Xeno, bool Release)> _endedHolds = new();

    /// <summary>RB-18 (FLOW-17): how many times one hold may restart the victim's struggle, all sources together.</summary>
    public const int StruggleRestartsPerHold = 1;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUMachineGripComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUMachineGripComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUMachineGripComponent, PullStartedMessage>(OnPullStarted);
        SubscribeLocalEvent<CMUMachineGripComponent, DoAfterAttemptEvent<CMUTelegraphGrabDoAfterEvent>>(OnGripAttempt);
        SubscribeLocalEvent<CMUMachineGripComponent, CMUTelegraphGrabDoAfterEvent>(OnGripDoAfter);
        SubscribeLocalEvent<CMUXenoGrabberComponent, CMUAggressiveGrabbedEvent>(OnXenoGrabbed);
    }

    #region Grants

    private void OnStartup(Entity<CMUMachineGripComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.BaseEscapeMultiplier = ent.Comp.EscapeMultiplier;
        ent.Comp.BaseXenoGrab = ent.Comp.XenoGrab;

        if (TryComp(ent, out CanFiremanCarryComponent? carry))
        {
            ent.Comp.OriginalAggressiveGrabDelay ??= carry.AggressiveGrabDelay;
            // review fix: a manual second Ctrl-click must not beat the telegraph (the decided one-click grab keeps the
            // victim's 0.4 s window). The upgrade delay is scaled by the CQC skill, so store the telegraph divided by
            // that multiplier: the manual upgrade unlocks exactly when the telegraph would land.
            var skillMultiplier = _skills.GetSkillDelayMultiplier(ent.Owner, carry.Skill);
            var delay = skillMultiplier > 0 ? ent.Comp.Telegraph / skillMultiplier : ent.Comp.Telegraph;
            _fireman.SetAggressiveGrabDelay((ent.Owner, carry), delay);
        }

        ApplyEscapeMultiplier(ent);
        ApplyXenoGrab(ent);
    }

    private void OnShutdown(Entity<CMUMachineGripComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (ent.Comp.OriginalAggressiveGrabDelay is { } delay)
            _fireman.SetAggressiveGrabDelay(ent.Owner, delay);

        ent.Comp.OriginalAggressiveGrabDelay = null;

        if (ent.Comp.AddedEscapeMultiplier)
        {
            RemComp<CMUGrabEscapeMultiplierComponent>(ent);
        }
        else if (ent.Comp.OriginalEscapeMultiplier is { } original &&
                 TryComp(ent, out CMUGrabEscapeMultiplierComponent? escape))
        {
            escape.Multiplier = original;
            Dirty(ent, escape);
        }

        ent.Comp.AddedEscapeMultiplier = false;
        ent.Comp.OriginalEscapeMultiplier = null;

        RemoveXenoGrip(ent);
    }

    /// <summary>Sets the break-free multiplier the body's grabs impose and applies it at once.</summary>
    public void SetEscapeMultiplier(Entity<CMUMachineGripComponent?> ent, float multiplier)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.EscapeMultiplier = multiplier;
        ApplyEscapeMultiplier((ent.Owner, ent.Comp));
    }

    /// <summary>Turns the xeno grab (with its pull immunity) on or off and applies it at once.</summary>
    public void SetXenoGrab(Entity<CMUMachineGripComponent?> ent, bool xenoGrab)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.XenoGrab = xenoGrab;
        ApplyXenoGrab((ent.Owner, ent.Comp));
    }

    /// <summary>Puts the escape multiplier and the xeno grab back to what the component started with.</summary>
    public void RestoreBase(Entity<CMUMachineGripComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        SetEscapeMultiplier(ent, ent.Comp.BaseEscapeMultiplier);
        SetXenoGrab(ent, ent.Comp.BaseXenoGrab);
    }

    private void ApplyEscapeMultiplier(Entity<CMUMachineGripComponent> ent)
    {
        if (TryComp(ent, out CMUGrabEscapeMultiplierComponent? escape))
        {
            if (!ent.Comp.AddedEscapeMultiplier)
                ent.Comp.OriginalEscapeMultiplier ??= escape.Multiplier;
        }
        else
        {
            escape = AddComp<CMUGrabEscapeMultiplierComponent>(ent);
            ent.Comp.AddedEscapeMultiplier = true;
        }

        escape.Multiplier = ent.Comp.EscapeMultiplier;
        Dirty(ent, escape);
    }

    private void ApplyXenoGrab(Entity<CMUMachineGripComponent> ent)
    {
        if (!ent.Comp.XenoGrab)
        {
            RemoveXenoGrip(ent);
            return;
        }

        if (!HasComp<CMUXenoGrabberComponent>(ent))
        {
            AddComp<CMUXenoGrabberComponent>(ent);
            ent.Comp.AddedXenoGrabber = true;
        }

        // a synthetic body already carries one from its add-list; only a body without any gets the copy
        var immunityName = _componentFactory.GetComponentName<ParalyzeImmuneFromComponent>();
        if (!HasComp<ParalyzeImmuneFromComponent>(ent) &&
            _proto.TryIndex(ent.Comp.XenoPullImmunity, out var immunity) &&
            immunity.Components.TryGetValue(immunityName, out var entry))
        {
            EntityManager.AddComponents(ent, new ComponentRegistry { [immunityName] = entry }, removeExisting: false);
            ent.Comp.AddedParalyzeImmuneFrom = true;
        }
    }

    private void RemoveXenoGrip(Entity<CMUMachineGripComponent> ent)
    {
        if (ent.Comp.AddedXenoGrabber)
            RemComp<CMUXenoGrabberComponent>(ent);

        if (ent.Comp.AddedParalyzeImmuneFrom)
            RemComp<ParalyzeImmuneFromComponent>(ent);

        ent.Comp.AddedXenoGrabber = false;
        ent.Comp.AddedParalyzeImmuneFrom = false;
    }

    #endregion

    #region Holds

    /// <summary>
    /// A hold restarts the struggle: when <paramref name="victim"/> is mid break-free do-after it starts over at full
    /// length. Choke ticks and machete hits call this. RB-18 (FLOW-17): at most <see cref="StruggleRestartsPerHold"/>
    /// restart per hold, shared by every caller; a call that finds the victim not struggling spends nothing.
    /// Returns whether the struggle was restarted.
    /// </summary>
    public bool ExtendStruggle(EntityUid victim)
    {
        var held = EnsureComp<CMUGripHeldComponent>(victim);
        if (held.StruggleRestarts >= StruggleRestartsPerHold)
            return false;

        if (!_fireman.RestartBreakFree(victim))
            return false;

        held.StruggleRestarts++;
        return true;
    }

    /// <summary>RB-18: how many struggle restarts the current hold on <paramref name="victim"/> has used.</summary>
    public int StruggleRestarts(EntityUid victim)
    {
        return TryComp(victim, out CMUGripHeldComponent? held) ? held.StruggleRestarts : 0;
    }

    /// <summary>RB-17: a choke has already knocked <paramref name="victim"/> out in the current hold.</summary>
    public bool ChokeKnockedOut(EntityUid victim)
    {
        return TryComp(victim, out CMUGripHeldComponent? held) && held.ChokeKnockedOut;
    }

    /// <summary>RB-17: records the one choke knockout of the current hold on <paramref name="victim"/>.</summary>
    public void MarkChokeKnockedOut(EntityUid victim)
    {
        EnsureComp<CMUGripHeldComponent>(victim).ChokeKnockedOut = true;
    }

    #endregion

    #region Telegraph

    private void OnPullStarted(Entity<CMUMachineGripComponent> ent, ref PullStartedMessage args)
    {
        if (args.PullerUid != ent.Owner)
            return;

        var target = args.PulledUid;

        // RB-17/RB-18: a new pull is a new hold (an aggressive grab only ever upgrades a running pull)
        RemComp<CMUGripHeldComponent>(target);

        if (!HasComp<MobStateComponent>(target) ||
            !TryComp(ent, out CanFiremanCarryComponent? carry) ||
            !_fireman.CanAggressiveGrab((ent.Owner, carry), target))
        {
            return;
        }

        // a large xeno (a grabbable T3) takes a longer windup. The manual Ctrl-click upgrade unlocks exactly when this
        // pull's windup would land (the delay is scaled by the CQC skill, so store the windup divided by it); set per
        // pull, so the next pull on a smaller target gets the ordinary window back. Only while the grip grants are on.
        var telegraph = GripTelegraphFor(ent, target);
        if (ent.Comp.OriginalAggressiveGrabDelay != null)
        {
            var skillMultiplier = _skills.GetSkillDelayMultiplier(ent.Owner, carry.Skill);
            _fireman.SetAggressiveGrabDelay((ent.Owner, carry), skillMultiplier > 0 ? telegraph / skillMultiplier : telegraph);
        }

        // no break on the body taking damage: the counterplay is the victim reacting, not third parties
        var doAfter = new DoAfterArgs(EntityManager, ent, telegraph, new CMUTelegraphGrabDoAfterEvent(), ent, target)
        {
            BreakOnMove = true,
            DistanceThreshold = ent.Comp.TelegraphRange,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-grip-telegraph"), target, target, PopupType.MediumCaution);
    }

    /// <summary>The body's grab windup on <paramref name="target"/>: longer on a large xeno (RMC size Big and up).</summary>
    public TimeSpan GripTelegraphFor(Entity<CMUMachineGripComponent> grip, EntityUid target)
    {
        if (HasComp<XenoComponent>(target) && _sizeStun.TryGetSize(target, out var size) && size >= RMCSizes.Big)
            return grip.Comp.Telegraph * Math.Max(1f, grip.Comp.LargeXenoMultiplier);

        return grip.Comp.Telegraph;
    }

    /// <summary>A victim that walks out of the pull (moving breaks a passive pull) ends the windup at once.</summary>
    private void OnGripAttempt(Entity<CMUMachineGripComponent> ent, ref DoAfterAttemptEvent<CMUTelegraphGrabDoAfterEvent> args)
    {
        if (args.DoAfter.Args.Target is not { } target || !_rmcPulling.IsPulling(ent.Owner, target))
            args.Cancel();
    }

    private void OnGripDoAfter(Entity<CMUMachineGripComponent> ent, ref CMUTelegraphGrabDoAfterEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        if (!args.Cancelled &&
            TryComp(ent, out CanFiremanCarryComponent? carry) &&
            _fireman.TryAggressiveGrab((ent.Owner, carry), target))
        {
            return;
        }

        // a manual second click may have landed the grab during the windup
        if (_rmcPulling.IsPulling(ent.Owner, target) && _fireman.IsAggressivelyGrabbed(target))
            return;

        // the passive pull, if the victim did not break it, is left as it is
        var msg = Loc.GetString("cmu-ew-joe-grip-slipped", ("target", Identity.Entity(target, EntityManager, ent.Owner)));
        _popup.PopupEntity(msg, target, ent, PopupType.SmallCaution);
    }

    #endregion

    #region Xeno hold cap

    private void OnXenoGrabbed(Entity<CMUXenoGrabberComponent> ent, ref CMUAggressiveGrabbedEvent args)
    {
        if (!HasComp<XenoComponent>(args.Target) || !TryComp(ent, out CMUMachineGripComponent? grip))
            return;

        ent.Comp.HoldTarget = args.Target;
        ent.Comp.HoldUntil = _timing.CurTime + grip.XenoHoldCap;
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _endedHolds.Clear();

        var query = EntityQueryEnumerator<CMUXenoGrabberComponent, CMUMachineGripComponent>();
        while (query.MoveNext(out var uid, out var grabber, out _))
        {
            if (grabber.HoldTarget is not { } xeno)
                continue;

            var holding = !TerminatingOrDeleted(xeno) &&
                          _rmcPulling.IsPulling(uid, xeno) &&
                          _fireman.IsAggressivelyGrabbed(xeno);

            if (holding && now < grabber.HoldUntil)
                continue;

            _endedHolds.Add((uid, xeno, holding));
        }

        // a hold that ended for any reason starts the cooldown; one that ran to the cap is released here
        foreach (var (grabberUid, xeno, release) in _endedHolds)
        {
            if (!TryComp(grabberUid, out CMUXenoGrabberComponent? grabber) || !TryComp(grabberUid, out CMUMachineGripComponent? grip))
                continue;

            grabber.HoldTarget = null;
            grabber.NextGrab = now + grip.XenoCooldown;
            Dirty(grabberUid, grabber);

            if (!release || !TryComp(xeno, out PullableComponent? pullable))
                continue;

            if (!_pulling.TryStopPull(xeno, pullable, grabberUid))
                continue;

            var msg = Loc.GetString("cmu-ew-joe-grip-xeno-released", ("target", Identity.Entity(xeno, EntityManager, grabberUid)));
            _popup.PopupEntity(msg, xeno, grabberUid, PopupType.SmallCaution);
        }

        _endedHolds.Clear();
    }

    #endregion
}

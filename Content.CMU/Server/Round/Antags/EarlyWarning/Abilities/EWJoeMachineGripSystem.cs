using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Fireman;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// N5 (FLOW-02) Machine Grip. The Early Warning Joe's first Ctrl-click on a valid target warns the victim and, after a
/// short windup the victim breaks by moving away, goes straight to an aggressive grab through
/// <see cref="FiremanCarrySystem.TryAggressiveGrab"/>. Every other grabber keeps RMC's two-step grab. Also holds the
/// small-caste xeno grab to its cap and cooldown, and lets the Joe's holds restart a victim's struggle.
/// </summary>
public sealed class EWJoeMachineGripSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private FiremanCarrySystem _fireman = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private RMCPullingSystem _rmcPulling = default!;
    [Dependency] private RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly List<(EntityUid Joe, EntityUid Xeno, bool Release)> _endedHolds = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeComponent, PullStartedMessage>(OnPullStarted);
        SubscribeLocalEvent<EWJoeComponent, DoAfterAttemptEvent<CMUTelegraphGrabDoAfterEvent>>(OnGripAttempt);
        SubscribeLocalEvent<EWJoeComponent, CMUTelegraphGrabDoAfterEvent>(OnGripDoAfter);
        SubscribeLocalEvent<CMUXenoGrabberComponent, CMUAggressiveGrabbedEvent>(OnXenoGrabbed);
    }

    /// <summary>
    /// A hold restarts the struggle: when <paramref name="victim"/> is mid break-free do-after it starts over at full
    /// length. Choke ticks and machete hits call this.
    /// </summary>
    public void ExtendStruggle(EntityUid victim)
    {
        _fireman.RestartBreakFree(victim);
    }

    private void OnPullStarted(Entity<EWJoeComponent> ent, ref PullStartedMessage args)
    {
        if (args.PullerUid != ent.Owner)
            return;

        var target = args.PulledUid;
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

        // no break on the Joe taking damage: the counterplay is the victim reacting, not third parties
        var doAfter = new DoAfterArgs(EntityManager, ent, telegraph, new CMUTelegraphGrabDoAfterEvent(), ent, target)
        {
            BreakOnMove = true,
            DistanceThreshold = ent.Comp.GripTelegraphRange,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-grip-telegraph"), target, target, PopupType.MediumCaution);
    }

    /// <summary>The Joe's grab windup on <paramref name="target"/>: longer on a large xeno (RMC size Big and up).</summary>
    public TimeSpan GripTelegraphFor(Entity<EWJoeComponent> joe, EntityUid target)
    {
        if (HasComp<XenoComponent>(target) && _sizeStun.TryGetSize(target, out var size) && size >= RMCSizes.Big)
            return joe.Comp.GripTelegraph * Math.Max(1f, joe.Comp.LargeXenoGripMultiplier);

        return joe.Comp.GripTelegraph;
    }

    /// <summary>A victim that walks out of the pull (moving breaks a passive pull) ends the windup at once.</summary>
    private void OnGripAttempt(Entity<EWJoeComponent> ent, ref DoAfterAttemptEvent<CMUTelegraphGrabDoAfterEvent> args)
    {
        if (args.DoAfter.Args.Target is not { } target || !_rmcPulling.IsPulling(ent.Owner, target))
            args.Cancel();
    }

    private void OnGripDoAfter(Entity<EWJoeComponent> ent, ref CMUTelegraphGrabDoAfterEvent args)
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

    private void OnXenoGrabbed(Entity<CMUXenoGrabberComponent> ent, ref CMUAggressiveGrabbedEvent args)
    {
        if (!HasComp<XenoComponent>(args.Target) || !TryComp(ent, out EWJoeComponent? joe))
            return;

        ent.Comp.HoldTarget = args.Target;
        ent.Comp.HoldUntil = _timing.CurTime + joe.XenoGripHoldCap;
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _endedHolds.Clear();

        var query = EntityQueryEnumerator<CMUXenoGrabberComponent, EWJoeComponent>();
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
        foreach (var (joe, xeno, release) in _endedHolds)
        {
            if (!TryComp(joe, out CMUXenoGrabberComponent? grabber) || !TryComp(joe, out EWJoeComponent? comp))
                continue;

            grabber.HoldTarget = null;
            grabber.NextGrab = now + comp.XenoGripCooldown;
            Dirty(joe, grabber);

            if (!release || !TryComp(xeno, out PullableComponent? pullable))
                continue;

            if (!_pulling.TryStopPull(xeno, pullable, joe))
                continue;

            var msg = Loc.GetString("cmu-ew-joe-grip-xeno-released", ("target", Identity.Entity(xeno, EntityManager, joe)));
            _popup.PopupEntity(msg, xeno, joe, PopupType.SmallCaution);
        }

        _endedHolds.Clear();
    }
}

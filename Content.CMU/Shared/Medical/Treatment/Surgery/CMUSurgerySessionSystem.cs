using Content.Shared._CMU14.Medical.Core;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Treatment.Surgery;

/// <summary>
///     Owns the lifecycle of patient-scoped surgery sessions and serializes
///     their short-lived attempts. It deliberately has no claim/release API.
/// </summary>
public sealed partial class CMUSurgerySessionSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    private ulong _lastSessionId;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUSurgeryAttemptActorComponent, EntityTerminatingEvent>(OnAttemptActorTerminating);
        SubscribeLocalEvent<CMUSurgeryAttemptActorComponent, PlayerDetachedEvent>(OnAttemptActorDetached);
        SubscribeLocalEvent<CMUSurgerySessionComponent, EntityTerminatingEvent>(OnSessionPatientTerminating);
    }

    public CMUSurgeryAttemptStartResult TryBeginAttempt(
        EntityUid patient,
        EntityUid surgeon,
        EntityUid tool,
        EntityUid target,
        CMUMedicalBodyPartKey site,
        EntProtoId<CMSurgeryComponent> procedure,
        EntProtoId<CMSurgeryStepComponent> step,
        out CMUSurgeryAttemptToken token)
    {
        token = default;
        if (!_net.IsServer)
            return CMUSurgeryAttemptStartResult.NotAuthoritative;

        if (TryComp<CMUSurgeryAttemptActorComponent>(surgeon, out var actorAttempt))
        {
            if (IsActorAttemptCurrent(surgeon, actorAttempt))
                return CMUSurgeryAttemptStartResult.Busy;

            RemComp<CMUSurgeryAttemptActorComponent>(surgeon);
        }

        var session = CompOrNull<CMUSurgerySessionComponent>(patient);
        if (session is null)
        {
            session = EnsureComp<CMUSurgerySessionComponent>(patient);
            session.Id = NextSessionId();
            session.Site = site;
            session.Procedure = procedure;
            session.Phase = CMUSurgerySessionPhase.AwaitingAction;
        }
        else
        {
            if (session.Phase == CMUSurgerySessionPhase.Performing)
                return CMUSurgeryAttemptStartResult.Busy;
            if (session.Site != site)
                return CMUSurgeryAttemptStartResult.SiteConflict;

            // A procedure may change only between attempts. The legacy lock
            // still controls whether that change is an allowed continuation.
            session.Procedure = procedure;
        }

        var attempt = unchecked(session.LastAttempt + 1);
        if (attempt == 0)
            attempt = 1;

        session.LastAttempt = attempt;
        session.CurrentStep = step;
        session.ActiveAttempt = new CMUSurgeryAttemptToken(session.Id, attempt);
        session.ActiveSurgeon = surgeon;
        session.ActiveTool = tool;
        session.ActiveTarget = target;
        session.Phase = CMUSurgerySessionPhase.Performing;
        token = session.ActiveAttempt.Value;

        var actor = EnsureComp<CMUSurgeryAttemptActorComponent>(surgeon);
        actor.Patient = patient;
        actor.Attempt = token;
        return CMUSurgeryAttemptStartResult.Started;
    }

    public bool IsAttemptCurrent(
        EntityUid patient,
        CMUSurgeryAttemptToken token,
        EntityUid surgeon,
        EntityUid? tool,
        EntityUid? target,
        EntProtoId<CMSurgeryStepComponent> step)
    {
        return TryComp<CMUSurgerySessionComponent>(patient, out var session)
            && session.Phase == CMUSurgerySessionPhase.Performing
            && session.ActiveAttempt == token
            && session.ActiveSurgeon == surgeon
            && session.ActiveTool == tool
            && session.ActiveTarget == target
            && session.CurrentStep == step;
    }

    /// <summary>
    ///     Atomically consumes the active attempt and returns the session to
    ///     awaiting action. A stale or duplicate token cannot consume state.
    /// </summary>
    public bool TryConsumeAttempt(
        EntityUid patient,
        CMUSurgeryAttemptToken token,
        EntityUid surgeon,
        EntityUid? tool,
        EntityUid? target,
        EntProtoId<CMSurgeryStepComponent> step)
    {
        if (!_net.IsServer || !IsAttemptCurrent(patient, token, surgeon, tool, target, step))
            return false;

        var session = Comp<CMUSurgerySessionComponent>(patient);
        ClearActiveAttempt(patient, session, CMUSurgerySessionPhase.AwaitingAction);
        return true;
    }

    public bool CancelActiveAttempt(EntityUid patient)
    {
        if (!_net.IsServer
            || !TryComp<CMUSurgerySessionComponent>(patient, out var session)
            || session.Phase != CMUSurgerySessionPhase.Performing)
        {
            return false;
        }

        ClearActiveAttempt(patient, session, CMUSurgerySessionPhase.AwaitingAction);
        return true;
    }

    public bool SetAwaitingDecision(EntityUid patient)
    {
        if (!_net.IsServer
            || !TryComp<CMUSurgerySessionComponent>(patient, out var session)
            || session.Phase != CMUSurgerySessionPhase.AwaitingAction)
        {
            return false;
        }

        ClearActiveAttempt(patient, session, CMUSurgerySessionPhase.AwaitingDecision);
        return true;
    }

    public bool IsPerforming(EntityUid patient)
    {
        return TryComp<CMUSurgerySessionComponent>(patient, out var session)
            && session.Phase == CMUSurgerySessionPhase.Performing;
    }

    public bool TryGetSession(EntityUid patient, out CMUSurgerySessionSnapshot snapshot)
    {
        snapshot = default;
        if (!TryComp<CMUSurgerySessionComponent>(patient, out var session))
            return false;

        snapshot = new CMUSurgerySessionSnapshot(
            session.Id,
            session.Site,
            session.Procedure,
            session.CurrentStep,
            session.Phase,
            session.ActiveSurgeon,
            session.ActiveAttempt,
            session.ActiveTarget);
        return true;
    }

    /// <summary>
    ///     Checks that a command created from a previously rendered view still
    ///     refers to the exact current session state. This prevents delayed UI
    ///     commands from cancelling a replacement attempt or session.
    /// </summary>
    public bool MatchesExpectedState(
        EntityUid patient,
        CMUSurgerySessionId? expectedSession,
        CMUSurgeryAttemptToken? expectedAttempt)
    {
        if (expectedSession is not { } sessionId)
        {
            return expectedAttempt is null
                && !HasComp<CMUSurgerySessionComponent>(patient);
        }

        return TryComp<CMUSurgerySessionComponent>(patient, out var session)
            && session.Id == sessionId
            && session.ActiveAttempt == expectedAttempt;
    }

    public void EndSession(EntityUid patient)
    {
        if (!_net.IsServer || !TryComp<CMUSurgerySessionComponent>(patient, out var session))
            return;

        ClearActiveAttempt(patient, session, CMUSurgerySessionPhase.AwaitingAction);
        RemComp<CMUSurgerySessionComponent>(patient);
    }

    private bool IsActorAttemptCurrent(EntityUid actor, CMUSurgeryAttemptActorComponent attempt)
    {
        return TryComp<CMUSurgerySessionComponent>(attempt.Patient, out var session)
            && session.Phase == CMUSurgerySessionPhase.Performing
            && session.ActiveSurgeon == actor
            && session.ActiveAttempt == attempt.Attempt;
    }

    private CMUSurgerySessionId NextSessionId()
    {
        _lastSessionId = unchecked(_lastSessionId + 1);
        if (_lastSessionId == 0)
            _lastSessionId = 1;

        return new CMUSurgerySessionId(_lastSessionId);
    }

    private void OnAttemptActorTerminating(
        Entity<CMUSurgeryAttemptActorComponent> ent,
        ref EntityTerminatingEvent args)
    {
        ReleaseAttemptForLostActor(ent, removeActorMarker: false);
    }

    private void OnAttemptActorDetached(
        Entity<CMUSurgeryAttemptActorComponent> ent,
        ref PlayerDetachedEvent args)
    {
        ReleaseAttemptForLostActor(ent, removeActorMarker: true);
    }

    private void ReleaseAttemptForLostActor(
        Entity<CMUSurgeryAttemptActorComponent> ent,
        bool removeActorMarker)
    {
        if (!_net.IsServer || !IsActorAttemptCurrent(ent.Owner, ent.Comp))
            return;

        var patient = ent.Comp.Patient;
        var attempt = ent.Comp.Attempt;
        var session = Comp<CMUSurgerySessionComponent>(patient);
        ClearActiveAttempt(
            patient,
            session,
            CMUSurgerySessionPhase.AwaitingAction,
            removeActorMarker);

        var ev = new CMUSurgeryAttemptActorLostEvent(attempt);
        RaiseLocalEvent(patient, ref ev);
    }

    private void OnSessionPatientTerminating(
        Entity<CMUSurgerySessionComponent> ent,
        ref EntityTerminatingEvent args)
    {
        if (_net.IsServer)
            ClearActiveAttempt(ent.Owner, ent.Comp, CMUSurgerySessionPhase.AwaitingAction);
    }

    private void ClearActiveAttempt(
        EntityUid patient,
        CMUSurgerySessionComponent session,
        CMUSurgerySessionPhase nextPhase,
        bool removeActorMarker = true)
    {
        if (removeActorMarker
            && session.ActiveSurgeon is { } surgeon
            && TryComp<CMUSurgeryAttemptActorComponent>(surgeon, out var actor)
            && actor.Patient == patient
            && actor.Attempt == session.ActiveAttempt)
        {
            RemComp<CMUSurgeryAttemptActorComponent>(surgeon);
        }

        session.ActiveAttempt = null;
        session.ActiveSurgeon = null;
        session.ActiveTool = null;
        session.ActiveTarget = null;
        session.Phase = nextPhase;
    }
}

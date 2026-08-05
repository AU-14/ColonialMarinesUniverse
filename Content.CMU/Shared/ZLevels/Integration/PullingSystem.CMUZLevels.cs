using Content.Shared.Movement.Pulling.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Shared.Movement.Pulling.Systems;

public sealed partial class PullingSystem
{
    public bool TryDetachPullJointForTransfer(
        EntityUid pullerUid,
        EntityUid pullableUid,
        PullerComponent? pullerComp = null,
        PullableComponent? pullableComp = null)
    {
        if (!ResolveActivePullRelationship(pullerUid, pullableUid, ref pullerComp, ref pullableComp))
            return false;

        var resolvedPullable = pullableComp!;

        if (_timing.ApplyingState || resolvedPullable.PullJointId is not { } pullJointId)
            return true;

        resolvedPullable.PullJointId = null;
        _joints.RemoveJoint(pullableUid, pullJointId);
        _joints.RemoveJoint(pullerUid, pullJointId);
        Dirty(pullableUid, resolvedPullable);
        return true;
    }

    public bool TryRefreshPullJointForTransfer(
        EntityUid pullerUid,
        EntityUid pullableUid,
        PullerComponent? pullerComp = null,
        PullableComponent? pullableComp = null)
    {
        if (!ResolveActivePullRelationship(pullerUid, pullableUid, ref pullerComp, ref pullableComp))
            return false;

        var resolvedPuller = pullerComp!;
        var resolvedPullable = pullableComp!;
        var pullerMap = Transform(pullerUid).MapID;

        if (pullerMap == MapId.Nullspace || pullerMap != Transform(pullableUid).MapID)
            return false;

        var pullJointId = $"pull-joint-{GetNetEntity(pullableUid)}";

        if (!_timing.ApplyingState)
        {
            if (resolvedPullable.PullJointId is { } oldJointId)
            {
                resolvedPullable.PullJointId = null;
                _joints.RemoveJoint(pullableUid, oldJointId);
                _joints.RemoveJoint(pullerUid, oldJointId);
            }

            _joints.RemoveJoint(pullableUid, pullJointId);
            _joints.RemoveJoint(pullerUid, pullJointId);

            if (!TryComp(pullerUid, out PhysicsComponent? pullerPhysics) ||
                !TryComp(pullableUid, out PhysicsComponent? pullablePhysics))
            {
                return false;
            }

            resolvedPullable.PullJointId = pullJointId;
            var joint = _joints.CreateDistanceJoint(
                pullableUid,
                pullerUid,
                pullablePhysics.LocalCenter,
                pullerPhysics.LocalCenter,
                id: pullJointId,
                minimumDistance: 1);
            joint.CollideConnected = false;
            joint.MaxLength = joint.Length + 0.15f;
            joint.MinLength = 0f;
            joint.Stiffness = 0f;

            _physics.SetFixedRotation(pullableUid, resolvedPullable.FixedRotationOnPull, body: pullablePhysics);
            EnsureComp<ActivePullerComponent>(pullerUid);
        }
        else
        {
            resolvedPullable.PullJointId = pullJointId;
        }

        Dirty(pullerUid, resolvedPuller);
        Dirty(pullableUid, resolvedPullable);
        return true;
    }

    private bool ResolveActivePullRelationship(
        EntityUid pullerUid,
        EntityUid pullableUid,
        ref PullerComponent? pullerComp,
        ref PullableComponent? pullableComp)
    {
        return Resolve(pullerUid, ref pullerComp, false) &&
               Resolve(pullableUid, ref pullableComp, false) &&
               pullerComp.Pulling == pullableUid &&
               pullableComp.Puller == pullerUid;
    }
}

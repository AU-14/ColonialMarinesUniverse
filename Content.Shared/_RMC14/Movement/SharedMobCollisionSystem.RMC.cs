using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Movement;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Movement.Systems;

namespace Content.Shared.Movement.Systems;

public abstract partial class SharedMobCollisionSystem
{
    private EntityQuery<RMCMobCollisionMassComponent> _rmcCollisionMassQuery;
    private EntityQuery<RMCSizeComponent> _rmcSizeQuery;
    private EntityQuery<XenoComponent> _rmcXenoQuery;

    private float _rmcPenetrationBase;
    private bool _rmcBigXenosCancelMovement;

    private void InitializeRMC()
    {
        _rmcCollisionMassQuery = GetEntityQuery<RMCMobCollisionMassComponent>();
        _rmcSizeQuery = GetEntityQuery<RMCSizeComponent>();
        _rmcXenoQuery = GetEntityQuery<XenoComponent>();

        Subs.CVar(CfgManager, RMCCVars.RMCMovementPenCapSubtract, value => _rmcPenetrationBase = value, true);
        Subs.CVar(CfgManager, RMCCVars.RMCMovementBigXenosCancelMovement, value => _rmcBigXenosCancelMovement = value, true);
    }

    private float GetRMCPenetrationBase()
    {
        return _rmcPenetrationBase;
    }

    private float GetRMCCollisionMass(EntityUid uid, float fallback)
    {
        return _rmcCollisionMassQuery.TryComp(uid, out var collisionMass)
            ? collisionMass.Mass
            : fallback;
    }

    private bool ShouldCancelRMCMovement(EntityUid mover, EntityUid other)
    {
        return _rmcBigXenosCancelMovement &&
               _rmcXenoQuery.HasComponent(mover) &&
               _rmcSizeQuery.TryComp(mover, out var moverSize) &&
               moverSize.Size >= RMCSizes.Big &&
               _rmcXenoQuery.HasComponent(other) &&
               _rmcSizeQuery.TryComp(other, out var otherSize) &&
               otherSize.Size < RMCSizes.Big;
    }
}

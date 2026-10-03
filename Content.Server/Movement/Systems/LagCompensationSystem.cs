using Content.Server._RMC14.Movement;
using Content.Server.Movement.Components;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Movement.Systems;

/// <summary>
/// Stores a buffer of previous positions of the relevant entity.
/// Can be used to check the entity's position at a recent point in time.
/// </summary>
public sealed partial class LagCompensationSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    // I figured 500 ping is max, so 1.5 is 750.
    // Max ping I've had is 350ms from aus to spain.
    public TimeSpan BufferTime = TimeSpan.FromMilliseconds(750);

    // RMC14
    [Dependency] private RMCLagCompensationSystem _rmcLagCompensation = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Level = LogLevel.Info;
        SubscribeLocalEvent<LagCompensationComponent, MoveEvent>(OnLagMove);
        SubscribeLocalEvent<LagCompensationComponent, ComponentStartup>(OnLagStartup); // CMU14: seed stationary history.
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var earliestTime = curTime - BufferTime;

        // Cull any old ones from active updates
        // Probably fine to include ignored.
        var query = AllEntityQuery<LagCompensationComponent>();

        while (query.MoveNext(out var comp))
        {
            PruneHistory(comp, earliestTime); // CMU14: keep the last predecessor of the retained window.
        }
    }

    private void OnLagMove(EntityUid uid, LagCompensationComponent component, ref MoveEvent args)
    {
        if (!args.NewPosition.EntityId.IsValid())
            return; // probably being sent to nullspace for deletion.

        component.Positions.Enqueue((_timing.CurTime, args.NewPosition, args.NewRotation));
    }

    public (EntityCoordinates Coordinates, Angle Angle) GetCoordinatesAngle(EntityUid uid, ICommonSession? pSession,
        TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return (EntityCoordinates.Invalid, Angle.Zero);

        if (pSession == null || !TryComp<LagCompensationComponent>(uid, out var lag) || lag.Positions.Count == 0)
            return (xform.Coordinates, xform.LocalRotation);

        // CMU14 History Begin: LastRealTick is the final applied snapshot, not projectile execution time.
        var viewTick = _rmcLagCompensation.GetLastRealTick(pSession.UserId);
        if (viewTick > _timing.CurTick)
            return (xform.Coordinates, xform.LocalRotation);

        var offsetTime = (_timing.CurTick - viewTick.Value).Value * _timing.TickPeriod;
        return GetCoordinatesAngleAtTime(uid, _timing.CurTime - offsetTime, xform);
        // CMU14 End
    }

    public Angle GetAngle(EntityUid uid, ICommonSession? session, TransformComponent? xform = null)
    {
        var (_, angle) = GetCoordinatesAngle(uid, session, xform);
        return angle;
    }

    public EntityCoordinates GetCoordinates(EntityUid uid, ICommonSession? session, TransformComponent? xform = null)
    {
        var (coordinates, _) = GetCoordinatesAngle(uid, session, xform);
        return coordinates;
    }
}

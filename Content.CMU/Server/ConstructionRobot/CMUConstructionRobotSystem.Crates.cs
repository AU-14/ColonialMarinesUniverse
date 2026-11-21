using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14.ConstructionRobot;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;
using Robust.Shared.Map;

namespace Content.Server.CMU14.ConstructionRobot;

public sealed partial class CMUConstructionRobotSystem
{
    private readonly Dictionary<EntityUid, EntityUid> _crateOrders = new();
    private readonly Dictionary<EntityUid, TimeSpan> _crateRetryAfter = new();

    private void OnCrateGetVerbs(Entity<RequisitionsCrateComponent> crate, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_owners.TryGetValue(args.User, out var work))
            return;

        var user = args.User;
        var crateUid = crate.Owner;
        EntityUid? selected = null;
        var closest = float.MaxValue;
        foreach (var robot in work.Robots)
        {
            if (!TryComp<CMUConstructionRobotComponent>(robot, out var comp) || comp.Operator != user)
                continue;

            if (_crateOrders.TryGetValue(robot, out var ordered) && ordered == crateUid)
            {
                selected = robot;
                break;
            }

            var distance = GetDistance(robot, Transform(crateUid).Coordinates);
            if (distance < closest)
            {
                closest = distance;
                selected = robot;
            }
        }

        if (selected is not { } robotUid)
            return;

        var dropping = _crateOrders.TryGetValue(robotUid, out var current) && current == crateUid;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(dropping ? "cmu-construction-robot-drop-crate" : "cmu-construction-robot-pull-crate"),
            Act = () =>
            {
                if (!TryComp<CMUConstructionRobotComponent>(robotUid, out var robot) || robot.Operator != user ||
                    !_interaction.InRangeAndAccessible(user, crateUid))
                    return;

                if (dropping)
                {
                    if (_crateOrders.TryGetValue(robotUid, out var order) && order == crateUid)
                        ClearCrateOrder(robotUid);
                    return;
                }

                if (!TryComp<PullableComponent>(crateUid, out var pullable) ||
                    pullable.Puller is { } puller && puller != robotUid)
                {
                    _popup.PopupEntity(Loc.GetString("cmu-construction-robot-crate-unavailable"), crateUid, user);
                    return;
                }

                ClearCrateOrder(robotUid);
                _crateOrders[robotUid] = crateUid;
            },
        });
    }

    private bool UpdateCrateOrder(Entity<CMUConstructionRobotComponent> robot)
    {
        if (!_crateOrders.TryGetValue(robot.Owner, out var crate))
            return false;

        if (TerminatingOrDeleted(crate) || !TryComp<PullableComponent>(crate, out var pullable) ||
            pullable.Puller is { } puller && puller != robot.Owner)
        {
            ClearCrateOrder(robot.Owner);
            return false;
        }

        if (pullable.Puller == robot.Owner)
            return false;

        if (_crateRetryAfter.TryGetValue(robot.Owner, out var retryAt))
        {
            if (_timing.CurTime < retryAt)
                return false;

            _crateRetryAfter.Remove(robot.Owner);
        }

        ReleaseAssignedTask(robot.Owner);
        var coordinates = Transform(crate).Coordinates;
        if (GetDistance(robot.Owner, coordinates) > 1.2f)
        {
            SetFollowTarget(robot, coordinates, 0.7f);
            return true;
        }

        if (!_interaction.InRangeUnobstructed(robot.Owner, crate, 1.2f))
        {
            _crateRetryAfter[robot.Owner] = _timing.CurTime + RetryDelay;
            return false;
        }

        if (!_pulling.TryStartPull(robot.Owner, crate))
        {
            if (robot.Comp.Operator is { } owner)
                _popup.PopupEntity(Loc.GetString("cmu-construction-robot-crate-unavailable"), crate, owner);
            ClearCrateOrder(robot.Owner);
        }

        return false;
    }

    private void ClearCrateOrder(EntityUid robot)
    {
        _crateRetryAfter.Remove(robot);
        if (!_crateOrders.Remove(robot, out var crate) ||
            !TryComp<PullableComponent>(crate, out var pullable) || pullable.Puller != robot)
            return;

        _pulling.TryStopPull(crate, pullable, robot);
    }

    private bool TryOpenNearbyCrate(Entity<CMUConstructionRobotComponent> robot, ConstructionRobotGhostTask task)
    {
        _nearbyCrates.Clear();
        _lookup.GetEntitiesInRange(task.Coordinates, robot.Comp.MaterialPickupRadius, _nearbyCrates, LookupFlags.Uncontained);
        EntityUid? closestCrate = null;
        var closest = float.MaxValue;
        foreach (var (uid, _) in _nearbyCrates)
        {
            if (task.CheckedCrates.Contains(uid) ||
                !TryComp<EntityStorageComponent>(uid, out var storage) || storage.Open ||
                _containers.IsEntityInContainer(uid) || GetDistance(uid, task.Coordinates) > robot.Comp.MaterialPickupRadius ||
                IsDestinationBlocked(task, robot.Owner, Transform(uid).Coordinates))
                continue;

            var distance = GetDistance(robot.Owner, Transform(uid).Coordinates);
            if (distance >= closest)
                continue;

            closest = distance;
            closestCrate = uid;
        }

        if (closestCrate is not { } crate)
            return false;

        if (closest > 1.2f)
        {
            AssignTask(task, robot.Owner);
            SetFollowTarget(robot, Transform(crate).Coordinates, 0.7f);
            return true;
        }

        if (!_interaction.InRangeUnobstructed(robot.Owner, crate, 1.2f))
        {
            task.BlockedDestinations[(robot.Owner, Transform(crate).Coordinates)] = _timing.CurTime + RetryDelay;
            return false;
        }

        task.CheckedCrates.Add(crate);
        if (!_entityStorage.TryOpenStorage(robot.Owner, crate, silent: true))
            return false;

        AssignTask(task, robot.Owner);
        return true;
    }
}

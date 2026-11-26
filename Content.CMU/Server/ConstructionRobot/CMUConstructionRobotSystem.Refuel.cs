using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.CMU14.ConstructionRobot;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;

namespace Content.Server.CMU14.ConstructionRobot;

public sealed partial class CMUConstructionRobotSystem
{
    private const float AutoRefuelRadius = 5f;
    private readonly HashSet<Entity<ReagentTankComponent>> _nearbyFuelTanks = [];

    private void UpdateFuelWarning(Entity<CMUConstructionRobotComponent> robot, EntityUid owner)
    {
        if (robot.Comp.FuelWarningThreshold <= 0 ||
            robot.Comp.WeldingFuel >= robot.Comp.FuelCapacity * robot.Comp.FuelWarningThreshold / 100f)
        {
            robot.Comp.FuelWarningSent = false;
            return;
        }

        if (robot.Comp.FuelWarningSent)
            return;

        robot.Comp.FuelWarningSent = true;
        _popup.PopupEntity(Loc.GetString("cmu-construction-robot-fuel-warning",
            ("robot", Name(robot)), ("fuel", (int) robot.Comp.WeldingFuel)), owner, owner);
    }

    private bool UpdateAutoRefuel(Entity<CMUConstructionRobotComponent> robot)
    {
        if (robot.Comp.AutoRefuelThreshold <= 0 ||
            robot.Comp.WeldingFuel >= robot.Comp.FuelCapacity * robot.Comp.AutoRefuelThreshold / 100f)
        {
            robot.Comp.AutoRefuelTarget = null;
            return false;
        }

        _nearbyFuelTanks.Clear();
        _lookup.GetEntitiesInRange(Transform(robot.Owner).Coordinates, AutoRefuelRadius, _nearbyFuelTanks, LookupFlags.Uncontained);
        foreach (var (uid, retry) in robot.Comp.AutoRefuelRetries.ToArray())
        {
            if (retry <= _timing.CurTime || TerminatingOrDeleted(uid))
                robot.Comp.AutoRefuelRetries.Remove(uid);
        }

        EntityUid? nearest = null;
        var closest = float.MaxValue;
        foreach (var (uid, _) in _nearbyFuelTanks)
        {
            if (TerminatingOrDeleted(uid) || _containers.IsEntityInContainer(uid) ||
                robot.Comp.AutoRefuelRetries.ContainsKey(uid) ||
                !TryGetTankFuel(robot, uid, out _, out var amount) || amount <= FixedPoint2.Zero)
                continue;

            var distance = GetDistance(robot.Owner, Transform(uid).Coordinates);
            if (distance > AutoRefuelRadius || distance >= closest ||
                distance <= 1.2f && !_interaction.InRangeUnobstructed(robot.Owner, uid, 1.2f))
                continue;

            closest = distance;
            nearest = uid;
        }

        if (nearest is not { } tank)
        {
            robot.Comp.AutoRefuelTarget = null;
            return false;
        }

        ReleaseAssignedTask(robot.Owner);
        robot.Comp.RepairTarget = null;
        robot.Comp.AutoRefuelTarget = tank;
        if (closest > 1.2f)
        {
            SetFollowTarget(robot, Transform(tank).Coordinates, 0.7f);
            return true;
        }

        SetFollowTarget(robot, null, 0.8f);
        var doAfter = new DoAfterArgs(EntityManager, robot.Owner, robot.Comp.RefuelDelay,
            new CMUConstructionRobotAutoRefuelDoAfterEvent(), robot, target: tank, used: tank)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
        };
        if (_doAfter.TryStartDoAfter(doAfter, out var id))
        {
            robot.Comp.IsAutoRefueling = true;
            robot.Comp.AutoRefuelDoAfter = id;
            robot.Comp.AutoRefuelOperator = robot.Comp.Operator;
            return true;
        }

        robot.Comp.AutoRefuelTarget = null;
        robot.Comp.AutoRefuelRetries[tank] = _timing.CurTime + RetryDelay;
        return false;
    }

    private void OnRobotAutoRefuel(Entity<CMUConstructionRobotComponent> robot,
        ref CMUConstructionRobotAutoRefuelDoAfterEvent args)
    {
        robot.Comp.IsAutoRefueling = false;
        robot.Comp.AutoRefuelDoAfter = null;
        var owner = robot.Comp.AutoRefuelOperator;
        robot.Comp.AutoRefuelOperator = null;
        var tank = robot.Comp.AutoRefuelTarget;
        robot.Comp.AutoRefuelTarget = null;
        if (args.Cancelled || args.Handled || tank is not { } uid || args.Used != uid ||
            owner == null || robot.Comp.Operator != owner || robot.Comp.AutoRefuelThreshold <= 0 ||
            robot.Comp.WeldingFuel >= robot.Comp.FuelCapacity * robot.Comp.AutoRefuelThreshold / 100f ||
            TerminatingOrDeleted(uid) || _containers.IsEntityInContainer(uid) ||
            !_interaction.InRangeUnobstructed(robot.Owner, uid, 1.2f) || PauseForHazard(robot) ||
            !TryGetTankFuel(robot, uid, out var source, out var amount) || amount <= FixedPoint2.Zero)
            return;

        args.Handled = true;
        robot.Comp.WeldingFuel += _solutions.RemoveReagent(source, WeldingFuel, amount).Float();
        Dirty(robot);
    }
}

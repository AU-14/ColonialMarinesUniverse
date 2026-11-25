using System.Numerics;
using Content.Server._RMC14.Synth;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.ConstructionRobot;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ConstructionRobot;

public sealed partial class CMUConstructionRobotSystem
{
    [Dependency] private DamageableSystem _repairDamage = default!;
    [Dependency] private SynthSystem _synth = default!;
    [Dependency] private TagSystem _repairTags = default!;

    private static readonly ProtoId<TagPrototype> StructureTag = "Structure";
    private static readonly FixedPoint2 RepairAmount = FixedPoint2.New(15);
    private const float RepairFuelCost = 5f;
    private static readonly TimeSpan StructureRepairDelay = TimeSpan.FromSeconds(3);

    private readonly HashSet<EntityUid> _repairCandidates = [];

    private void InitializeRepair()
    {
        SubscribeLocalEvent<CMUConstructionRobotComponent, CMUConstructionRobotRepairDoAfterEvent>(OnRobotRepair);
    }

    private bool UpdateRepairWork(Entity<CMUConstructionRobotComponent> robot)
    {
        if (robot.Comp.RepairAreaCenter is { } area && !area.IsValid(EntityManager))
        {
            robot.Comp.RepairAreaCenter = null;
            Dirty(robot);
            return false;
        }

        if (robot.Comp.WeldingFuel < RepairFuelCost)
            return false;

        _repairCandidates.Clear();
        if (robot.Comp.RepairAreaCenter is { } center)
            _lookup.GetEntitiesInRange(center, robot.Comp.RepairAreaRadius, _repairCandidates);
        else
            _lookup.GetEntitiesInRange(robot.Owner, robot.Comp.RepairAreaRadius, _repairCandidates);

        EntityUid? target = null;
        var closest = float.MaxValue;
        var robotPosition = _transform.GetMapCoordinates(robot);
        foreach (var candidate in _repairCandidates)
        {
            if (_containers.IsEntityInContainer(candidate) || !CanRepairTarget(robot, candidate))
                continue;

            if (candidate == robot.Comp.RepairRetryTarget)
            {
                if (_timing.CurTime < robot.Comp.RepairRetryUntil)
                    continue;

                robot.Comp.RepairRetryTarget = null;
            }

            var position = _transform.GetMapCoordinates(candidate);
            if (position.MapId != robotPosition.MapId)
                continue;

            var distance = Vector2.DistanceSquared(robotPosition.Position, position.Position);
            if (distance <= 1.2f * 1.2f && !_interaction.InRangeUnobstructed(robot.Owner, candidate, 1.2f))
                continue;

            if (distance >= closest)
                continue;

            closest = distance;
            target = candidate;
        }

        if (target is not { } repairTarget)
        {
            robot.Comp.RepairTarget = null;
            return false;
        }

        ReleaseAssignedTask(robot.Owner);
        robot.Comp.RepairTarget = repairTarget;
        if (GetDistance(robot.Owner, Transform(repairTarget).Coordinates) > 1.2f)
        {
            SetFollowTarget(robot, Transform(repairTarget).Coordinates, 0.7f);
            return true;
        }

        if (!_interaction.InRangeUnobstructed(robot.Owner, repairTarget, 1.2f))
            return true;

        var delay = TryComp<SynthComponent>(repairTarget, out var synth)
            ? _synth.GetRepairTime((repairTarget, synth), robot.Owner)
            : StructureRepairDelay;
        var doAfter = new DoAfterArgs(EntityManager, robot.Owner, delay,
            new CMUConstructionRobotRepairDoAfterEvent(), robot, target: repairTarget)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter, out var doAfterId))
            return true;

        robot.Comp.IsRepairing = true;
        robot.Comp.RepairDoAfter = doAfterId;
        return true;
    }

    private bool CanRepairTarget(Entity<CMUConstructionRobotComponent> robot, EntityUid target)
    {
        if (target == robot.Owner || TerminatingOrDeleted(target) ||
            !TryComp(target, out DamageableComponent? damageable))
            return false;

        var synthetic = TryComp<SynthComponent>(target, out var synth);
        if (synthetic)
        {
            if (synth.WelderDamageToRepair == null ||
                _repairDamage.GetPositiveDamage((target, damageable), synth.WelderDamageGroup).GetTotal() <= FixedPoint2.Zero)
                return false;
        }
        else if (_repairDamage.GetTotalDamage((target, damageable)) <= FixedPoint2.Zero)
        {
            return false;
        }

        if ((robot.Comp.RepairAreaCenter == null && !synthetic) ||
            (robot.Comp.RepairAreaCenter != null && !synthetic && !_repairTags.HasTag(target, StructureTag)))
            return false;

        if (robot.Comp.RepairAreaCenter is { } center &&
            GetDistance(target, center) > robot.Comp.RepairAreaRadius)
            return false;

        return true;
    }

    private void OnRobotRepair(
        Entity<CMUConstructionRobotComponent> robot,
        ref CMUConstructionRobotRepairDoAfterEvent args)
    {
        robot.Comp.IsRepairing = false;
        robot.Comp.RepairDoAfter = null;
        var target = robot.Comp.RepairTarget;
        robot.Comp.RepairTarget = null;
        if (args.Cancelled || target is not { } repairTarget || PauseForHazard(robot) ||
            robot.Comp.Operator == null || robot.Comp.WeldingFuel < RepairFuelCost ||
            !CanRepairTarget(robot, repairTarget) ||
            !_interaction.InRangeUnobstructed(robot.Owner, repairTarget, 1.2f) ||
            !TryComp(repairTarget, out DamageableComponent? damageable))
            return;

        if (TryComp<SynthComponent>(repairTarget, out var synth))
        {
            if (synth.WelderDamageToRepair is not { } repair)
                return;

            _repairDamage.TryChangeDamage(repairTarget, repair, ignoreResistances: true,
                interruptsDoAfters: false, damageable: damageable, origin: robot.Owner);
        }
        else
            _repairDamage.HealEvenly((repairTarget, damageable), -RepairAmount, origin: robot.Owner);
        robot.Comp.RepairRetryTarget = null;
        robot.Comp.WeldingFuel -= RepairFuelCost;
        Dirty(robot);
    }
}

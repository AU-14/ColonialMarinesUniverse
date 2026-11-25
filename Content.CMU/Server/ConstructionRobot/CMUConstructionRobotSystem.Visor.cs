using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.ConstructionRobot;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.ConstructionRobot;

public sealed partial class CMUConstructionRobotSystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ViewSubscriberSystem _views = default!;

    private readonly Dictionary<EntityUid, (EntityUid Viewer, EntityUid Robot, EntityUid? Previous, ICommonSession Session)> _visorViews = [];

    private void InitializeVisor()
    {
        SubscribeLocalEvent<CMUSmartWeldingVisorComponent, CMUConstructionRobotStatusActionEvent>(OnVisorStatus);
        SubscribeLocalEvent<CMUSmartWeldingVisorComponent, CMUConstructionRobotHoldActionEvent>(OnVisorHold);
        SubscribeLocalEvent<CMUSmartWeldingVisorComponent, CMUConstructionRobotRepairAreaActionEvent>(OnVisorRepairArea);
        SubscribeLocalEvent<CMUSmartWeldingVisorComponent, GotUnequippedEvent>(OnVisorUnequipped);
        SubscribeLocalEvent<CMUSmartWeldingVisorComponent, ComponentShutdown>(OnVisorShutdown);
        Subs.BuiEvents<CMUSmartWeldingVisorComponent>(CMUConstructionRobotUiKey.Key,
            subs =>
            {
                subs.Event<CMUConstructionRobotCommandMessage>(OnVisorCommand);
                subs.Event<CMUConstructionRobotJobCommandMessage>(OnVisorJobCommand);
                subs.Event<CMUConstructionRobotRefuelThresholdMessage>(OnVisorRefuelThreshold);
            });
    }

    private bool IsWearingVisor(EntityUid user, EntityUid visor)
        => _inventory.TryGetSlotEntity(user, "eyes", out var equipped) && equipped == visor;

    private bool HasWornVisor(EntityUid user)
        => _inventory.TryGetSlotEntity(user, "eyes", out var equipped) &&
           HasComp<CMUSmartWeldingVisorComponent>(equipped);

    private EntityUid? SelectedRobot(Entity<CMUSmartWeldingVisorComponent> visor, EntityUid user)
    {
        if (visor.Comp.SelectedRobot is { } selected &&
            TryComp<CMUConstructionRobotComponent>(selected, out var selectedComp) && selectedComp.Operator == user)
            return selected;

        visor.Comp.SelectedRobot = null;
        if (!_owners.TryGetValue(user, out var work))
            return null;

        var closest = float.MaxValue;
        foreach (var robot in work.Robots)
        {
            if (!TryComp<CMUConstructionRobotComponent>(robot, out var component) || component.Operator != user)
                continue;

            var distance = GetDistance(robot, Transform(user).Coordinates);
            if (distance >= closest)
                continue;
            closest = distance;
            visor.Comp.SelectedRobot = robot;
        }

        return visor.Comp.SelectedRobot;
    }

    private void OnVisorStatus(Entity<CMUSmartWeldingVisorComponent> visor, ref CMUConstructionRobotStatusActionEvent args)
    {
        if (args.Handled || !IsWearingVisor(args.Performer, visor))
            return;

        args.Handled = true;
        UpdateVisor(visor, args.Performer);
        _ui.TryToggleUi(visor.Owner, CMUConstructionRobotUiKey.Key, args.Performer);
    }

    private void OnVisorHold(Entity<CMUSmartWeldingVisorComponent> visor, ref CMUConstructionRobotHoldActionEvent args)
    {
        if (args.Handled || !IsWearingVisor(args.Performer, visor) ||
            SelectedRobot(visor, args.Performer) is not { } robot ||
            !args.Target.IsValid(EntityManager) || !float.IsFinite(args.Target.X) || !float.IsFinite(args.Target.Y))
            return;

        var point = _transform.ToMapCoordinates(args.Target);
        if (point.MapId != _transform.GetMapCoordinates(args.Performer).MapId ||
            !_examine.InRangeUnOccluded(args.Performer, point, 20f))
            return;

        args.Handled = true;
        var component = Comp<CMUConstructionRobotComponent>(robot);
        component.IdlePosition = args.Target;
        component.RepairAreaCenter = null;
        Dirty(robot, component);
        _popup.PopupEntity(Loc.GetString("cmu-construction-robot-hold-ordered"), robot, args.Performer);
    }

    private void OnVisorRepairArea(
        Entity<CMUSmartWeldingVisorComponent> visor,
        ref CMUConstructionRobotRepairAreaActionEvent args)
    {
        if (args.Handled || !IsWearingVisor(args.Performer, visor) ||
            SelectedRobot(visor, args.Performer) is not { } robot ||
            !args.Target.IsValid(EntityManager) || !float.IsFinite(args.Target.X) || !float.IsFinite(args.Target.Y))
            return;

        var point = _transform.ToMapCoordinates(args.Target);
        if (point.MapId != _transform.GetMapCoordinates(args.Performer).MapId ||
            !_examine.InRangeUnOccluded(args.Performer, point, 20f))
            return;

        args.Handled = true;
        var component = Comp<CMUConstructionRobotComponent>(robot);
        component.RepairAreaCenter = args.Target;
        component.IdlePosition = null;
        Dirty(robot, component);
        _popup.PopupEntity(Loc.GetString("cmu-construction-robot-repair-area-ordered"), robot, args.Performer);
    }

    private void OnVisorCommand(Entity<CMUSmartWeldingVisorComponent> visor, ref CMUConstructionRobotCommandMessage args)
    {
        if (!IsWearingVisor(args.Actor, visor) || !TryGetEntity(args.Robot, out var uid) ||
            !TryComp<CMUConstructionRobotComponent>(uid, out var robot) || robot.Operator != args.Actor)
            return;

        if (args.Command == CMUConstructionRobotCommand.Select)
        {
            StopVisorView(visor.Owner);
            visor.Comp.SelectedRobot = uid;
        }
        else if (SelectedRobot(visor, args.Actor) != uid)
            return;
        else
        {
            switch (args.Command)
            {
                case CMUConstructionRobotCommand.Follow:
                    robot.IdlePosition = null;
                    robot.RepairAreaCenter = null;
                    Dirty(uid.Value, robot);
                    break;
                case CMUConstructionRobotCommand.Hold:
                    robot.IdlePosition = Transform(uid.Value).Coordinates;
                    robot.RepairAreaCenter = null;
                    Dirty(uid.Value, robot);
                    break;
                case CMUConstructionRobotCommand.ClearRepairArea:
                    robot.RepairAreaCenter = null;
                    Dirty(uid.Value, robot);
                    break;
                case CMUConstructionRobotCommand.Camera:
                    if (_visorViews.ContainsKey(visor))
                        StopVisorView(visor.Owner);
                    else
                        StartVisorView(visor.Owner, args.Actor, uid.Value);
                    break;
                case CMUConstructionRobotCommand.AvoidHazards:
                    robot.AvoidHazards = !robot.AvoidHazards;
                    break;
                case CMUConstructionRobotCommand.AvoidFire:
                    robot.AvoidFire = !robot.AvoidFire;
                    break;
                case CMUConstructionRobotCommand.AvoidHostiles:
                    robot.AvoidHostiles = !robot.AvoidHostiles;
                    break;
                case CMUConstructionRobotCommand.AvoidExplosives:
                    robot.AvoidExplosives = !robot.AvoidExplosives;
                    break;
                case CMUConstructionRobotCommand.AutomaticJobOrder:
                    if (_owners.TryGetValue(args.Actor, out var work))
                        work.ManualQueue = false;
                    break;
            }
        }

        UpdateVisor(visor, args.Actor);
    }

    private void StartVisorView(EntityUid visor, EntityUid viewer, EntityUid robot)
    {
        if (!TryComp<EyeComponent>(viewer, out var eye) || !TryComp<ActorComponent>(viewer, out var actor))
            return;

        _visorViews[visor] = (viewer, robot, eye.Target, actor.PlayerSession);
        _views.AddViewSubscriber(robot, actor.PlayerSession);
        _eye.SetTarget(viewer, robot, eye);
    }

    private void OnVisorJobCommand(Entity<CMUSmartWeldingVisorComponent> visor, ref CMUConstructionRobotJobCommandMessage args)
    {
        if (!IsWearingVisor(args.Actor, visor) || !TryGetEntity(args.Robot, out var uid) ||
            SelectedRobot(visor, args.Actor) != uid ||
            !TryComp<CMUConstructionRobotComponent>(uid, out var robot) || robot.Operator != args.Actor ||
            !_owners.TryGetValue(args.Actor, out var work) || !work.Ghosts.TryGetValue(args.JobId, out var job))
            return;

        switch (args.Command)
        {
            case CMUConstructionRobotJobCommand.Priority:
                job.Priority = job.Priority switch
                {
                    CMUConstructionRobotJobPriority.Low => CMUConstructionRobotJobPriority.Normal,
                    CMUConstructionRobotJobPriority.Normal => CMUConstructionRobotJobPriority.High,
                    _ => CMUConstructionRobotJobPriority.Low,
                };
                break;
            case CMUConstructionRobotJobCommand.Up:
            case CMUConstructionRobotJobCommand.Down:
                var ordered = work.Ghosts.Values.Where(task => task.Priority == job.Priority)
                    .OrderBy(task => task.QueueOrder).ToArray();
                var index = Array.IndexOf(ordered, job);
                var other = index + (args.Command == CMUConstructionRobotJobCommand.Up ? -1 : 1);
                if (other < 0 || other >= ordered.Length)
                    return;
                (job.QueueOrder, ordered[other].QueueOrder) = (ordered[other].QueueOrder, job.QueueOrder);
                work.ManualQueue = true;
                break;
            default:
                return;
        }

        UpdateVisor(visor, args.Actor);
    }

    private void OnVisorRefuelThreshold(Entity<CMUSmartWeldingVisorComponent> visor,
        ref CMUConstructionRobotRefuelThresholdMessage args)
    {
        if (!IsWearingVisor(args.Actor, visor) || !TryGetEntity(args.Robot, out var uid) ||
            SelectedRobot(visor, args.Actor) != uid || args.Threshold is < 0 or > 100 ||
            !TryComp<CMUConstructionRobotComponent>(uid, out var robot) || robot.Operator != args.Actor)
            return;

        robot.AutoRefuelThreshold = args.Threshold;
        if (robot.AutoRefuelDoAfter is { } refuel &&
            (args.Threshold == 0 || robot.WeldingFuel >= robot.FuelCapacity * args.Threshold / 100f))
            _doAfter.Cancel(refuel);
        UpdateVisor(visor, args.Actor);
    }

    private void StopVisorView(EntityUid visor)
    {
        if (!_visorViews.Remove(visor, out var view))
            return;

        _views.RemoveViewSubscriber(view.Robot, view.Session);
        if (!TerminatingOrDeleted(view.Viewer) && TryComp<EyeComponent>(view.Viewer, out var eye) && eye.Target == view.Robot)
            _eye.SetTarget(view.Viewer,
                view.Previous is { } previous && !TerminatingOrDeleted(previous) ? previous : null, eye);
    }

    private void OnVisorUnequipped(Entity<CMUSmartWeldingVisorComponent> visor, ref GotUnequippedEvent args)
    {
        StopVisorView(visor.Owner);
        _ui.CloseUi(visor.Owner, CMUConstructionRobotUiKey.Key);
        visor.Comp.SelectedRobot = null;
    }

    private void OnVisorShutdown(Entity<CMUSmartWeldingVisorComponent> visor, ref ComponentShutdown args)
        => StopVisorView(visor.Owner);

    private void StopRobotViews(EntityUid robot)
    {
        foreach (var visor in _visorViews.Where(pair => pair.Value.Robot == robot).Select(pair => pair.Key).ToArray())
            StopVisorView(visor);
    }

    private void UpdateVisors()
    {
        var query = EntityQueryEnumerator<CMUSmartWeldingVisorComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_visorViews.TryGetValue(uid, out var view) &&
                (!IsWearingVisor(view.Viewer, uid) || !TryComp<CMUConstructionRobotComponent>(view.Robot, out var robot) ||
                 robot.Operator != view.Viewer || !TryComp<ActorComponent>(view.Viewer, out var actor) ||
                 actor.PlayerSession != view.Session || !TryComp<EyeComponent>(view.Viewer, out var eye) ||
                 eye.Target != view.Robot))
                StopVisorView(uid);

            if (!_ui.IsUiOpen(uid, CMUConstructionRobotUiKey.Key))
                continue;

            var wearer = Transform(uid).ParentUid;
            if (!IsWearingVisor(wearer, uid))
            {
                _ui.CloseUi(uid, CMUConstructionRobotUiKey.Key);
                continue;
            }

            UpdateVisor((uid, component), wearer);
        }
    }

    private void UpdateVisor(Entity<CMUSmartWeldingVisorComponent> visor, EntityUid user)
    {
        var robots = new List<CMUConstructionRobotEntry>();
        var jobs = new List<CMUConstructionRobotJobEntry>();
        var selected = SelectedRobot(visor, user);
        if (_owners.TryGetValue(user, out var work))
        {
            foreach (var uid in work.Robots)
            {
                if (!TerminatingOrDeleted(uid) && TryComp<CMUConstructionRobotComponent>(uid, out var comp) && comp.Operator == user)
                    robots.Add(new CMUConstructionRobotEntry(GetNetEntity(uid), Name(uid)));
            }

            if (selected is { } selectedUid && TryComp<CMUConstructionRobotComponent>(selectedUid, out var selectedComp))
            {
                foreach (var task in work.Ghosts.Values.OrderByDescending(task => task.Priority).ThenBy(task => task.QueueOrder))
                {
                    if (!TryGetBuildPlan(task.Prototype, out var plan))
                        continue;
                    plan = ApplyConstructionSkill(selectedUid, plan);
                    var name = plan.Prototype.Name ?? plan.Prototype.SetName ?? task.Prototype;
                    var status = TryFindMissingMaterial((selectedUid, selectedComp), plan, out var type, out var amount)
                        ? Loc.GetString("cmu-construction-robot-job-missing", ("name", name), ("amount", amount), ("material", type))
                        : Loc.GetString("cmu-construction-robot-job-ready", ("name", name));
                    jobs.Add(new CMUConstructionRobotJobEntry(task.GhostId, status, task.Priority));
                }
            }
        }

        CMUConstructionRobotComponent? state = null;
        if (selected is { } robotUid)
            TryComp(robotUid, out state);
        _ui.SetUiState(visor.Owner, CMUConstructionRobotUiKey.Key, new CMUConstructionRobotUiState(
            robots, selected == null ? null : GetNetEntity(selected.Value), state?.IdlePosition == null,
            state?.RepairAreaCenter != null,
            _visorViews.ContainsKey(visor), state?.AvoidingHazard ?? false, state?.AvoidHazards ?? false,
            state?.AvoidFire ?? false, state?.AvoidHostiles ?? false, state?.AvoidExplosives ?? false,
            (int) (state?.WeldingFuel ?? 0), (int) (state?.FuelCapacity ?? 0), state?.AutoRefuelThreshold ?? 0, jobs));
    }
}

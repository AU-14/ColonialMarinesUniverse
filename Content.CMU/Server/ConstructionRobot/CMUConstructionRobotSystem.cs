using System.Numerics;
using Content.Server.Construction;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Shared._RMC14.Construction;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.ConstructionRobot;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.NPC;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Server.Player;

namespace Content.Server.CMU14.ConstructionRobot;

public sealed partial class CMUConstructionRobotSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private ConstructionSystem _construction = default!;

    private const string FollowCompoundTask = "CMUDroneFollowCompound";
    private const string FollowCloseRangeKey = "FollowCloseRange";
    private static readonly ReagentId WeldingFuel = new("WeldingFuel", null);

    private readonly List<ConstructionRobotGhostTask> _ghosts = new();
    private readonly List<EntityUid> _nearby = new();
    private readonly HashSet<(EntityUid Owner, int GhostId)> _warnedUnsupportedTasks = new();
    private TimeSpan _nextThink;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUConstructionRobotComponent, ComponentInit>(OnRobotInit);
        SubscribeLocalEvent<CMUConstructionRobotComponent, GetVerbsEvent<Verb>>(OnRobotGetVerbs);
        SubscribeLocalEvent<CMUConstructionRobotComponent, InteractUsingEvent>(OnRobotInteractUsing);
        SubscribeLocalEvent<CMUConstructionRobotComponent, CMUConstructionRobotRefuelDoAfterEvent>(OnRobotRefuel);
        SubscribeLocalEvent<CMUConstructionRobotComponent, CMUConstructionRobotBuildDoAfterEvent>(OnRobotBuild);
        SubscribeLocalEvent<CMUConstructionRobotComponent, ExaminedEvent>(OnRobotExamine);
        SubscribeLocalEvent<CMUConstructionRobotKitComponent, AfterInteractEvent>(OnRobotKitAfterInteract);
        SubscribeNetworkEvent<ConstructionRobotGhostPlacedMessage>(OnGhostPlaced);
        SubscribeNetworkEvent<ConstructionRobotGhostRemovedMessage>(OnGhostRemoved);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextThink)
            return;

        _nextThink = _timing.CurTime + TimeSpan.FromSeconds(0.5);

        var robots = EntityQueryEnumerator<CMUConstructionRobotComponent>();
        while (robots.MoveNext(out var uid, out var component))
            UpdateRobot((uid, component));
    }

    private void OnRobotInit(Entity<CMUConstructionRobotComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Materials = _containers.EnsureContainer<Container>(ent, ent.Comp.MaterialsContainerId);
        EnsureFollowMovement(ent.Owner);
        EnsureComp<HTNComponent>(ent.Owner);
    }

    private void OnRobotGetVerbs(Entity<CMUConstructionRobotComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var previousOwner = ent.Comp.Owner;
        if (previousOwner is { } owner && owner != args.User)
            return;

        var taking = previousOwner == null;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(taking
                ? "cmu-construction-robot-verb-take"
                : "cmu-construction-robot-verb-release"),
            Act = () =>
            {
                if (!TryComp(ent.Owner, out CMUConstructionRobotComponent? current) ||
                    current.Owner != previousOwner)
                    return;

                if (!taking)
                    ReleaseAssignedTasks(ent.Owner);

                current.Owner = taking ? args.User : null;
                Dirty(ent.Owner, current);
                UpdateRobot((ent.Owner, current));
            },
        });
    }

    private void OnRobotInteractUsing(Entity<CMUConstructionRobotComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !TryComp<ReagentTankComponent>(args.Used, out var tank) ||
            tank.TankType != ReagentTankType.Fuel ||
            !_solutions.TryGetSolution(args.Used, "tank", out _, out var solution) ||
            !solution.TryGetReagentQuantity(WeldingFuel, out var available) ||
            available <= FixedPoint2.Zero)
            return;

        args.Handled = true;
        if (ent.Comp.WeldingFuel >= ent.Comp.FuelCapacity)
        {
            _popup.PopupEntity(Loc.GetString("cmu-construction-robot-fuel-full"), ent.Owner, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.RefuelDelay,
            new CMUConstructionRobotRefuelDoAfterEvent(),
            ent,
            ent,
            used: args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnRobotRefuel(
        Entity<CMUConstructionRobotComponent> ent,
        ref CMUConstructionRobotRefuelDoAfterEvent args)
    {
        if (args.Cancelled ||
            args.Used is not { } tank ||
            !TryComp<ReagentTankComponent>(tank, out var tankComp) ||
            tankComp.TankType != ReagentTankType.Fuel ||
            !_solutions.TryGetSolution(tank, "tank", out var solutionEntity, out var solution) ||
            !solution.TryGetReagentQuantity(WeldingFuel, out var available))
            return;

        var amount = Math.Min(available.Float(), ent.Comp.FuelCapacity - ent.Comp.WeldingFuel);
        if (amount <= 0)
            return;

        var removed = _solutions.RemoveReagent(solutionEntity, "WeldingFuel", FixedPoint2.New(amount));
        ent.Comp.WeldingFuel += removed.Float();
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("cmu-construction-robot-refuel-finished"), ent.Owner, args.User);
    }

    private void OnRobotExamine(Entity<CMUConstructionRobotComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(
            "cmu-construction-robot-examine-fuel",
            ("fuel", MathF.Floor(ent.Comp.WeldingFuel)),
            ("capacity", ent.Comp.FuelCapacity)));

        if (ent.Comp.Owner is { } owner && Exists(owner))
            args.PushMarkup(Loc.GetString("cmu-construction-robot-examine-owner", ("owner", owner)));
    }

    private void OnRobotKitAfterInteract(Entity<CMUConstructionRobotKitComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !args.ClickLocation.IsValid(EntityManager))
            return;

        var robot = Spawn(ent.Comp.RobotPrototype, _transform.ToMapCoordinates(args.ClickLocation));
        QueueDel(ent.Owner);
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("cmu-construction-robot-deployed"), robot, args.User);
    }

    private void OnGhostPlaced(ConstructionRobotGhostPlacedMessage ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } owner ||
            !_prototypes.TryIndex<ConstructionPrototype>(ev.Prototype, out _) ||
            _ghosts.Any(task => task.Owner == owner && task.GhostId == ev.GhostId))
            return;

        var coordinates = GetCoordinates(ev.Location);
        var ownerPosition = _transform.GetMapCoordinates(owner);
        var ghostPosition = _transform.ToMapCoordinates(coordinates);
        if (ownerPosition.MapId != ghostPosition.MapId ||
            Vector2.Distance(ownerPosition.Position, ghostPosition.Position) > 20f)
            return;

        _ghosts.Add(new ConstructionRobotGhostTask(owner, ev.GhostId, ev.Prototype, coordinates, ev.Rotation));
    }

    private void OnGhostRemoved(ConstructionRobotGhostRemovedMessage ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } owner)
            return;

        _ghosts.RemoveAll(task => task.Owner == owner && task.GhostId == ev.GhostId);
        _warnedUnsupportedTasks.Remove((owner, ev.GhostId));
    }

    private void UpdateRobot(Entity<CMUConstructionRobotComponent> robot)
    {
        if (robot.Comp.IsBuilding)
            return;

        if (robot.Comp.Owner is not { } owner || TerminatingOrDeleted(owner))
        {
            ReleaseAssignedTasks(robot.Owner);
            robot.Comp.Owner = null;
            SetFollowTarget(robot, null, 0.8f);
            return;
        }

        var tasks = _ghosts
            .Where(task => task.Owner == owner)
            .OrderBy(task => GetDistance(robot.Owner, task.Coordinates))
            .ToArray();

        foreach (var task in tasks)
        {
            if (task.AssignedRobot is { } assigned && assigned != robot.Owner)
                continue;

            if (!TryGetBuildPlan(task, owner, out var plan))
            {
                if (_warnedUnsupportedTasks.Add((task.Owner, task.GhostId)))
                    _popup.PopupEntity(Loc.GetString("cmu-construction-robot-unsupported-plan"), owner, owner);
                continue;
            }

            if (robot.Comp.WeldingFuel < plan.WeldingFuel)
                continue;

            if (TryFindMissingMaterial(robot, plan, out var needed, out var missingAmount))
            {
                if (!TryFindNearbyMaterial(task, needed, robot.Comp.MaterialPickupRadius, out var material))
                    continue;

                if (robot.Comp.Materials!.ContainedEntities.Count >= robot.Comp.MaxMaterialStacks)
                {
                    continue;
                }

                if (GetDistance(robot.Owner, Transform(material).Coordinates) > 1.2f)
                {
                    AssignTask(task, robot.Owner);
                    SetFollowTarget(robot, Transform(material).Coordinates, 0.7f);
                    return;
                }

                var pickup = material;
                if (TryComp<StackComponent>(material, out var stack) && stack.Count > missingAmount)
                    pickup = _stacks.Split(
                        (material, stack),
                        missingAmount,
                        Transform(robot.Owner).Coordinates,
                        robot.Owner) ?? material;

                if (_containers.Insert(pickup, robot.Comp.Materials))
                {
                    AssignTask(task, robot.Owner);
                    return;
                }

                continue;
            }

            if (GetDistance(robot.Owner, task.Coordinates) > 1.2f)
            {
                AssignTask(task, robot.Owner);
                SetFollowTarget(robot, task.Coordinates, 0.7f);
                return;
            }

            AssignTask(task, robot.Owner);
            StartBuild(robot, task, plan);
            return;
        }

        ReleaseAssignedTasks(robot.Owner);
        SetFollowTarget(robot, new EntityCoordinates(owner, Vector2.Zero), 0.8f);
    }

    private void AssignTask(ConstructionRobotGhostTask task, EntityUid robot)
    {
        ReleaseAssignedTasks(robot, task);
        task.AssignedRobot = robot;
    }

    private void ReleaseAssignedTasks(EntityUid robot, ConstructionRobotGhostTask? except = null)
    {
        foreach (var task in _ghosts)
        {
            if (task != except && task.AssignedRobot == robot)
                task.AssignedRobot = null;
        }
    }

    private bool TryFindMissingMaterial(
        Entity<CMUConstructionRobotComponent> robot,
        ConstructionRobotBuildPlan plan,
        out string missingType,
        out int missingAmount)
    {
        foreach (var (type, amount) in plan.Materials)
        {
            var stored = 0;
            foreach (var item in robot.Comp.Materials!.ContainedEntities)
            {
                if (TryComp<StackComponent>(item, out var stack) && stack.StackTypeId.Id == type)
                    stored += stack.Count;
            }

            if (stored >= amount)
                continue;

            missingType = type;
            missingAmount = amount - stored;
            return true;
        }

        missingType = string.Empty;
        missingAmount = 0;
        return false;
    }

    private bool TryFindNearbyMaterial(
        ConstructionRobotGhostTask task,
        string type,
        float radius,
        out EntityUid stackEntity)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(_transform.ToMapCoordinates(task.Coordinates), radius, _nearby);
        foreach (var entity in _nearby)
        {
            if (!TryComp<StackComponent>(entity, out var stack) ||
                stack.StackTypeId.Id != type ||
                _containers.IsEntityInContainer(entity))
                continue;

            stackEntity = entity;
            return true;
        }

        stackEntity = default;
        return false;
    }

    private void StartBuild(
        Entity<CMUConstructionRobotComponent> robot,
        ConstructionRobotGhostTask task,
        ConstructionRobotBuildPlan plan)
    {
        var doAfter = new DoAfterArgs(
            EntityManager,
            robot.Owner,
            plan.BuildDelay,
            new CMUConstructionRobotBuildDoAfterEvent(),
            robot,
            robot)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        SetFollowTarget(robot, null, 0.8f);
        robot.Comp.IsBuilding = true;
        robot.Comp.ActiveGhostId = task.GhostId;
        robot.Comp.CurrentFollowTarget = null;
        Dirty(robot);
    }

    private void OnRobotBuild(
        Entity<CMUConstructionRobotComponent> ent,
        ref CMUConstructionRobotBuildDoAfterEvent args)
    {
        ent.Comp.IsBuilding = false;
        var ghostId = ent.Comp.ActiveGhostId;
        ent.Comp.ActiveGhostId = null;

        if (args.Cancelled ||
            ghostId is not { } id ||
            ent.Comp.Owner is not { } owner)
            return;

        var task = _ghosts.FirstOrDefault(candidate => candidate.Owner == owner && candidate.GhostId == id);
        if (task == null ||
            !TryGetBuildPlan(task, owner, out var plan) ||
            ent.Comp.WeldingFuel < plan.WeldingFuel ||
            _whitelist.IsWhitelistFail(plan.Prototype.EntityWhitelist, owner) ||
            !HasRequiredMaterials(ent, plan))
            return;

        var attempt = new RMCConstructionAttemptEvent(task.Coordinates, plan.Prototype, User: owner);
        RaiseLocalEvent(ref attempt);
        if (attempt.Cancelled ||
            plan.Prototype.Conditions.Any(condition =>
                !condition.Condition(owner, task.Coordinates, task.Rotation.GetDir())))
            return;

        if (!ConsumeMaterials(ent, plan))
            return;

        ent.Comp.WeldingFuel -= plan.WeldingFuel;
        Dirty(ent);

        var built = SpawnAttachedTo(plan.TargetPrototype, task.Coordinates, rotation: task.Rotation);
        _construction.PerformActions(built, owner, plan.FinalNode.Actions);

        var completed = new ConstructionCompletedEvent(built, owner);
        RaiseLocalEvent(built, ref completed, broadcast: true);

        _ghosts.RemoveAll(candidate => candidate.Owner == owner && candidate.GhostId == id);
        _warnedUnsupportedTasks.Remove((owner, id));
        if (_players.TryGetSessionByEntity(owner, out var session))
            RaiseNetworkEvent(new AckStructureConstructionMessage(id), session.Channel);
    }

    private bool HasRequiredMaterials(Entity<CMUConstructionRobotComponent> robot, ConstructionRobotBuildPlan plan)
    {
        foreach (var (type, amount) in plan.Materials)
        {
            var stored = 0;
            foreach (var item in robot.Comp.Materials!.ContainedEntities)
            {
                if (TryComp<StackComponent>(item, out var stack) && stack.StackTypeId.Id == type)
                    stored += stack.Count;
            }

            if (stored < amount)
                return false;
        }

        return true;
    }

    private bool ConsumeMaterials(Entity<CMUConstructionRobotComponent> robot, ConstructionRobotBuildPlan plan)
    {
        foreach (var (type, amount) in plan.Materials)
        {
            var remaining = amount;
            foreach (var item in robot.Comp.Materials!.ContainedEntities.ToArray())
            {
                if (!TryComp<StackComponent>(item, out var stack) || stack.StackTypeId.Id != type)
                    continue;

                var used = Math.Min(remaining, stack.Count);
                if (used == stack.Count)
                    QueueDel(item);
                else
                    _stacks.SetCount(item, stack.Count - used, stack);

                remaining -= used;
                if (remaining <= 0)
                    break;
            }

            if (remaining > 0)
                return false;
        }

        return true;
    }

    private bool TryGetBuildPlan(
        ConstructionRobotGhostTask task,
        EntityUid owner,
        out ConstructionRobotBuildPlan plan)
    {
        plan = default!;
        if (!_prototypes.TryIndex<ConstructionPrototype>(task.Prototype, out var prototype) ||
            !_prototypes.TryIndex(prototype.Graph, out ConstructionGraphPrototype? graph) ||
            !graph.Nodes.TryGetValue(prototype.StartNode, out var startNode) ||
            !TryFindUniquePath(graph, startNode, prototype.TargetNode, out var path) ||
            path.Count == 0)
            return false;

        var materials = new Dictionary<string, int>();
        var delay = 0f;
        var fuel = 0f;
        var current = startNode;
        for (var i = 0; i < path.Count; i++)
        {
            var next = path[i];
            var edge = current.GetEdge(next.Name);
            if (edge == null || edge.Conditions.Count > 0 || edge.Completed.Count > 0)
                return false;

            foreach (var step in edge.Steps)
            {
                if (step.Completed.Count > 0)
                    return false;

                delay += step.DoAfter;
                if (step is MaterialConstructionGraphStep material)
                {
                    materials[material.MaterialPrototypeId.Id] =
                        materials.GetValueOrDefault(material.MaterialPrototypeId.Id) + material.Amount;
                }
                else if (step is ToolConstructionGraphStep tool)
                {
                    if (tool.Tool.Id == "Welding")
                        fuel += tool.Fuel;
                }
                else
                {
                    return false;
                }
            }

            if (i < path.Count - 1 && next.Actions.Count > 0)
                return false;

            current = next;
        }

        var targetPrototype = current.Entity.GetId(null, owner, new(EntityManager));
        if (targetPrototype == null || !_prototypes.TryIndex<EntityPrototype>(targetPrototype, out _))
            return false;

        plan = new ConstructionRobotBuildPlan(
            prototype,
            current,
            targetPrototype,
            materials,
            fuel,
            TimeSpan.FromSeconds(Math.Max(1f, delay)));
        return true;
    }

    private bool TryFindUniquePath(
        ConstructionGraphPrototype graph,
        ConstructionGraphNode start,
        string target,
        out List<ConstructionGraphNode> path)
    {
        var paths = new List<List<ConstructionGraphNode>>(2);
        FindPaths(graph, start, target, new List<ConstructionGraphNode>(), new HashSet<string> { start.Name }, paths);
        if (paths.Count != 1)
        {
            path = new List<ConstructionGraphNode>();
            return false;
        }

        path = paths[0];
        return true;
    }

    private void FindPaths(
        ConstructionGraphPrototype graph,
        ConstructionGraphNode current,
        string target,
        List<ConstructionGraphNode> path,
        HashSet<string> visited,
        List<List<ConstructionGraphNode>> paths)
    {
        if (paths.Count > 1)
            return;

        if (current.Name == target)
        {
            paths.Add(new List<ConstructionGraphNode>(path));
            return;
        }

        foreach (var edge in current.Edges)
        {
            if (!graph.Nodes.TryGetValue(edge.Target, out var next) || !visited.Add(next.Name))
                continue;

            path.Add(next);
            FindPaths(graph, next, target, path, visited, paths);
            path.RemoveAt(path.Count - 1);
            visited.Remove(next.Name);
        }
    }

    private void SetFollowTarget(
        Entity<CMUConstructionRobotComponent> robot,
        EntityCoordinates? target,
        float closeRange)
    {
        if (robot.Comp.CurrentFollowTarget == target)
            return;

        var htn = EnsureComp<HTNComponent>(robot.Owner);
        if (htn.RootTask == null || htn.RootTask.Task != FollowCompoundTask)
            htn.RootTask = new HTNCompoundTask { Task = FollowCompoundTask };

        if (target == null)
        {
            _htn.SetHTNEnabled((robot.Owner, htn), false);
            _npc.SleepNPC(robot.Owner, htn);
            ClearFollowBlackboard(htn);
            robot.Comp.CurrentFollowTarget = null;
            return;
        }

        EnsureFollowMovement(robot.Owner);
        htn.Blackboard.SetValue(NPCBlackboard.Owner, robot.Owner);
        htn.Blackboard.SetValue(NPCBlackboard.OwnerCoordinates, Transform(robot.Owner).Coordinates);
        _npc.SetBlackboard(robot.Owner, NPCBlackboard.FollowTarget, target.Value, htn);
        htn.Blackboard.SetValue(FollowCloseRangeKey, closeRange);
        robot.Comp.CurrentFollowTarget = target;
        _htn.SetHTNEnabled((robot.Owner, htn), true);
        _npc.WakeNPC(robot.Owner, htn);
        _htn.Replan(htn);
    }

    private void EnsureFollowMovement(EntityUid robot)
    {
        EnsureComp<InputMoverComponent>(robot);
        EnsureComp<MobMoverComponent>(robot);
    }

    private static void ClearFollowBlackboard(HTNComponent htn)
    {
        htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);
        htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.OwnerCoordinates);
        htn.Blackboard.Remove<float>(FollowCloseRangeKey);
    }

    private float GetDistance(EntityUid entity, EntityCoordinates coordinates)
    {
        var entityPosition = _transform.GetMapCoordinates(entity);
        var targetPosition = _transform.ToMapCoordinates(coordinates);
        if (entityPosition.MapId != targetPosition.MapId)
            return float.MaxValue;

        return Vector2.Distance(entityPosition.Position, targetPosition.Position);
    }

    private sealed class ConstructionRobotGhostTask(
        EntityUid owner,
        int ghostId,
        string prototype,
        EntityCoordinates coordinates,
        Angle rotation)
    {
        public EntityUid Owner { get; } = owner;
        public int GhostId { get; } = ghostId;
        public string Prototype { get; } = prototype;
        public EntityCoordinates Coordinates { get; } = coordinates;
        public Angle Rotation { get; } = rotation;
        public EntityUid? AssignedRobot { get; set; }
    }

    private sealed record ConstructionRobotBuildPlan(
        ConstructionPrototype Prototype,
        ConstructionGraphNode FinalNode,
        string TargetPrototype,
        Dictionary<string, int> Materials,
        float WeldingFuel,
        TimeSpan BuildDelay);
}

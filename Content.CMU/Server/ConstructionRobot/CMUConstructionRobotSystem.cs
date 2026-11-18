using System.Linq;
using System.Numerics;
using Content.Server.Construction;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.ConstructionRobot;
using Content.Shared.Construction;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.NPC;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.ConstructionRobot;

public sealed partial class CMUConstructionRobotSystem : EntitySystem
{
    [Dependency] private ConstructionSystem _construction = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private RMCConstructionSystem _rmcConstruction = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    private const string FollowCompoundTask = "CMUDroneFollowCompound";
    private const string FollowCloseRangeKey = "FollowCloseRange";
    private static readonly ReagentId WeldingFuel = new("WeldingFuel", null);

    private readonly List<(float Distance, ConstructionRobotGhostTask Task)> _candidates = [];
    private readonly HashSet<Entity<StackComponent>> _nearby = new();
    private readonly Dictionary<EntityUid, (ICommonSession Session, ConstructionRobotGhostTask Task)> _activeBuilds = new();
    private TimeSpan _nextThink;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUConstructionRobotComponent, ComponentInit>(OnRobotInit);
        SubscribeLocalEvent<CMUConstructionRobotComponent, GetVerbsEvent<Verb>>(OnRobotGetVerbs);
        SubscribeLocalEvent<CMUConstructionRobotComponent, DragDropTargetEvent>(OnRobotDragDrop);
        SubscribeLocalEvent<CMUConstructionRobotComponent, CMUConstructionRobotRefuelDoAfterEvent>(OnRobotRefuel);
        SubscribeLocalEvent<CMUConstructionRobotComponent, CMUConstructionRobotBuildDoAfterEvent>(OnRobotBuild);
        SubscribeLocalEvent<CMUConstructionRobotComponent, ExaminedEvent>(OnRobotExamine);
        SubscribeLocalEvent<CMUConstructionRobotKitComponent, AfterInteractEvent>(OnRobotKitAfterInteract);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        InitializeGhostTracking();
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
        if (ent.Comp.Operator is { } owner && !TerminatingOrDeleted(owner))
            RegisterRobot(owner, ent.Owner);
    }

    private void OnRobotGetVerbs(Entity<CMUConstructionRobotComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var previousOwner = ent.Comp.Operator;
        if (previousOwner is { } owner && owner != args.User)
            return;

        var user = args.User;
        var taking = previousOwner == null;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(taking
                ? "cmu-construction-robot-verb-take"
                : "cmu-construction-robot-verb-release"),
            Act = () =>
            {
                if (!TryComp(ent.Owner, out CMUConstructionRobotComponent? current) ||
                    current.Operator != previousOwner)
                    return;

                if (taking)
                    RegisterRobot(user, ent.Owner);
                else
                {
                    UnregisterRobot(user, ent.Owner);
                    if (current.Materials != null)
                        _containers.EmptyContainer(current.Materials);
                }

                current.Operator = taking ? user : null;
                Dirty(ent.Owner, current);
                if (taking)
                    UpdateRobot((ent.Owner, current));
                else
                    SetFollowTarget((ent.Owner, current), null, 0.8f);
            },
        });
    }

    private void OnRobotDragDrop(Entity<CMUConstructionRobotComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled || !TryGetRefuelAmount(ent, args.User, args.Dragged, out _, out var amount))
            return;

        args.Handled = true;
        if (amount <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("cmu-construction-robot-fuel-full"), ent.Owner, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.RefuelDelay,
            new CMUConstructionRobotRefuelDoAfterEvent(), ent, target: ent, used: args.Dragged)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnRobotRefuel(Entity<CMUConstructionRobotComponent> ent, ref CMUConstructionRobotRefuelDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } tank ||
            !TryGetRefuelAmount(ent, args.User, tank, out var solution, out var amount) ||
            amount <= FixedPoint2.Zero)
            return;

        args.Handled = true;
        ent.Comp.WeldingFuel += _solutions.RemoveReagent(solution, WeldingFuel, amount).Float();
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("cmu-construction-robot-refuel-finished"), ent.Owner, args.User);
    }

    private bool TryGetRefuelAmount(Entity<CMUConstructionRobotComponent> robot, EntityUid user, EntityUid tank,
        out Entity<SolutionComponent> source, out FixedPoint2 amount)
    {
        source = default;
        amount = FixedPoint2.Zero;
        if (!TryComp<ReagentTankComponent>(tank, out var reagentTank) || reagentTank.TankType != ReagentTankType.Fuel ||
            !_interaction.InRangeAndAccessible(user, robot.Owner) || !_interaction.InRangeAndAccessible(user, tank) ||
            !_interaction.InRangeUnobstructed(robot.Owner, tank))
            return false;

        // The UGV draws directly from the tank even when its general-purpose valve is set to intake.
        if (!_solutions.TryGetDrainableSolution(tank, out var solutionEntity, out var solution) &&
            !(TryComp<RMCToggleableSolutionTransferComponent>(tank, out var transfer) &&
              _solutions.TryGetSolution(tank, transfer.Solution, out solutionEntity, out solution)))
            return false;

        if (!solution.TryGetReagentQuantity(WeldingFuel, out var available) || available <= FixedPoint2.Zero)
            return false;

        source = solutionEntity.Value;
        amount = FixedPoint2.Min(available, FixedPoint2.New(robot.Comp.FuelCapacity - robot.Comp.WeldingFuel));
        return true;
    }

    private void OnRobotExamine(Entity<CMUConstructionRobotComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(
            "cmu-construction-robot-examine-fuel",
            ("fuel", MathF.Floor(ent.Comp.WeldingFuel)),
            ("capacity", ent.Comp.FuelCapacity)));

        if (ent.Comp.Operator is { } owner && Exists(owner))
            args.PushMarkup(Loc.GetString("cmu-construction-robot-examine-owner", ("owner", owner)));
    }

    private void OnRobotKitAfterInteract(Entity<CMUConstructionRobotKitComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !args.ClickLocation.IsValid(EntityManager) || EntityManager.IsQueuedForDeletion(ent.Owner))
            return;

        var robot = SpawnAtPosition(ent.Comp.RobotPrototype, args.ClickLocation);
        QueueDel(ent.Owner);
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("cmu-construction-robot-deployed"), robot, args.User);
    }

    private void UpdateRobot(Entity<CMUConstructionRobotComponent> robot)
    {
        if (robot.Comp.Operator is not { } owner)
            return;

        if (TerminatingOrDeleted(owner))
        {
            UnregisterRobot(owner, robot.Owner);
            robot.Comp.Operator = null;
            Dirty(robot);
            SetFollowTarget(robot, null, 0.8f);
            return;
        }

        if (robot.Comp.IsBuilding)
            return;

        _candidates.Clear();
        if (_owners.TryGetValue(owner, out var work) && work.Ghosts.Count > 0)
        {
            var position = _transform.GetMapCoordinates(robot);
            foreach (var task in work.Ghosts.Values)
            {
                if (!task.Coordinates.IsValid(EntityManager))
                {
                    RemoveGhost(owner, task.GhostId);
                    continue;
                }

                if (task.AssignedRobot != null && task.AssignedRobot != robot.Owner)
                    continue;

                var target = _transform.ToMapCoordinates(task.Coordinates);
                if (target.MapId == position.MapId)
                    _candidates.Add((Vector2.DistanceSquared(position.Position, target.Position), task));
            }

            _candidates.Sort(static (left, right) => left.Distance.CompareTo(right.Distance));
        }

        foreach (var (distanceSquared, task) in _candidates)
        {
            if (!TryGetBuildPlan(task.Prototype, out var plan) || robot.Comp.WeldingFuel < plan.WeldingFuel)
                continue;

            if (TryFindMissingMaterial(robot, plan, out var needed, out var missingAmount))
            {
                if (robot.Comp.Materials!.ContainedEntities.Count >= robot.Comp.MaxMaterialStacks
                    || !TryFindNearbyMaterial(robot.Owner, task, needed, robot.Comp.MaterialPickupRadius, out var material))
                    continue;

                if (GetDistance(robot.Owner, Transform(material).Coordinates) > 1.2f)
                {
                    AssignTask(task, robot.Owner);
                    SetFollowTarget(robot, Transform(material).Coordinates, 0.7f);
                    return;
                }

                if (!_interaction.InRangeUnobstructed(robot.Owner, material, 1.2f))
                    continue;

                EntityUid? pickup = material;
                if (TryComp<StackComponent>(material, out var stack) && stack.Count > missingAmount)
                    pickup = _stacks.Split((material, stack), missingAmount, Transform(material).Coordinates, robot.Owner);

                if (pickup is { } item && _containers.Insert(item, robot.Comp.Materials))
                {
                    AssignTask(task, robot.Owner);
                    return;
                }

                continue;
            }

            if (distanceSquared > 1.2f * 1.2f)
            {
                AssignTask(task, robot.Owner);
                SetFollowTarget(robot, task.Coordinates, 0.7f);
                return;
            }

            if (!CanReachBuild(robot, task, plan))
                continue;

            AssignTask(task, robot.Owner);
            StartBuild(robot, task, plan);
            return;
        }

        ReleaseAssignedTask(robot.Owner);
        SetFollowTarget(robot, new EntityCoordinates(owner, Vector2.Zero), 0.8f);
    }

    private void AssignTask(ConstructionRobotGhostTask task, EntityUid robot)
    {
        if (task.AssignedRobot == robot)
            return;

        ReleaseAssignedTask(robot);
        _assignments[robot] = task;
        task.AssignedRobot = robot;
    }

    private void ReleaseAssignedTask(EntityUid robot)
    {
        if (_assignments.Remove(robot, out var task))
            task.AssignedRobot = null;
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
        EntityUid robot,
        ConstructionRobotGhostTask task,
        string type,
        float radius,
        out EntityUid stackEntity)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(task.Coordinates, radius, _nearby, LookupFlags.Uncontained);
        stackEntity = EntityUid.Invalid;
        var closest = float.MaxValue;
        foreach (var (uid, stack) in _nearby)
        {
            if (stack.StackTypeId.Id != type || stack.Count <= 0 ||
                HasComp<TimedCorrodingComponent>(uid) ||
                HasComp<DamageableCorrodingComponent>(uid) ||
                _containers.IsEntityInContainer(uid) || GetDistance(uid, task.Coordinates) > radius)
                continue;

            var distance = GetDistance(robot, Transform(uid).Coordinates);
            if (distance >= closest || distance <= 1.2f && !_interaction.InRangeUnobstructed(robot, uid, 1.2f))
                continue;

            closest = distance;
            stackEntity = uid;
        }

        return stackEntity.IsValid();
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

        if (!_players.TryGetSessionByEntity(task.Owner, out var session) ||
            !_construction.TryReserveGhost(session, task.GhostId))
            return;

        _activeBuilds.Add(robot.Owner, (session, task));
        robot.Comp.IsBuilding = true;
        SetFollowTarget(robot, null, 0.8f);
        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            robot.Comp.IsBuilding = false;
            ReleaseActiveBuild(robot.Owner);
        }
    }

    private void OnRobotBuild(
        Entity<CMUConstructionRobotComponent> ent,
        ref CMUConstructionRobotBuildDoAfterEvent args)
    {
        ent.Comp.IsBuilding = false;
        var task = ReleaseActiveBuild(ent.Owner);
        if (args.Cancelled || task == null || ent.Comp.Operator is not { } owner ||
            owner != task.Owner || task.AssignedRobot != ent.Owner ||
            GetGhost(owner, task.GhostId) != task ||
            !TryGetBuildPlan(task.Prototype, out var plan) ||
            !CanUseRecipe(owner, plan.Prototype) || !CanReachBuild(ent, task, plan) ||
            ent.Comp.WeldingFuel < plan.WeldingFuel || TryFindMissingMaterial(ent, plan, out _, out _))
            return;

        ConsumeMaterials(ent, plan);

        ent.Comp.WeldingFuel -= plan.WeldingFuel;
        Dirty(ent);

        var built = SpawnAttachedTo(plan.TargetPrototype, task.Coordinates, rotation: task.Rotation);
        if (plan.Completions.Count > 0)
            _construction.PerformActions(built, owner, plan.Completions);

        var completed = new ConstructionCompletedEvent(built, owner);
        RaiseLocalEvent(built, ref completed, broadcast: true);

        RemoveGhost(owner, task.GhostId);
        if (_players.TryGetSessionByEntity(owner, out var session))
            RaiseNetworkEvent(new AckStructureConstructionMessage(task.GhostId, GetNetEntity(built)), session.Channel);
    }

    private ConstructionRobotGhostTask? ReleaseActiveBuild(EntityUid robot)
    {
        if (!_activeBuilds.Remove(robot, out var build))
            return null;

        _construction.ReleaseGhost(build.Session, build.Task.GhostId);
        return build.Task;
    }

    private bool CanReachBuild(Entity<CMUConstructionRobotComponent> robot, ConstructionRobotGhostTask task, ConstructionRobotBuildPlan plan)
    {
        if (EntityManager.IsQueuedForDeletion(robot.Owner) || !task.Coordinates.IsValid(EntityManager) ||
            _containers.IsEntityInContainer(robot.Owner))
            return false;

        var position = _transform.ToMapCoordinates(task.Coordinates);
        var predicate = _construction.GetPredicate(plan.Prototype.CanBuildInImpassable, position);
        if (!_interaction.InRangeUnobstructed(robot.Owner, position, 1.2f, predicate: predicate))
            return false;

        var attempt = new RMCConstructionAttemptEvent(task.Coordinates, plan.Prototype, User: task.Owner);
        RaiseLocalEvent(ref attempt);
        return !attempt.Cancelled && plan.Prototype.Conditions.All(condition =>
            condition.Condition(task.Owner, task.Coordinates, task.Rotation.GetCardinalDir()));
    }

    private void ConsumeMaterials(Entity<CMUConstructionRobotComponent> robot, ConstructionRobotBuildPlan plan)
    {
        foreach (var (type, amount) in plan.Materials)
        {
            var remaining = amount;
            foreach (var item in robot.Comp.Materials!.ContainedEntities.ToArray())
            {
                if (!TryComp<StackComponent>(item, out var stack) || stack.StackTypeId.Id != type)
                    continue;

                var used = Math.Min(remaining, stack.Count);
                _stacks.ReduceCount((item, stack), used);

                remaining -= used;
                if (remaining <= 0)
                    break;
            }
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
}

using System.Linq;
using System.Numerics;
using Content.Server.Construction;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Administration;
using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.ConstructionRobot;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Construction;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.NPC;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.ConstructionRobot;

public sealed partial class CMUConstructionRobotSystem : EntitySystem
{
    [Dependency] private ConstructionSystem _construction = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private QuickDialogSystem _quickDialog = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private RMCConstructionSystem _rmcConstruction = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    private const string FollowCompoundTask = "CMUDroneFollowCompound";
    private const string FollowCloseRangeKey = "FollowCloseRange";
    private static readonly TimeSpan MovementTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);
    private static readonly ReagentId WeldingFuel = new("WeldingFuel", null);

    private readonly List<(bool KnownMaterials, float Distance, ConstructionRobotGhostTask Task)> _candidates = [];
    private readonly HashSet<Entity<StackComponent>> _nearby = new();
    private readonly HashSet<Entity<RequisitionsCrateComponent>> _nearbyCrates = new();
    private readonly Dictionary<EntityUid, (ICommonSession Session, ConstructionRobotGhostTask Task)> _activeBuilds = new();
    private TimeSpan _nextThink;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUConstructionRobotComponent, ComponentInit>(OnRobotInit);
        SubscribeLocalEvent<CMUConstructionRobotComponent, GetVerbsEvent<Verb>>(OnRobotGetVerbs);
        SubscribeLocalEvent<CMUConstructionRobotComponent, DragDropTargetEvent>(OnRobotDragDrop);
        SubscribeLocalEvent<CMUConstructionRobotComponent, CMUConstructionRobotRefuelDoAfterEvent>(OnRobotRefuel);
        SubscribeLocalEvent<CMUConstructionRobotComponent, CMUConstructionRobotAutoRefuelDoAfterEvent>(OnRobotAutoRefuel);
        SubscribeLocalEvent<CMUConstructionRobotComponent, CMUConstructionRobotBuildDoAfterEvent>(OnRobotBuild);
        SubscribeLocalEvent<CMUConstructionRobotComponent, ExaminedEvent>(OnRobotExamine);
        SubscribeLocalEvent<RequisitionsCrateComponent, GetVerbsEvent<Verb>>(OnCrateGetVerbs);
        SubscribeLocalEvent<CMUConstructionRobotKitComponent, AfterInteractEvent>(OnRobotKitAfterInteract);
        SubscribeLocalEvent<CMUConstructionRobotPackageComponent, UseInHandEvent>(OnRobotPackageUse);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        InitializeGhostTracking();
        InitializeVisor();
        InitializeRepair();
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

        UpdateOwnerHud();
        UpdateVisors();
    }

    private void OnRobotInit(Entity<CMUConstructionRobotComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Materials = _containers.EnsureContainer<Container>(ent, StorageComponent.ContainerId);
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
        if (previousOwner == user)
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("cmu-construction-robot-rename-verb"),
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/pencil.png")),
                Priority = -4,
                Act = () => OpenRobotRenameDialog(ent, user),
            });
        }

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
                    UnregisterRobot(user, ent.Owner);

                current.Operator = taking ? user : null;
                Dirty(ent.Owner, current);
                if (taking)
                    UpdateRobot((ent.Owner, current));
                else
                    SetFollowTarget((ent.Owner, current), null, 0.8f);
            },
        });
    }

    private void OpenRobotRenameDialog(Entity<CMUConstructionRobotComponent> robot, EntityUid user)
    {
        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        var netRobot = GetNetEntity(robot.Owner);
        var netUser = GetNetEntity(user);
        _quickDialog.OpenDialog(actor.PlayerSession,
            Loc.GetString("cmu-construction-robot-rename-title"),
            Loc.GetString("cmu-construction-robot-rename-prompt", ("max", robot.Comp.MaxNameLength)),
            (string name) =>
            {
                if (!TryGetEntity(netRobot, out var robotUid) ||
                    !TryGetEntity(netUser, out var userUid) ||
                    !TryComp<CMUConstructionRobotComponent>(robotUid.Value, out var current) ||
                    current.Operator != userUid.Value)
                    return;

                if (!CMUDroneName.TryNormalize(name, current.MaxNameLength, out var normalized))
                {
                    _popup.PopupEntity(Loc.GetString("cmu-construction-robot-rename-invalid", ("max", current.MaxNameLength)),
                        userUid.Value, userUid.Value, PopupType.SmallCaution);
                    return;
                }

                _metaData.SetEntityName(robotUid.Value, normalized);
                _popup.PopupEntity(Loc.GetString("cmu-construction-robot-rename-success", ("robot", robotUid.Value)),
                    userUid.Value, userUid.Value);
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

        return TryGetTankFuel(robot, tank, out source, out amount);
    }

    private bool TryGetTankFuel(Entity<CMUConstructionRobotComponent> robot, EntityUid tank,
        out Entity<SolutionComponent> source, out FixedPoint2 amount)
    {
        source = default;
        amount = FixedPoint2.Zero;
        if (!TryComp<ReagentTankComponent>(tank, out var reagentTank) || reagentTank.TankType != ReagentTankType.Fuel)
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
        if (!args.IsInDetailsRange || !HasWornVisor(args.Examiner))
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

    private void OnRobotPackageUse(Entity<CMUConstructionRobotPackageComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || EntityManager.IsQueuedForDeletion(ent.Owner))
            return;

        args.Handled = true;
        var coordinates = Transform(args.User).Coordinates;
        var deployer = Spawn(ent.Comp.DeployerPrototype, coordinates);
        var visor = Spawn(ent.Comp.VisorPrototype, coordinates);
        _hands.TryDrop(args.User, ent.Owner);
        QueueDel(ent.Owner);
        _hands.TryPickupAnyHand(args.User, visor);
        _popup.PopupEntity(Loc.GetString("cmu-construction-robot-package-unpacked"), deployer, args.User);
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

        UpdateFuelWarning(robot, owner);
        if (PauseForHazard(robot))
            return;

        if (robot.Comp.IsBuilding || robot.Comp.IsRepairing || robot.Comp.IsAutoRefueling)
            return;

        CheckMovementProgress(robot);
        if (UpdateAutoRefuel(robot))
            return;
        if (UpdateCrateOrder(robot))
            return;

        _candidates.Clear();
        var manualQueue = false;
        if (_owners.TryGetValue(owner, out var work) && work.Ghosts.Count > 0)
        {
            manualQueue = work.ManualQueue;
            var position = _transform.GetMapCoordinates(robot);
            foreach (var task in work.Ghosts.Values.ToArray())
            {
                if (!task.Coordinates.IsValid(EntityManager))
                {
                    RemoveGhost(owner, task.GhostId);
                    continue;
                }

                if (task.AssignedRobot != null && task.AssignedRobot != robot.Owner)
                    continue;

                foreach (var key in task.BlockedDestinations
                             .Where(entry => entry.Value <= _timing.CurTime)
                             .Select(entry => entry.Key).ToArray())
                    task.BlockedDestinations.Remove(key);

                var target = _transform.ToMapCoordinates(task.Coordinates);
                if (target.MapId != position.MapId ||
                    !TryGetBuildPlan(task.Prototype, out var plan) || robot.Comp.WeldingFuel < plan.WeldingFuel)
                    continue;

                plan = ApplyConstructionSkill(robot.Owner, plan);
                _candidates.Add((HasKnownMaterials(robot, task, plan),
                    Vector2.DistanceSquared(position.Position, target.Position), task));
            }

            _candidates.Sort((left, right) =>
            {
                var priority = right.Task.Priority.CompareTo(left.Task.Priority);
                if (priority != 0)
                    return priority;
                if (manualQueue)
                    return left.Task.QueueOrder.CompareTo(right.Task.QueueOrder);
                var availability = right.KnownMaterials.CompareTo(left.KnownMaterials);
                return availability != 0 ? availability : left.Distance.CompareTo(right.Distance);
            });
        }

        foreach (var (_, distanceSquared, task) in _candidates)
        {
            if (!TryGetBuildPlan(task.Prototype, out var plan) || robot.Comp.WeldingFuel < plan.WeldingFuel)
                continue;

            plan = ApplyConstructionSkill(robot.Owner, plan);
            if (TryFindMissingMaterial(robot, plan, out var needed, out var missingAmount))
            {
                if (!TryFindNearbyMaterial(robot.Owner, task, needed, robot.Comp.MaterialPickupRadius, out var material))
                {
                    if (TryOpenNearbyCrate(robot, task))
                        return;

                    AlertMissingMaterial(robot, task, plan, needed, missingAmount);
                    continue;
                }

                if (!_storage.CanInsert(robot.Owner, material, out _))
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

                if (pickup is { } item && _storage.Insert(robot.Owner, item, out _, playSound: false))
                {
                    AssignTask(task, robot.Owner);
                    return;
                }

                continue;
            }

            if (distanceSquared > 1.2f * 1.2f)
            {
                if (IsDestinationBlocked(task, robot.Owner, task.Coordinates))
                    continue;

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

        if (UpdateRepairWork(robot))
            return;

        ReleaseAssignedTask(robot.Owner);
        if (robot.Comp.RepairAreaCenter is { } repairArea && repairArea.IsValid(EntityManager))
        {
            if (GetDistance(robot.Owner, repairArea) <= 0.8f)
            {
                SetFollowTarget(robot, null, 0.8f);
                return;
            }

            SetFollowTarget(robot, repairArea, 0.7f);
            return;
        }

        if (robot.Comp.IdlePosition is { } idle)
        {
            if (!idle.IsValid(EntityManager))
                robot.Comp.IdlePosition = null;
            else if (GetDistance(robot.Owner, idle) <= 0.8f)
            {
                SetFollowTarget(robot, null, 0.8f);
                return;
            }
            else
            {
                SetFollowTarget(robot, idle, 0.7f);
                return;
            }
        }

        SetFollowTarget(robot, new EntityCoordinates(owner, Vector2.Zero), 0.8f);
    }

    private bool HasKnownMaterials(Entity<CMUConstructionRobotComponent> robot, ConstructionRobotGhostTask task,
        ConstructionRobotBuildPlan plan)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(task.Coordinates, robot.Comp.MaterialPickupRadius, _nearby, LookupFlags.Uncontained);
        foreach (var (type, amount) in plan.Materials)
        {
            var available = 0;
            foreach (var item in robot.Comp.Materials!.ContainedEntities)
            {
                if (TryComp<StackComponent>(item, out var stack) && stack.StackTypeId.Id == type)
                    available += stack.Count;
            }

            foreach (var (uid, stack) in _nearby)
            {
                if (stack.StackTypeId.Id != type || stack.Count <= 0 ||
                    HasComp<TimedCorrodingComponent>(uid) || HasComp<DamageableCorrodingComponent>(uid) ||
                    _containers.IsEntityInContainer(uid) || GetDistance(uid, task.Coordinates) > robot.Comp.MaterialPickupRadius ||
                    IsDestinationBlocked(task, robot.Owner, Transform(uid).Coordinates) ||
                    !_storage.CanInsert(robot.Owner, uid, out _))
                    continue;

                available += stack.Count;
            }

            if (available < amount)
                return false;
        }

        return true;
    }

    private bool IsDestinationBlocked(ConstructionRobotGhostTask task, EntityUid robot, EntityCoordinates coordinates)
    {
        var key = (robot, coordinates);
        if (!task.BlockedDestinations.TryGetValue(key, out var retryAt))
            return false;
        if (_timing.CurTime < retryAt)
            return true;

        task.BlockedDestinations.Remove(key);
        return false;
    }

    private void CheckMovementProgress(Entity<CMUConstructionRobotComponent> robot)
    {
        if (robot.Comp.CurrentFollowTarget is not { } target || !target.IsValid(EntityManager))
            return;

        var position = _transform.GetMapCoordinates(robot);
        if (robot.Comp.LastProgressPosition is not { } previous || position.MapId != previous.MapId ||
            Vector2.DistanceSquared(position.Position, previous.Position) >= 0.25f * 0.25f)
        {
            robot.Comp.LastProgressPosition = position;
            robot.Comp.LastProgressTime = _timing.CurTime;
            return;
        }

        if (GetDistance(robot.Owner, target) <= robot.Comp.FollowRange)
        {
            robot.Comp.LastProgressTime = _timing.CurTime;
            return;
        }

        if (_timing.CurTime < robot.Comp.LastProgressTime + MovementTimeout)
            return;

        if (robot.Comp.AutoRefuelTarget is { } tank && !TerminatingOrDeleted(tank) &&
            target == Transform(tank).Coordinates)
        {
            robot.Comp.AutoRefuelRetries[tank] = _timing.CurTime + RetryDelay;
            robot.Comp.AutoRefuelTarget = null;
        }
        else if (_assignments.TryGetValue(robot.Owner, out var task))
        {
            task.BlockedDestinations[(robot.Owner, target)] = _timing.CurTime + RetryDelay;
            ReleaseAssignedTask(robot.Owner);
        }
        else if (_crateOrders.TryGetValue(robot.Owner, out var crate) &&
                 TryComp<PullableComponent>(crate, out var pullable) && pullable.Puller != robot.Owner &&
                 target == Transform(crate).Coordinates)
        {
            _crateRetryAfter[robot.Owner] = _timing.CurTime + RetryDelay;
        }
        else if (robot.Comp.RepairTarget is { } repairTarget)
        {
            robot.Comp.RepairRetryTarget = repairTarget;
            robot.Comp.RepairRetryUntil = _timing.CurTime + RetryDelay;
            robot.Comp.RepairTarget = null;
        }

        SetFollowTarget(robot, null, 0.8f);
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

    private void AlertMissingMaterial(Entity<CMUConstructionRobotComponent> robot, ConstructionRobotGhostTask task,
        ConstructionRobotBuildPlan plan, string type, int amount)
    {
        if (robot.Comp.Operator is not { } owner || !HasWornVisor(owner) ||
            task.MissingAlerts.TryGetValue(robot.Owner, out var nextAlert) && _timing.CurTime < nextAlert)
            return;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(task.Coordinates, robot.Comp.MaterialPickupRadius, _nearby, LookupFlags.Uncontained);
        foreach (var (uid, stack) in _nearby)
        {
            if (stack.StackTypeId.Id == type && stack.Count > 0 && !_containers.IsEntityInContainer(uid) &&
                _storage.CanInsert(robot.Owner, uid, out _))
                return;
        }

        _nearbyCrates.Clear();
        _lookup.GetEntitiesInRange(task.Coordinates, robot.Comp.MaterialPickupRadius, _nearbyCrates, LookupFlags.Uncontained);
        foreach (var (uid, _) in _nearbyCrates)
        {
            if (!task.CheckedCrates.Contains(uid) && TryComp<EntityStorageComponent>(uid, out var storage) && !storage.Open)
                return;
        }

        task.MissingAlerts[robot.Owner] = _timing.CurTime + TimeSpan.FromSeconds(30);
        var name = plan.Prototype.Name ?? plan.Prototype.SetName ?? task.Prototype;
        _popup.PopupEntity(Loc.GetString("cmu-construction-robot-missing-alert",
            ("name", name), ("amount", amount), ("material", type)), robot.Owner, owner);
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
                _containers.IsEntityInContainer(uid) || GetDistance(uid, task.Coordinates) > radius ||
                IsDestinationBlocked(task, robot, Transform(uid).Coordinates) ||
                !_storage.CanInsert(robot, uid, out _))
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
        if (!_doAfter.TryStartDoAfter(doAfter, out var doAfterId))
        {
            robot.Comp.IsBuilding = false;
            ReleaseActiveBuild(robot.Owner);
        }
        else
            robot.Comp.BuildDoAfter = doAfterId;
    }

    private void OnRobotBuild(
        Entity<CMUConstructionRobotComponent> ent,
        ref CMUConstructionRobotBuildDoAfterEvent args)
    {
        ent.Comp.IsBuilding = false;
        ent.Comp.BuildDoAfter = null;
        var task = ReleaseActiveBuild(ent.Owner);
        if (args.Cancelled || task == null || PauseForHazard(ent) || ent.Comp.Operator is not { } owner ||
            owner != task.Owner || task.AssignedRobot != ent.Owner ||
            GetGhost(owner, task.GhostId) != task ||
            !TryGetBuildPlan(task.Prototype, out var plan) ||
            !CanUseRecipe(owner, plan.Prototype) || !CanReachBuild(ent, task, plan) ||
            ent.Comp.WeldingFuel < plan.WeldingFuel)
            return;

        plan = ApplyConstructionSkill(ent.Owner, plan);
        if (TryFindMissingMaterial(ent, plan, out _, out _))
            return;

        ConsumeMaterials(ent, plan);

        ent.Comp.WeldingFuel -= plan.WeldingFuel;
        Dirty(ent);

        var built = SpawnAttachedTo(plan.TargetPrototype, task.Coordinates, rotation: task.Rotation);
        if (plan.Completions.Count > 0)
            _construction.PerformActions(built, owner, plan.Completions);

        foreach (var (type, shortfall) in plan.Shortfalls)
        {
            var transaction = new RMCConstructionTransactionCompletedEvent(built, ent.Owner, type, shortfall);
            RaiseLocalEvent(built, ref transaction, broadcast: true);
        }

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
        robot.Comp.FollowRange = closeRange;
        robot.Comp.LastProgressPosition = _transform.GetMapCoordinates(robot);
        robot.Comp.LastProgressTime = _timing.CurTime;
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
        public CMUConstructionRobotJobPriority Priority = CMUConstructionRobotJobPriority.Normal;
        public long QueueOrder;
        public EntityUid? AssignedRobot { get; set; }
        public HashSet<EntityUid> CheckedCrates { get; } = [];
        public Dictionary<(EntityUid Robot, EntityCoordinates Destination), TimeSpan> BlockedDestinations { get; } = [];
        public Dictionary<EntityUid, TimeSpan> MissingAlerts { get; } = [];
    }
}

using System.Numerics;
using Content.Server.CMU14.Round;
using Content.Server.Ghost.Roles.Components;
using Content.Server.NPC.Components;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared._RMC14.Xenonids.Devour;
using Content.Shared._RMC14.Input;
using Content.Shared._RMC14.Xenonids.Eye;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Actions;
using Content.Shared.CMU14.Xenomorphs.Swarm;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Xenomorphs.Swarm;

public sealed partial class CMUQueenSwarmSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private QueenEyeSystem _eye = default!;
    [Dependency] private NpcFactionSystem _factions = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private AuRoundSystem _round = default!;
    [Dependency] private NPCSteeringSystem _steering = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        InitializeRules();
        InitializeOrders();
        InitializeBehavior();
        SubscribeLocalEvent<CMUQueenSwarmComponent, MapInitEvent>(OnQueenInit);
        SubscribeLocalEvent<CMUQueenSwarmComponent, CMUSummonSwarmActionEvent>(OnSummon);
        SubscribeLocalEvent<CMUQueenSwarmComponent, CMUGuardSwarmActionEvent>(OnGuard);
        SubscribeLocalEvent<CMUQueenSwarmComponent, CMUFollowSwarmActionEvent>(OnFollow);
        SubscribeLocalEvent<CMUSwarmLesserComponent, MobStateChangedEvent>(OnLesserState);
        SubscribeLocalEvent<CMUSwarmLesserComponent, MapInitEvent>(OnLesserInit);
        SubscribeLocalEvent<CMUSwarmLesserComponent, ComponentStartup>(OnLesserStartup);
        SubscribeLocalEvent<CMUSwarmControllerComponent, ComponentShutdown>(OnControllerShutdown);
    }

    private void OnQueenInit(Entity<CMUQueenSwarmComponent> ent, ref MapInitEvent args)
    {
        var commander = EnsureComp<CMUSwarmCommanderComponent>(ent);
        foreach (var prototype in new[] { ent.Comp.MenuAction, ent.Comp.RulesAction, ent.Comp.SummonAction,
                     ent.Comp.GuardAction, ent.Comp.FollowAction, ent.Comp.PlaceAction })
        {
            if (_actions.AddAction(ent, prototype) is { } action)
                commander.Actions.Add(action);
        }
        _ui.SetUi(ent.Owner, CMUSwarmUiKey.Key, new InterfaceData("CMUSwarmBoundUserInterface"));
        SetCommandAccess(ent, HasComp<XenoAttachedOvipositorComponent>(ent));
    }

    private bool TryController(EntityUid queen, out EntityUid hive, out CMUSwarmControllerComponent controller)
    {
        hive = default;
        controller = default!;
        if (!_mobState.IsAlive(queen) || !HasComp<XenoAttachedOvipositorComponent>(queen) ||
            _hive.GetHive(queen) is not { } current || current.Comp.CurrentQueen != queen)
            return false;

        if (!TryComp<CMUQueenSwarmComponent>(queen, out var swarm) ||
            !(swarm.Enabled ?? _round.SelectedPreset?.ID == "DistressSignal"))
        {
            _popup.PopupCursor(Loc.GetString("cmu-swarm-disabled"), queen);
            return false;
        }

        hive = current.Owner;
        controller = EnsureComp<CMUSwarmControllerComponent>(hive);
        return true;
    }

    private void OnSummon(Entity<CMUQueenSwarmComponent> ent, ref CMUSummonSwarmActionEvent args)
    {
        if (args.Handled || !TryController(ent, out var hive, out var controller))
            return;

        if (controller.NextSummon > _timing.CurTime)
        {
            _popup.PopupCursor(Loc.GetString("cmu-swarm-cooldown"), ent);
            return;
        }

        if (_hive.GetHive(ent.Owner) is not { } hiveEntity || _hive.GetHiveCore(hiveEntity) is not { } core || TerminatingOrDeleted(core))
        {
            _popup.PopupCursor(Loc.GetString("cmu-swarm-no-core"), ent);
            return;
        }

        // Count survivors here as well as during AI maintenance: summons can arrive before
        // the next think, and dead or deleted members must immediately free their slots.
        controller.Members.RemoveAll(member => TerminatingOrDeleted(member) || !_mobState.IsAlive(member));
        var count = Math.Min(controller.SpawnCount, CMUQueenSwarmComponent.MaxLivingMembers - controller.Members.Count);
        if (count <= 0)
        {
            _popup.PopupCursor(Loc.GetString("cmu-swarm-full", ("limit", CMUQueenSwarmComponent.MaxLivingMembers)), ent);
            return;
        }

        for (var i = 0; i < count; i++)
        {
            var lesser = Spawn(controller.Lesser, Transform(core).Coordinates);
            _hive.SetHive(lesser, hive);
            controller.Members.Add(lesser);
            var agent = GetAgent(lesser, controller);
            agent.Home = new EntityCoordinates(core, Vector2.Zero);
            agent.Order = new CMUSwarmOrder { Kind = CMUSwarmOrderKind.Idle };
            agent.NextSense = _timing.CurTime + TimeSpan.FromSeconds(i * 0.07);
        }

        if (controller.Anchor == null || TerminatingOrDeleted(controller.Anchor))
            controller.Anchor = core;

        controller.NextSummon = _timing.CurTime + controller.Cooldown;
        // The queen's body may be off-screen while commanding through her eye.
        _popup.PopupCursor(Loc.GetString("cmu-swarm-summoned", ("count", count)), ent);
        args.Handled = true;
    }

    private bool CanCommandAt(EntityUid queen, EntityCoordinates coordinates)
    {
        if (!coordinates.IsValid(EntityManager) || !float.IsFinite(coordinates.X) || !float.IsFinite(coordinates.Y) ||
            !_zLevels.IsSameZNetwork(_transform.GetMapId(coordinates), Transform(queen).MapID))
            return false;

        if (_eye.IsInQueenEye(queen))
            return _eye.CanSeeTarget(queen, coordinates);

        return _interaction.InRangeUnobstructed(queen, coordinates, 14);
    }

    private void OnGuard(Entity<CMUQueenSwarmComponent> ent, ref CMUGuardSwarmActionEvent args)
    {
        if (args.Handled || !TryController(ent, out var hive, out var controller) || !CanCommandAt(ent, args.Target))
            return;

        PruneFlags(controller);
        if (controller.Flags.Count >= MaxFlags)
            return;
        controller.Marker = CreateFlag(hive, controller, CMUSwarmOrderKind.Guard, args.Target);
        controller.Anchor = controller.Marker;
        var order = new CMUSwarmOrder { Kind = CMUSwarmOrderKind.Guard };
        order.Flags.Add(controller.Marker.Value);
        foreach (var member in controller.Members)
        {
            if (!TerminatingOrDeleted(member) && _mobState.IsAlive(member))
                SetOrder(member, GetAgent(member, controller), order);
        }
        ResetOrder(controller);
        PruneFlags(controller);
        args.Handled = true;
    }

    private void OnFollow(Entity<CMUQueenSwarmComponent> ent, ref CMUFollowSwarmActionEvent args)
    {
        if (args.Handled || !TryController(ent, out _, out var controller) ||
            !HasComp<XenoComponent>(args.Target) || !HasComp<ActorComponent>(args.Target) ||
            !_mobState.IsAlive(args.Target) || !_hive.FromSameHive(ent.Owner, args.Target) ||
            !CanCommandAt(ent, Transform(args.Target).Coordinates))
            return;

        var commander = EnsureComp<CMUSwarmCommanderComponent>(ent);
        var selected = commander.Pending == CMUSwarmOrderKind.Follow ? commander.Selection : controller.Members;
        var order = new CMUSwarmOrder { Kind = CMUSwarmOrderKind.Follow, Follow = args.Target };
        foreach (var member in selected)
        {
            if (!TerminatingOrDeleted(member) && _mobState.IsAlive(member) &&
                _hive.FromSameHive(ent.Owner, member) && !HasComp<ActorComponent>(member))
                SetOrder(member, GetAgent(member, controller), order);
        }
        commander.Pending = null;
        controller.NextThink = TimeSpan.Zero;
        args.Handled = true;
    }

    private void ResetOrder(CMUSwarmControllerComponent controller)
    {
        controller.Sightings.Clear();
        foreach (var member in controller.Members)
        {
            if (TerminatingOrDeleted(member))
                continue;
            var agent = GetAgent(member, controller);
            StopAgent(member, agent);
            agent.NextSense = TimeSpan.Zero;
        }
        controller.NextThink = TimeSpan.Zero;
        controller.Cursor = 0;
    }

    private void OnLesserStartup(Entity<CMUSwarmLesserComponent> ent, ref ComponentStartup args)
    {
        // Remove the player-only input optimization before MapInit can disable NPC movement.
        RemComp<RMCActiveInputMoverComponent>(ent);
    }

    private void OnLesserInit(Entity<CMUSwarmLesserComponent> ent, ref MapInitEvent args)
    {
        // Reuse the player lesser's anatomy and visuals without offering a ghost takeover
        // or applying its off-weeds attrition to long-distance swarm orders.
        RemComp<GhostRoleComponent>(ent);
        RemComp<GhostTakeoverAvailableComponent>(ent);
        RemComp<DamageOffWeedsComponent>(ent);
        // Expendable AI losses must not flood hive chat with player death announcements.
        RemComp<XenoAnnounceDeathComponent>(ent);
        EnsureComp<ActiveNPCComponent>(ent);
    }

    private void OnLesserState(Entity<CMUSwarmLesserComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;
        if (TryComp<CMUSwarmAgentComponent>(ent, out var agent))
            CancelWork(ent, agent);
        RemComp<NPCMeleeCombatComponent>(ent);
        _steering.Unregister(ent);
        RemComp<ActiveNPCComponent>(ent);
    }

    private void OnControllerShutdown(Entity<CMUSwarmControllerComponent> ent, ref ComponentShutdown args)
    {
        foreach (var flag in ent.Comp.Flags)
            QueueDel(flag);
        ResetOrder(ent.Comp);
    }

    // A large hive continues its batch next frame; native movement/combat runs between decisions.
    private readonly List<EntityUid> _expiredSightings = new();

    public override void Update(float frameTime)
    {
        UpdateCommanders();
        var budget = 32;
        var sightBudget = 64;
        var query = EntityQueryEnumerator<CMUSwarmControllerComponent>();
        while (query.MoveNext(out var hive, out var controller))
        {
            if (budget == 0 || controller.NextThink > _timing.CurTime)
                continue;
            if (controller.Cursor == 0)
            {
                controller.Members.RemoveAll(uid => TerminatingOrDeleted(uid) || !_mobState.IsAlive(uid));
                controller.AssignedTargets.Clear();
                if (controller.NextMaintenance <= _timing.CurTime)
                {
                    PruneFlags(controller);
                    controller.NextMaintenance = _timing.CurTime + TimeSpan.FromSeconds(5);
                }
            }
            var count = controller.Members.Count;
            if (count == 0)
                continue;
            var end = Math.Min(count, controller.Cursor + budget);
            var sightings = controller.Sightings;
            _expiredSightings.Clear();
            foreach (var (target, seenUntil) in sightings)
            {
                if (controller.Passive || seenUntil <= _timing.CurTime || TerminatingOrDeleted(target) || !CanAttack(target, hive))
                    _expiredSightings.Add(target);
            }
            foreach (var target in _expiredSightings)
                sightings.Remove(target);

            // Shared sightings are bounded and require real LOS. Each observer senses at most once a second.
            for (var i = controller.Cursor; i < end && sightBudget > 0; i++)
            {
                var member = controller.Members[i];
                if (TerminatingOrDeleted(member) || !_mobState.IsAlive(member) || controller.Passive)
                    continue;
                var agent = GetAgent(member, controller);
                if (agent.NextSense > _timing.CurTime || agent.Aggression == CMUSwarmAggression.Passive)
                    continue;
                agent.NextSense = _timing.CurTime + TimeSpan.FromSeconds(1);
                var candidates = 0;
                foreach (var hostile in _factions.GetNearbyHostiles(member, controller.SightRange))
                {
                    if (candidates++ == 8 || sightBudget-- <= 0)
                        break;
                    if (!CanAttack(hostile, hive) || !_interaction.InRangeUnobstructed(member, hostile, controller.SightRange))
                        continue;
                    if (sightings.Count < 32 || sightings.ContainsKey(hostile))
                        sightings[hostile] = _timing.CurTime + controller.TargetMemory;
                }
            }

            for (var i = controller.Cursor; i < end; i++)
            {
                budget--;
                var member = controller.Members[i];
                if (TerminatingOrDeleted(member) || !_mobState.IsAlive(member) || HasComp<ActorComponent>(member))
                    continue;
                var agent = GetAgent(member, controller);
                var anchor = OrderAnchor(agent, controller.Anchor);
                if (anchor is not { } targetAnchor || TerminatingOrDeleted(targetAnchor) ||
                    agent.Order?.Kind == CMUSwarmOrderKind.Follow &&
                    (!_mobState.IsAlive(targetAnchor) || _hive.GetHive(targetAnchor)?.Owner != hive))
                {
                    SetOrder(member, agent, new CMUSwarmOrder { Kind = CMUSwarmOrderKind.Idle });
                    agent.Home = Transform(member).Coordinates;
                    targetAnchor = agent.Home.Value.EntityId;
                }
                if (TryComp<CMUSwarmTraversalComponent>(member, out var traversal) && traversal.Crossing &&
                    TraverseToAnchor(member, targetAnchor))
                    continue;
                var enemy = controller.Passive || agent.Aggression == CMUSwarmAggression.Passive ? null :
                    SelectEnemy(member, sightings, controller.AssignedTargets, agent, targetAnchor, controller.LeashRange);
                if (enemy is { } hostile)
                {
                    Wake(member, agent);
                    SetSlow(member, agent, false);
                    CancelWork(member, agent);
                    if (traversal?.Route != null)
                        ClearTraversal(member, traversal);
                    controller.AssignedTargets[hostile] = controller.AssignedTargets.GetValueOrDefault(hostile) + 1;
                    var target = BlockingBarricade(member, hostile) ?? hostile;
                    MoveTo(member, new EntityCoordinates(target, Vector2.Zero), target == hostile ? 0.2f : 0.6f);
                    EnsureComp<NPCMeleeCombatComponent>(member).Target = target;
                    continue;
                }
                ThinkOrder(member, agent, targetAnchor, controller, i);
            }
            controller.Cursor = end;
            if (end >= count)
            {
                controller.Cursor = 0;
                controller.NextThink = _timing.CurTime + controller.ThinkInterval;
            }
        }
    }

    private static void ConfigureSteering(NPCSteeringComponent steering)
    {
        // These lessers inherit climbing and prying, but have no HTN blackboard
        // from which the general steering system could infer those capabilities.
        steering.Flags = PathFlags.Climbing | PathFlags.Prying | PathFlags.Interact;
        // Keep planning around nearby obstacles until within the arrival radius.
        // The normal 1.5-tile shortcut can strand a lesser on the other side of a corner.
        steering.RepathRange = 0.2f;
    }

    private bool CanAttack(EntityUid target, EntityUid hive)
    {
        return _mobState.IsAlive(target) && !_hive.IsAllyOfHive(target, hive) &&
               !HasComp<XenoNestedComponent>(target) && !HasComp<DevouredComponent>(target);
    }

    private bool Within(EntityUid origin, EntityUid target, float range)
    {
        return Transform(origin).Coordinates.TryDistance(EntityManager, _transform, Transform(target).Coordinates, out var distance) && distance <= range;
    }
}

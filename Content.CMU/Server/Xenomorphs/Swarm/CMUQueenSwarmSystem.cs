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
        _actions.AddAction(ent, ent.Comp.MenuAction);
        _actions.AddAction(ent, ent.Comp.RulesAction);
        _actions.AddAction(ent, ent.Comp.SummonAction);
        _actions.AddAction(ent, ent.Comp.GuardAction);
        _actions.AddAction(ent, ent.Comp.FollowAction);
    }

    private bool TryController(EntityUid queen, out EntityUid hive, out CMUSwarmControllerComponent controller)
    {
        hive = default;
        controller = default!;
        if (!_mobState.IsAlive(queen) || _hive.GetHive(queen) is not { } current || current.Comp.CurrentQueen != queen)
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

        for (var i = 0; i < controller.SpawnCount; i++)
        {
            var lesser = Spawn(controller.Lesser, Transform(core).Coordinates);
            _hive.SetHive(lesser, hive);
            controller.Members.Add(lesser);
        }

        if (controller.Anchor == null || TerminatingOrDeleted(controller.Anchor))
            controller.Anchor = ent.Owner;

        controller.NextSummon = _timing.CurTime + controller.Cooldown;
        // The queen's body may be off-screen while commanding through her eye.
        _popup.PopupCursor(Loc.GetString("cmu-swarm-summoned", ("count", controller.SpawnCount)), ent);
        args.Handled = true;
    }

    private bool CanCommandAt(EntityUid queen, EntityCoordinates coordinates)
    {
        if (!coordinates.IsValid(EntityManager) || !float.IsFinite(coordinates.X) || !float.IsFinite(coordinates.Y) ||
            _transform.GetMapId(coordinates) != Transform(queen).MapID)
            return false;

        if (_eye.IsInQueenEye(queen))
            return _eye.CanSeeTarget(queen, coordinates);

        return _interaction.InRangeUnobstructed(_transform.GetMapCoordinates(queen), _transform.ToMapCoordinates(coordinates), 14);
    }

    private void OnGuard(Entity<CMUQueenSwarmComponent> ent, ref CMUGuardSwarmActionEvent args)
    {
        if (args.Handled || !TryController(ent, out _, out var controller) || !CanCommandAt(ent, args.Target))
            return;

        if (controller.Marker is { } old)
            QueueDel(old);
        controller.Marker = Spawn(controller.MarkerPrototype, args.Target);
        controller.Anchor = controller.Marker;
        ResetOrder(controller);
        args.Handled = true;
    }

    private void OnFollow(Entity<CMUQueenSwarmComponent> ent, ref CMUFollowSwarmActionEvent args)
    {
        if (args.Handled || !TryController(ent, out _, out var controller) ||
            !HasComp<XenoComponent>(args.Target) || !HasComp<ActorComponent>(args.Target) ||
            !_mobState.IsAlive(args.Target) || !_hive.FromSameHive(ent.Owner, args.Target) ||
            !CanCommandAt(ent, Transform(args.Target).Coordinates))
            return;

        if (controller.Marker is { } old)
            QueueDel(old);
        controller.Marker = null;
        controller.Anchor = args.Target;
        ResetOrder(controller);
        args.Handled = true;
    }

    private void ResetOrder(CMUSwarmControllerComponent controller)
    {
        controller.Sightings.Clear();
        foreach (var member in controller.Members)
        {
            if (TerminatingOrDeleted(member))
                continue;
            RemComp<NPCMeleeCombatComponent>(member);
            _steering.Unregister(member);
        }
        controller.NextThink = TimeSpan.Zero;
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
        RemComp<NPCMeleeCombatComponent>(ent);
        _steering.Unregister(ent);
        RemComp<ActiveNPCComponent>(ent);
    }

    private void OnControllerShutdown(Entity<CMUSwarmControllerComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Marker is { } marker)
            QueueDel(marker);
        ResetOrder(ent.Comp);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUSwarmControllerComponent>();
        while (query.MoveNext(out var hive, out var controller))
        {
            if (controller.NextThink > _timing.CurTime)
                continue;
            controller.NextThink = _timing.CurTime + controller.ThinkInterval;
            controller.Members.RemoveAll(uid => TerminatingOrDeleted(uid) || !_mobState.IsAlive(uid));
            if (controller.Members.Count == 0)
                continue;

            if (controller.Anchor is not { } anchor || TerminatingOrDeleted(anchor) ||
                (HasComp<XenoComponent>(anchor) && (!_mobState.IsAlive(anchor) || _hive.GetHive(anchor)?.Owner != hive)))
            {
                ResetOrder(controller);
                controller.Anchor = _hive.GetHiveCore((hive, Comp<HiveComponent>(hive)));
                continue;
            }

            // Only actual sightings are shared: walls must prevent initial acquisition.
            var sightings = controller.Sightings;
            var expired = new List<EntityUid>();
            foreach (var (target, seenUntil) in sightings)
            {
                if (controller.Passive || seenUntil <= _timing.CurTime || TerminatingOrDeleted(target) ||
                    !CanAttack(target, hive))
                    expired.Add(target);
            }
            foreach (var target in expired)
                sightings.Remove(target);
            foreach (var member in controller.Members)
            {
                if (controller.Passive)
                    break;
                foreach (var hostile in _factions.GetNearbyHostiles(member, controller.SightRange))
                {
                    if (!CanAttack(hostile, hive) ||
                        (Within(anchor, member, controller.LeashRange) && !Within(anchor, hostile, controller.LeashRange)) ||
                        !_interaction.InRangeUnobstructed(member, hostile, controller.SightRange))
                        continue;
                    sightings[hostile] = _timing.CurTime + controller.TargetMemory;
                }
            }

            var assignedTargets = new Dictionary<EntityUid, int>();
            for (var i = 0; i < controller.Members.Count; i++)
            {
                var member = controller.Members[i];
                if (TryComp<NPCSteeringComponent>(member, out var failed) && failed.Status == SteeringStatus.NoPath)
                    _steering.Unregister(member);
                if (SelectEnemy(member, sightings, assignedTargets) is { } enemy)
                {
                    assignedTargets[enemy] = assignedTargets.GetValueOrDefault(enemy) + 1;
                    var target = BlockingBarricade(member, enemy) ?? enemy;
                    var pursuit = _steering.Register(member, new EntityCoordinates(target, Vector2.Zero));
                    ConfigureSteering(pursuit);
                    pursuit.Range = target == enemy ? 0.2f : 0.6f;
                    EnsureComp<NPCMeleeCombatComponent>(member).Target = target;
                    continue;
                }

                RemComp<NPCMeleeCombatComponent>(member);
                // Fill an expanding disk instead of squeezing every summon onto one ring.
                var angle = i * 2.39996323f;
                var radius = controller.FormationRadius * MathF.Sqrt((i + 0.5f) / 15f);
                var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                var destination = new EntityCoordinates(anchor, offset);
                // If a formation slot is inside a wall, converge on the anchor instead.
                if (!_interaction.InRangeUnobstructed(_transform.GetMapCoordinates(anchor), _transform.ToMapCoordinates(destination), radius + 0.1f))
                    destination = new EntityCoordinates(anchor, Vector2.Zero);
                var steering = _steering.Register(member, destination);
                ConfigureSteering(steering);
                steering.Range = 0.2f;
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

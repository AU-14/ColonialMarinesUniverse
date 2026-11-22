using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Prototypes;
using Content.Shared.CMU14.ConstructionRobot;
using Content.Shared.Construction.Prototypes;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server.CMU14.ConstructionRobot;

public sealed partial class CMUConstructionRobotSystem
{
    private const int MaxGhostsPerOwner = 64;
    private readonly Dictionary<EntityUid, OwnerWork> _owners = [];
    private readonly Dictionary<EntityUid, ConstructionRobotGhostTask> _assignments = [];

    private sealed class OwnerWork
    {
        public readonly HashSet<EntityUid> Robots = [];
        public readonly Dictionary<int, ConstructionRobotGhostTask> Ghosts = [];
    }

    private void InitializeGhostTracking()
    {
        SubscribeLocalEvent<CMUConstructionRobotComponent, ComponentShutdown>(OnRobotShutdown);
        SubscribeLocalEvent<PlayerDetachedEvent>(ev => ReleaseOwner(ev.Entity));
        SubscribeLocalEvent<CMUConstructionRobotOwnerComponent, MobStateChangedEvent>(OnOwnerMobStateChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            foreach (var owner in _owners.Keys.ToArray())
                ReleaseOwner(owner);

            _candidates.Clear();
        });
        SubscribeNetworkEvent<ConstructionRobotGhostPlacedMessage>(OnGhostPlaced);
        SubscribeNetworkEvent<ConstructionRobotGhostRemovedMessage>(OnGhostRemoved);
        SubscribeNetworkEvent<ConstructionRobotClearGhostsMessage>(OnClearGhosts);
    }

    private void RegisterRobot(EntityUid owner, EntityUid robot)
    {
        if (!_owners.TryGetValue(owner, out var work))
        {
            _owners.Add(owner, work = new OwnerWork());
            EnsureComp<CMUConstructionRobotOwnerComponent>(owner);
        }

        work.Robots.Add(robot);
    }

    private void OnRobotShutdown(Entity<CMUConstructionRobotComponent> robot, ref ComponentShutdown args)
    {
        if (robot.Comp.Operator is { } owner)
            UnregisterRobot(owner, robot.Owner);
    }

    private void UnregisterRobot(EntityUid owner, EntityUid robot)
    {
        ReleaseAssignedTask(robot);
        ReleaseActiveBuild(robot);
        if (!_owners.TryGetValue(owner, out var work) || !work.Robots.Remove(robot) || work.Robots.Count != 0)
            return;

        _owners.Remove(owner);
        if (!TerminatingOrDeleted(owner))
            RemComp<CMUConstructionRobotOwnerComponent>(owner);
    }

    private void OnOwnerMobStateChanged(Entity<CMUConstructionRobotOwnerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            ReleaseOwner(ent.Owner);
    }

    private void ReleaseOwner(EntityUid owner)
    {
        if (!_owners.TryGetValue(owner, out var work))
            return;

        foreach (var robot in work.Robots.ToArray())
        {
            UnregisterRobot(owner, robot);
            if (TerminatingOrDeleted(robot) || !TryComp<CMUConstructionRobotComponent>(robot, out var comp))
                continue;

            comp.Operator = null;
            Dirty(robot, comp);
            SetFollowTarget((robot, comp), null, 0.8f);
        }
    }

    private void OnGhostPlaced(ConstructionRobotGhostPlacedMessage ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } owner
            || !_owners.TryGetValue(owner, out var work)
            || work.Ghosts.Count >= MaxGhostsPerOwner
            || work.Ghosts.ContainsKey(ev.GhostId)
            || string.IsNullOrEmpty(ev.Prototype)
            || !_prototypes.TryIndex(ev.Prototype, out ConstructionPrototype? prototype)
            || !CanUseRecipe(owner, prototype)
            || !double.IsFinite(ev.Rotation.Theta)
            || !TryGetEntity(ev.Location.NetEntity, out var parent))
            return;

        var coordinates = new EntityCoordinates(parent.Value, ev.Location.Position);
        if (!coordinates.IsValid(EntityManager)
            || (!HasComp<MapGridComponent>(parent) && !HasComp<MapComponent>(parent)))
            return;

        var position = _transform.ToMapCoordinates(coordinates);
        var ownerPosition = _transform.GetMapCoordinates(owner);
        if (position.MapId != ownerPosition.MapId || position.MapId == MapId.Nullspace
            || !(Vector2.DistanceSquared(ownerPosition.Position, position.Position) <= 20f * 20f)
            || !_examine.InRangeUnOccluded(owner, position, 20f,
                _construction.GetPredicate(prototype.CanBuildInImpassable, position)))
            return;

        if (!TryGetBuildPlan(ev.Prototype, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-construction-robot-unsupported-plan"), owner, owner);
            return;
        }

        var rotation = prototype.CanRotate ? ev.Rotation.GetDir().ToAngle() : Angle.Zero;
        work.Ghosts.Add(ev.GhostId, new ConstructionRobotGhostTask(owner, ev.GhostId, ev.Prototype, coordinates, rotation));
    }

    private bool CanUseRecipe(EntityUid owner, ConstructionPrototype prototype)
    {
        return prototype.Type == ConstructionType.Structure && !prototype.Hide
            && (!CMPrototypeExtensions.FilterCM || prototype.IsCM)
            && _rmcConstruction.CanConstruct(owner)
            && !_containers.IsEntityInContainer(owner)
            && !_whitelist.IsWhitelistFail(prototype.EntityWhitelist, owner);
    }

    private ConstructionRobotGhostTask? GetGhost(EntityUid owner, int ghostId)
        => _owners.TryGetValue(owner, out var work) ? work.Ghosts.GetValueOrDefault(ghostId) : null;

    private void OnGhostRemoved(ConstructionRobotGhostRemovedMessage ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } owner)
            RemoveGhost(owner, ev.GhostId);
    }

    private void RemoveGhost(EntityUid owner, int ghostId)
    {
        if (_owners.TryGetValue(owner, out var work) && work.Ghosts.Remove(ghostId, out var task)
            && task.AssignedRobot is { } robot)
        {
            ReleaseAssignedTask(robot);
            ReleaseActiveBuild(robot);
        }
    }

    private void OnClearGhosts(ConstructionRobotClearGhostsMessage ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } owner)
            ClearGhosts(owner);
    }

    private void ClearGhosts(EntityUid owner)
    {
        if (!_owners.TryGetValue(owner, out var work))
            return;

        foreach (var robot in work.Robots)
        {
            ReleaseAssignedTask(robot);
            ReleaseActiveBuild(robot);
        }

        work.Ghosts.Clear();
    }
}

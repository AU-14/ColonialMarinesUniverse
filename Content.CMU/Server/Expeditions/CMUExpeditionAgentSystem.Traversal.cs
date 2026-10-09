using System.Linq;
using System.Numerics;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.DoAfter;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private CMUZLevelLadderSystem _squadLadders = default!;
    [Dependency] private CMUZLevelsSystem _squadLevels = default!;
    private sealed record SquadPortal(EntityUid Entity, EntityUid From, EntityUid To, EntityCoordinates Entry,
        EntityCoordinates Landing, int Offset, Vector2 Step, bool Ladder);
    private readonly List<SquadPortal> _squadPortals = new();
    private TimeSpan _nextPortalSnapshot;

    private void CancelPortalClimb(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (agent.PortalActivated && TryComp<DoAfterComponent>(uid, out var actions))
            foreach (var action in actions.DoAfters.Values.ToArray())
                if (!action.Cancelled && !action.Completed && action.Args.Target == agent.TravelPortal &&
                    action.Args.Event is CMUZLevelLadderDoAfterEvent)
                    _doAfter.Cancel(action.Id);
        agent.PortalActivated = false;
    }

    private void RefreshPortals(TimeSpan now)
    {
        if (now < _nextPortalSnapshot)
            return;
        _nextPortalSnapshot = now + TimeSpan.FromSeconds(5);
        _squadPortals.Clear();
        var ladders = EntityQueryEnumerator<CMUZLevelLadderComponent, TransformComponent>();
        while (ladders.MoveNext(out var uid, out var ladder, out var transform))
        {
            var magnitude = Math.Max(1, Math.Abs(ladder.Offset));
            if (ladder.CanMoveUp)
                Add(uid, transform, magnitude, Vector2.Zero, true);
            if (ladder.CanMoveDown)
                Add(uid, transform, -magnitude, Vector2.Zero, true);
        }
        var stairs = EntityQueryEnumerator<CMUZLevelStairsComponent, TransformComponent>();
        while (stairs.MoveNext(out var uid, out var stair, out var transform))
        {
            var direction = stair.Offset > 0 ? stair.Direction : stair.Direction.GetOpposite();
            Add(uid, transform, stair.Offset, direction.ToVec(), false);
        }
        void Add(EntityUid uid, TransformComponent transform, int offset, Vector2 step, bool ladder)
        {
            if (transform.MapUid is not { } map || !transform.Anchored ||
                !_squadLevels.TryProjectToZMap(map, offset, _transform.GetWorldPosition(uid) + step, out var projected, out _))
                return;
            var landing = _transform.ToCoordinates(projected);
            if (!TrySquadCoordinates(landing, out landing) || !GroundSafe(landing))
                return;
            _squadPortals.Add(new SquadPortal(uid, map, Transform(landing.EntityId).MapUid!.Value,
                transform.Coordinates, landing, offset, step, ladder));
        }
    }

    private int PortalDistance(EntityUid from, EntityUid goal)
    {
        var visited = new HashSet<EntityUid> { from };
        var queue = new Queue<(EntityUid Map, int Depth)>();
        queue.Enqueue((from, 0));
        while (queue.TryDequeue(out var step) && visited.Count <= 64)
        {
            if (step.Map == goal)
                return step.Depth;
            foreach (var portal in _squadPortals)
                if (portal.From == step.Map && visited.Add(portal.To))
                    queue.Enqueue((portal.To, step.Depth + 1));
        }
        return int.MaxValue;
    }

    private bool FollowLevelOrder(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        var currentMap = Transform(uid).MapUid;
        if (agent.TravelGoal == null && agent.OrderedDestination is { } destination &&
            Transform(destination.EntityId).MapUid != currentMap)
        {
            agent.TravelGoal = destination;
            agent.OrderedDestination = null;
        }
        if (agent.TravelGoal is not { } goal)
            return false;
        if (!Exists(goal.EntityId) || currentMap == null)
        {
            agent.TravelGoal = null;
            return false;
        }
        var goalMap = Transform(goal.EntityId).MapUid;
        if (currentMap == goalMap)
        {
            agent.PortalActivated = false;
            agent.TravelGoal = null;
            agent.TravelPortal = null;
            agent.OrderedDestination = goal;
            agent.Home = Transform(uid).Coordinates;
            agent.OrderRoute.Clear();
            agent.PortalUntil = TimeSpan.Zero;
            Decision(agent, "level-arrival", "resuming-final-order");
            return false;
        }
        RefreshPortals(now);
        if (agent.TravelPortal is { } active)
        {
            var portal = _squadPortals.FirstOrDefault(entry => entry.Entity == active && entry.Offset == agent.TravelOffset);
            if (portal == null || !Exists(active) || portal.From != currentMap)
            {
                CancelPortalClimb(uid, agent);
                agent.TravelPortal = null;
                agent.OrderedDestination = null;
                agent.OrderRoute.Clear();
                agent.PortalUntil = TimeSpan.Zero;
                agent.Home = Transform(uid).Coordinates;
                return true;
            }
            if (now >= agent.PortalUntil && agent.PortalUntil != TimeSpan.Zero)
            {
                CancelPortalClimb(uid, agent);
                agent.FailedPortals[active] = now + TimeSpan.FromSeconds(15);
                agent.TravelPortal = null;
                agent.OrderedDestination = null;
                agent.OrderRoute.Clear();
                agent.PortalUntil = TimeSpan.Zero;
                agent.NextPortalSearch = now + TimeSpan.FromSeconds(1);
                Decision(agent, "level-blocked", "transition-or-approach-timed-out");
                return true;
            }
            if (agent.OrderBlockedSince is { } blocked && now - blocked > TimeSpan.FromSeconds(5))
            {
                agent.PortalUntil = now;
                return true;
            }
            if (!portal.Ladder && _transform.InRange(Transform(uid).Coordinates, portal.Entry, 1.3f) &&
                !_transform.InRange(Transform(uid).Coordinates, portal.Entry, .2f))
            {
                Move(uid, portal.Entry, precise: true, validated: true);
                return true;
            }
            var range = portal.Ladder ? 1.3f : .3f;
            if (!_transform.InRange(Transform(uid).Coordinates, portal.Entry, range))
                return false; // FollowOrders executes the approach using ordinary routes/door handling.
            if (!ValidOrderPoint(uid, portal.Landing))
            {
                agent.PortalUntil = now;
                return true;
            }
            _steering.Unregister(uid);
            if (portal.Ladder)
            {
                if (!agent.PortalActivated && _squadLadders.StartControlledClimb(active, uid, portal.Offset))
                {
                    agent.PortalActivated = true;
                    agent.OrderedDestination = null;
                    agent.PortalUntil = now + Comp<CMUZLevelLadderComponent>(active).Delay + TimeSpan.FromSeconds(2);
                    Decision(agent, "climbing", "native-ladder-action");
                }
            }
            else
            {
                // Cross the native directional stair trigger by walking, with a clear physical approach.
                var exit = _transform.ToCoordinates(portal.Entry.EntityId,
                    _transform.ToMapCoordinates(portal.Entry).Offset(portal.Step));
                if (BodyFits(uid, exit))
                    Move(uid, exit, precise: true, validated: true);
                Decision(agent, "stairs", "crossing-native-stair-trigger");
            }
            return true;
        }
        if (now < agent.NextPortalSearch)
            return true;
        agent.NextPortalSearch = now + TimeSpan.FromSeconds(3);
        foreach (var failed in agent.FailedPortals.Where(pair => pair.Value <= now || !Exists(pair.Key)).Select(pair => pair.Key).ToArray())
            agent.FailedPortals.Remove(failed);
        foreach (var portal in _squadPortals.Where(entry => entry.From == currentMap &&
                     !(agent.FailedPortals.TryGetValue(entry.Entity, out var until) && until > now) &&
                     goalMap is { } targetMap && PortalDistance(entry.To, targetMap) < PortalDistance(entry.From, targetMap))
                     .OrderBy(entry => Vector2.DistanceSquared(_transform.GetWorldPosition(uid), _transform.ToMapCoordinates(entry.Entry).Position)))
        {
            if (!TryPortalApproach(uid, portal, out var approach))
                continue;
            agent.TravelPortal = portal.Entity;
            agent.PortalActivated = false;
            agent.TravelOffset = portal.Offset;
            agent.OrderedDestination = approach;
            agent.OrderRoute.Clear();
            agent.OrderBlockedSince = null;
            agent.PortalUntil = now + TimeSpan.FromSeconds(45);
            agent.Home = Transform(uid).Coordinates;
            Decision(agent, "level-route", portal.Ladder ? "approaching-ladder" : "approaching-stairs");
            return false;
        }
        agent.OrderBlocked = true;
        Decision(agent, "level-blocked", "no-usable-portal-chain");
        return true;
    }

    private bool TryPortalApproach(EntityUid uid, SquadPortal portal, out EntityCoordinates approach)
    {
        approach = default;
        foreach (var candidate in NearbySquadPositions(portal.Entry, portal.Ladder ? 1 : 0))
        {
            if (!TrySquadCoordinates(candidate, out var point) || !ValidOrderPoint(uid, point) ||
                portal.Ladder && (!_transform.InRange(point, portal.Entry, 1.3f) ||
                    !_interaction.InRangeUnobstructed(_transform.ToMapCoordinates(point), _transform.ToMapCoordinates(portal.Entry), 1.3f,
                        predicate: entity => entity == uid || entity == portal.Entity)))
                continue;
            approach = point;
            return true;
        }
        return false;
    }
}

using System.Linq;
using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private ActionBlockerSystem _doorActions = default!;
    [Dependency] private SharedDoorSystem _doors = default!;
    [Dependency] private TagSystem _tags = default!;
    private readonly Dictionary<(EntityUid User, EntityUid Door), bool> _doorPassageCache = new();

    // Planning may cross a usable door; physical movement and cover checks never ignore it.
    private bool CanNavigateDoor(EntityUid uid, EntityUid obstacle)
    {
        if (!TryComp<DoorComponent>(obstacle, out var door))
            return false;
        if (_doorPassageCache.TryGetValue((uid, obstacle), out var usable))
            return usable;
        usable = !(TryComp<CMUExpeditionAgentComponent>(uid, out var agent) &&
                   agent.FailedDoor == obstacle && _timing.CurTime < agent.AvoidDoorUntil) &&
            (door.State == DoorState.Opening ||
                (door.State is DoorState.Closed or DoorState.Denying or DoorState.Closing) &&
                (door.ClickOpen || door.BumpOpen && _tags.HasTag(uid, SharedDoorSystem.DoorBumpTag)) &&
                _doors.CanOpen(obstacle, door, uid));
        _doorPassageCache[(uid, obstacle)] = usable;
        return usable;
    }

    // Route queries also admit native vaults. Move executes those interactions before steering.
    private bool RoutePoint(EntityUid uid, EntityCoordinates point, bool allowVault = true) =>
        GroundSafe(point) && BodyFits(uid, point, planningDoors: true, allowVault: allowVault);

    private bool RoutePassage(EntityUid uid, EntityCoordinates from, EntityCoordinates to, float radius = AgentBodyRadius,
        bool allowVault = true) =>
        TraversablePassage(uid, from, to, radius, planningDoors: true, allowVault: allowVault);

    private bool WaitingAtDoor(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (agent.WaitingForDoor is not { } door)
            return false;
        // Partial opening drops collision before the animation finishes. Resume as soon
        // as the native door permits passage, not when our failure deadline expires.
        if (!TryComp<PhysicsComponent>(door, out var body) || !body.CanCollide)
        {
            agent.WaitingForDoor = null;
            agent.DoorOpenRequested = false;
            agent.DoorDecision = "door-clear";
            PauseTravelClock(agent, _timing.CurTime);
            return false;
        }
        if (_timing.CurTime < agent.DoorWaitUntil)
            return true;
        // Expire before the generic stuck check, so a broken opening animation cannot
        // repeatedly win a new route search and restart the same wait.
        RejectDoor(uid, agent, door, _timing.CurTime);
        return false;
    }

    /// <summary>Approach the first door on this leg, open it once, and wait for real clearance.</summary>
    private bool PrepareDoorPassage(EntityUid uid, CMUExpeditionAgentComponent agent, ref EntityCoordinates destination)
    {
        var start = Transform(uid).Coordinates;
        if (TraversablePassage(uid, start, destination))
        {
            if (agent.WaitingForDoor != null)
            {
                PauseTravelClock(agent, _timing.CurTime);
                agent.DoorDecision = "door-clear";
            }
            agent.WaitingForDoor = null;
            return true;
        }
        var from = _transform.ToMapCoordinates(start);
        var to = _transform.ToMapCoordinates(destination);
        var delta = to.Position - from.Position;
        if (from.MapId != to.MapId || delta.LengthSquared() < 0.01f)
            return true;
        var center = (from.Position + to.Position) / 2;
        var bounds = new Box2Rotated(Box2.CenteredAround(center,
            new Vector2(delta.Length() + AgentBodyRadius * 2, AgentBodyRadius * 2)), delta.ToAngle(), center);
        var fixtures = new HashSet<FixtureProxy>();
        _lookup.GetFixturesIntersecting(from.MapId, bounds, fixtures, new FixtureQueryArgs(new QueryFilter
        {
            MaskBits = (long) MovementMask,
        }, IgnoreShapeSkin: true));
        var obstacle = fixtures.Where(f => f.Fixture.Hard && f.Body.CanCollide && HasComp<DoorComponent>(f.Entity))
            .OrderBy(f => Vector2.DistanceSquared(from.Position, _transform.GetWorldPosition(f.Entity)))
            .Select(f => (EntityUid?) f.Entity).FirstOrDefault();
        if (obstacle is not { } doorUid)
        {
            agent.WaitingForDoor = null;
            return true;
        }
        var now = _timing.CurTime;
        if (!CanNavigateDoor(uid, doorUid) || agent.WaitingForDoor == doorUid && now >= agent.DoorWaitUntil)
        {
            RejectDoor(uid, agent, doorUid, now);
            return false;
        }
        if (!RoutePassage(uid, start, destination))
        {
            // A wall or crate before the door still needs a real route around it.
            agent.WaitingForDoor = null;
            return true;
        }
        // A remote leg may cross several doors. Walk only as far as a physically clear
        // approach; never feed steering a destination inside a closed door's fixture.
        if (!_interaction.InRangeUnobstructed(uid, doorUid, range: 1.5f))
        {
            agent.WaitingForDoor = null;
            var localEnd = _transform.ToCoordinates(start.EntityId, to);
            var steps = Math.Min(32, (int) MathF.Ceiling(delta.Length() / 0.2f));
            var approach = start;
            for (var i = 1; i <= steps; i++)
            {
                var point = new EntityCoordinates(start.EntityId, Vector2.Lerp(start.Position, localEnd.Position, i / (float) steps));
                if (!TraversablePassage(uid, start, point))
                    break;
                approach = point;
            }
            if (_transform.InRange(start, approach, 0.05f))
            {
                RejectDoor(uid, agent, doorUid, now);
                return false;
            }
            destination = approach;
            agent.DoorDecision = "approaching-door";
            return true;
        }
        RequestDoorOpening(uid, agent, doorUid, now);
        return false;
    }

    private bool RequestDoorOpening(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid doorUid, TimeSpan now)
    {
        if (!CanNavigateDoor(uid, doorUid) || agent.WaitingForDoor == doorUid && now >= agent.DoorWaitUntil)
        {
            RejectDoor(uid, agent, doorUid, now);
            return false;
        }
        var door = Comp<DoorComponent>(doorUid);
        if (agent.WaitingForDoor != doorUid)
        {
            agent.WaitingForDoor = doorUid;
            agent.DoorOpenRequested = false;
            agent.DoorWaitUntil = now + TimeSpan.FromSeconds(Math.Clamp(
                (door.OpenTimeOne + door.OpenTimeTwo + door.CloseTimeOne + door.CloseTimeTwo).TotalSeconds + 1, 2, 8));
        }
        // Finish a closing animation before attempting an open; don't toggle squadmates' doors.
        if (!agent.DoorOpenRequested && (door.State is DoorState.Closed or DoorState.Denying))
        {
            agent.DoorOpenRequested = true;
            // Activation keeps interaction restrictions and door-specific handlers. Only
            // bump-only doors need the equivalent native open request, never a toggle.
            if (door.ClickOpen)
                _interaction.InteractionActivate(uid, doorUid);
            else if (_doorActions.CanInteract(uid, doorUid))
                _doors.TryOpen(doorUid, door, uid);
            if (door.State is DoorState.Opening or DoorState.Open)
                agent.DoorsOpened++;
            else
            {
                RejectDoor(uid, agent, doorUid, now);
                return false;
            }
        }
        agent.DoorDecision = "waiting-for-door";
        PauseTravelClock(agent, now);
        _steering.Unregister(uid);
        return true;
    }

    private void RejectDoor(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid door, TimeSpan now)
    {
        agent.WaitingForDoor = null;
        agent.FailedDoor = door;
        agent.AvoidDoorUntil = now + TimeSpan.FromSeconds(8);
        agent.DoorDecision = "door-blocked-repath";
        agent.DoorFailures++;
        agent.Route.Clear();
        agent.RouteDestination = null;
        agent.OrderRoute.Clear();
        agent.NextOrderRoute = now + TimeSpan.FromSeconds(0.3);
        agent.LastMoveFailed = true;
        agent.MoveUntil = now;
        _bodyClearCache.Clear();
        _doorPassageCache[(uid, door)] = false;
        _steering.Unregister(uid);
    }
}

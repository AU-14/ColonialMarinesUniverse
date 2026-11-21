using System.Numerics;
using Content.Server.NPC.Components;
using Content.Shared._RMC14.Atmos;
using Content.Shared.CMU14.ConstructionRobot;
using Content.Shared.Explosion.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Trigger.Components;
using Robust.Shared.Map;

namespace Content.Server.CMU14.ConstructionRobot;

public sealed partial class CMUConstructionRobotSystem
{
    [Dependency] private MobStateSystem _mobs = default!;
    [Dependency] private NpcFactionSystem _factions = default!;

    private readonly HashSet<EntityUid> _hazardsNearby = [];
    private readonly Dictionary<EntityUid, TimeSpan> _hazardRetry = [];

    private bool PauseForHazard(Entity<CMUConstructionRobotComponent> robot)
    {
        if (!robot.Comp.AvoidHazards)
        {
            robot.Comp.AvoidingHazard = false;
            _hazardRetry.Remove(robot.Owner);
            return false;
        }

        if (_hazardRetry.TryGetValue(robot.Owner, out var retry) && _timing.CurTime < retry)
            return true;

        robot.Comp.AvoidingHazard = false;
        _hazardsNearby.Clear();
        _lookup.GetEntitiesInRange(Transform(robot.Owner).Coordinates, 12f, _hazardsNearby, LookupFlags.All);
        var start = _transform.GetMapCoordinates(robot);
        foreach (var hazard in _hazardsNearby)
        {
            if (hazard == robot.Owner || TerminatingOrDeleted(hazard))
                continue;

            var radius = HazardRadius(robot, hazard);
            if (radius <= 0)
                continue;

            var location = _transform.GetMapCoordinates(hazard);
            if (location.MapId != start.MapId)
                continue;

            if (Vector2.DistanceSquared(start.Position, location.Position) <= radius * radius ||
                PathIntersectsHazard(robot.Owner, start, location.Position, radius))
            {
                robot.Comp.AvoidingHazard = true;
                _hazardRetry[robot.Owner] = _timing.CurTime + TimeSpan.FromSeconds(2);
                SetFollowTarget(robot, null, 0.8f);
                if (robot.Comp.BuildDoAfter is { } doAfter)
                    _doAfter.Cancel(doAfter);
                ReleaseAssignedTask(robot.Owner);
                return true;
            }
        }

        _hazardRetry.Remove(robot.Owner);
        return false;
    }

    private float HazardRadius(Entity<CMUConstructionRobotComponent> robot, EntityUid hazard)
    {
        if (robot.Comp.AvoidFire && HasComp<RMCIgniteOnCollideComponent>(hazard) &&
            !HasComp<ProjectileComponent>(hazard) && !_mobs.IsAlive(hazard))
            return 2f;

        if (robot.Comp.AvoidExplosives && HasComp<ActiveTimerTriggerComponent>(hazard) &&
            HasComp<ExplosiveComponent>(hazard))
            return 5f;

        if (!robot.Comp.AvoidHostiles || robot.Comp.Operator is not { } owner ||
            !_mobs.IsAlive(hazard) || !TryComp<NpcFactionMemberComponent>(owner, out var faction) ||
            !TryComp<NpcFactionMemberComponent>(hazard, out var other) ||
            !faction.HostileFactions.Overlaps(other.Factions) ||
            _factions.IsEntityFriendly((owner, faction), (hazard, other)) ||
            _factions.IsIgnored((owner, null), hazard) ||
            !_examine.InRangeUnOccluded(robot.Owner, _transform.GetMapCoordinates(hazard), 12f))
            return 0f;

        return 6f;
    }

    private bool PathIntersectsHazard(EntityUid robot, MapCoordinates start, Vector2 hazard, float radius)
    {
        if (!TryComp<NPCSteeringComponent>(robot, out var steering))
            return false;

        var previous = start.Position;
        var remaining = 6f;
        foreach (var node in steering.CurrentPath)
        {
            if (!node.Coordinates.IsValid(EntityManager))
                break;
            var point = _transform.ToMapCoordinates(node.Coordinates);
            if (point.MapId != start.MapId)
                break;

            var offset = point.Position - previous;
            var length = offset.Length();
            if (length > remaining)
                offset *= remaining / length;
            var segmentLengthSquared = offset.LengthSquared();
            var fraction = segmentLengthSquared > 0
                ? Math.Clamp(Vector2.Dot(hazard - previous, offset) / segmentLengthSquared, 0f, 1f)
                : 0f;
            if (Vector2.DistanceSquared(previous + offset * fraction, hazard) <= radius * radius)
                return true;
            previous += offset;
            remaining -= offset.Length();
            if (remaining <= 0)
                break;
        }

        return false;
    }
}

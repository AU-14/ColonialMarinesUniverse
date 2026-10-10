using System.Numerics;
using Content.Shared.Atmos.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private static void ClearTrafficYield(CMUExpeditionAgentComponent agent)
    {
        agent.TrafficNudgeRequester = null;
        agent.TrafficNudgeDestination = null;
        agent.TrafficNudgeUntil = TimeSpan.Zero;
    }

    private static bool CanAcceptTrafficYield(CMUExpeditionAgentComponent agent) =>
        !agent.HoldPosition && !agent.Entrench && !agent.CornerHolding &&
        (agent.OrderedDestination == null || agent.Patrolling) &&
        (agent.State is CMUExpeditionAgentState.Guard or CMUExpeditionAgentState.Watch or
            CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage or CMUExpeditionAgentState.Recover) &&
        agent.Action == null && agent.Treatment == null && agent.PendingWeapon == null && agent.AimedWeapon == null &&
        agent.FlareItem == null && agent.WorkItem == null && !agent.PreparingWork && agent.ScavengeTarget == null &&
        agent.FireRescueTarget == null && agent.RushTarget == null && agent.SpacingDestination == null &&
        agent.VaultTarget == null && agent.WaitingForDoor == null && agent.CoverAnchor == null && agent.CoverDestination == null;

    private bool TryRequestTrafficYield(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid blocker,
        Vector2 forward, TimeSpan now)
    {
        if (!TryComp<CMUExpeditionAgentComponent>(blocker, out var buddy) || !CanAcceptTrafficYield(buddy) ||
            buddy.TrafficNudgeRequester != null || now < buddy.NextTrafficNudge || HasComp<ActorComponent>(blocker) ||
            HasCoverCommitment(blocker, buddy, now) ||
            TryComp<FlammableComponent>(blocker, out var burning) && burning.OnFire ||
            TryComp<FlammableComponent>(uid, out var requesterBurning) && requesterBurning.OnFire ||
            agent.FireRescueTarget != null)
            return false;
        var origin = _transform.GetMapCoordinates(uid);
        var start = Transform(blocker).Coordinates;
        var bodyPoint = _transform.ToMapCoordinates(start);
        if (origin.MapId != bodyPoint.MapId || !GroundSafe(start) || !GroundSafe(Transform(uid).Coordinates) ||
            !_transform.InRange(start, Transform(uid).Coordinates, 1.5f))
            return false;
        buddy.NextTrafficNudge = now + TimeSpan.FromSeconds(3);
        var localForward = _transform.ToCoordinates(start.EntityId, bodyPoint.Offset(forward)).Position - start.Position;
        var side = new Vector2(-localForward.Y, localForward.X);
        var exposure = ExposureScore(blocker, buddy, start);
        EntityCoordinates threat = default;
        var keepShot = _guns.TryGetGun(blocker, out var gun) && TryAimPoint(blocker, buddy, gun, out threat) &&
            SafeShot(blocker, buddy, gun, threat);
        foreach (var distance in new[] { 0.85f, 1.15f, 1.5f })
        foreach (var direction in new[] { side, -side, Vector2.Normalize(side + localForward),
                     Vector2.Normalize(-side + localForward), localForward })
        {
            var candidate = start.Offset(direction * distance);
            var world = _transform.ToMapCoordinates(candidate);
            // The destination must actually free the requested lane. Merely shoving a
            // stationary body farther down the same one-wide corridor repeats the jam.
            if (SegmentDistance(world.Position, origin.Position, origin.Position + forward * 2) < AgentBodyRadius * 2 + 0.15f ||
                buddy.Home is not { } home || !_transform.InRange(home, candidate, buddy.LeashRange) ||
                !TraversablePassage(blocker, start, candidate, allowVault: false) ||
                !TrafficPassageClear(blocker, start, candidate) || !KnownDangerPassage(blocker, buddy, start, candidate) ||
                !KnownDangerPassage(uid, agent, start, candidate) ||
                CrossesCoveringFire(blocker, buddy, start, candidate) ||
                ExposureScore(blocker, buddy, candidate) > exposure + 0.1f ||
                ExposureScore(blocker, buddy, start.Offset(direction * distance / 2)) > exposure + 0.1f ||
                keepShot && !SafeShot(blocker, buddy, gun, threat, candidate))
                continue;
            buddy.TrafficNudgeRequester = uid;
            buddy.TrafficNudgeDestination = candidate;
            buddy.TrafficNudgeUntil = now + TimeSpan.FromSeconds(2.5);
            buddy.TrafficNudges++;
            buddy.TrafficDecision = "making-room-for-squadmate";
            buddy.FightingPosition = null;
            return true;
        }
        return false;
    }

    private bool UpdateTrafficYield(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.TrafficNudgeRequester is not { } requester)
            return false;
        var start = Transform(uid).Coordinates;
        if (!CanAcceptTrafficYield(agent) || now >= agent.TrafficNudgeUntil ||
            agent.TrafficNudgeDestination is not { } destination || !Exists(requester) || !_mobs.IsAlive(requester) ||
            HasComp<ActorComponent>(uid) || !IsFriendly(uid, requester) ||
            Transform(requester).MapID != Transform(uid).MapID || !_transform.InRange(start, Transform(requester).Coordinates, 3) ||
            TryComp<FlammableComponent>(uid, out var burning) && burning.OnFire ||
            !TraversablePassage(uid, start, destination, allowVault: false) || !TrafficPassageClear(uid, start, destination) ||
            !KnownDangerPassage(uid, agent, start, destination) || CrossesCoveringFire(uid, agent, start, destination))
        {
            ClearTraffic(agent);
            _steering.Unregister(uid);
            agent.TrafficDecision = "clearance-finished";
            return false;
        }
        // Keep the pocket briefly while the requester passes; preserve the current aim
        // and volley. Native movement, collision and gun readiness remain authoritative.
        if (agent.TrafficActiveUntil <= now)
            agent.TrafficTicket = now;
        agent.TrafficGoal = destination;
        agent.TrafficActiveUntil = now + TimeSpan.FromSeconds(0.6);
        PauseTravelClock(agent, now);
        if (_transform.InRange(start, destination, 0.12f))
            _steering.Unregister(uid);
        else
            Move(uid, destination, precise: true, validated: true);
        return true;
    }
}

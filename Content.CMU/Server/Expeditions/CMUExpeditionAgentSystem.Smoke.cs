using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Expeditions;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool FindSmokeWithdrawal(EntityUid uid, CMUExpeditionAgentComponent agent,
        EntityCoordinates? landing, EntityCoordinates? threat, out EntityCoordinates destination)
    {
        destination = default;
        var start = Transform(uid).Coordinates;
        if (agent.Home is not { } home)
            return false;
        var origin = _transform.ToMapCoordinates(start);
        if (threat is { } missing && (!Exists(missing.EntityId) || _transform.ToMapCoordinates(missing).MapId != origin.MapId))
            threat = null;
        var bearing = threat is { } contact ? _transform.ToMapCoordinates(contact).Position - origin.Position : Vector2.Zero;
        var exposure = ExposureScore(uid, agent, start);
        var candidates = new List<EntityCoordinates>();
        if (agent.SmokeWithdrawalDestination is { } planned)
            candidates.Add(planned);
        if (agent.LastSafePosition is { } safe && Exists(safe.EntityId) && _transform.InRange(start, safe, 8))
            candidates.Add(safe);
        foreach (var distance in new[] { 4f, 7f })
        for (var direction = 0; direction < 8; direction++)
        {
            var angle = direction * MathF.Tau / 8;
            candidates.Add(start.Offset(new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance));
        }
        var choices = new List<(EntityCoordinates Point, float Score)>();
        foreach (var point in candidates)
        {
            if (!Exists(point.EntityId) || !_transform.InRange(home, point, agent.LeashRange) ||
                _transform.InRange(start, point, 1.5f) || SmokeAt(point) ||
                landing is { } screen && _transform.InRange(point, screen, agent.SmokeWithdrawalClearance) ||
                !GroundSafe(point) || !BodyFits(uid, point) || EscapeReserved(uid, point) ||
                !TrafficPassageClear(uid, point, point) || ExposureScore(uid, agent, point) > exposure + 0.5f ||
                MeleeClearance(agent, point) < Math.Min(agent.MeleeStandoffRange, MeleeClearance(agent, start)))
                continue;
            var offset = _transform.ToMapCoordinates(point).Position - origin.Position;
            var sheltered = threat is { } known && Sheltered(uid, point, known);
            if (!sheltered && Vector2.Dot(offset, bearing) > 0)
                continue;
            var away = bearing.LengthSquared() > 0.01f ? -Vector2.Dot(offset, Vector2.Normalize(bearing)) : 0;
            choices.Add((point, (sheltered ? 12 : 0) + away - offset.Length() * 0.25f -
                ExposureScore(uid, agent, point) - FriendlyCrowding(uid, point) * 2));
        }
        var previousRoute = agent.Route.ToArray();
        var previousDestination = agent.RouteDestination;
        foreach (var choice in choices.OrderByDescending(choice => choice.Point == agent.SmokeWithdrawalDestination)
                     .ThenByDescending(choice => choice.Score).Take(3))
        {
            if (!BuildTacticalRoute(uid, agent, choice.Point, allowVaults: false))
                continue;
            var previous = start;
            var valid = true;
            foreach (var step in agent.Route)
            {
                valid &= KnownDangerPassage(uid, agent, previous, step) &&
                    ExposureScore(uid, agent, step) <= exposure + 0.5f;
                previous = step;
            }
            if (!valid)
                continue;
            destination = choice.Point;
            return true;
        }
        agent.Route.Clear();
        foreach (var step in previousRoute)
            agent.Route.Enqueue(step);
        agent.RouteDestination = previousDestination;
        return false;
    }

    private bool BeginSmokeWithdrawal(EntityUid uid, CMUExpeditionAgentComponent agent,
        EntityCoordinates? landing, EntityCoordinates? threat, TimeSpan now)
    {
        if (!FindSmokeWithdrawal(uid, agent, landing, threat, out var destination))
        {
            agent.IncomingFireDecision = "smoke-no-safe-withdrawal";
            return false;
        }
        var route = agent.Route.ToArray();
        CancelAimedWeapon(agent);
        CancelFlare(uid, agent);
        BeginCombatSpacing(uid, agent, now);
        agent.IncomingEscapeDestination = null;
        foreach (var step in route)
            agent.Route.Enqueue(step);
        agent.RouteDestination = destination;
        BeginMove(uid, agent, destination, CMUExpeditionAgentState.PlanMove, now);
        agent.SmokeEscapeDestination = destination;
        agent.SmokeEscapeUntil = agent.MoveUntil = now + TimeSpan.FromSeconds(8);
        agent.Goal = CMUTacticalGoal.Recover;
        agent.IncomingFireDecision = "withdrawing-through-smoke";
        Decision(agent, "smoke-withdrawal", "leaving-screen-for-safer-ground");
        ReadyRifle(uid, agent);
        return true;
    }

    private bool RunSmokeWithdrawal(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.SmokeEscapeDestination is { } destination)
        {
            if (now < agent.SmokeEscapeUntil && agent.RushTarget == null &&
                _transform.InRange(Transform(uid).Coordinates, destination, 0.6f) &&
                !SmokeAt(Transform(uid).Coordinates) &&
                KnownDangerCost(agent, Transform(uid).Coordinates) < 1 && GroundSafe(Transform(uid).Coordinates))
            {
                _steering.Unregister(uid);
                _physics.SetLinearVelocity(uid, Vector2.Zero);
                agent.State = CMUExpeditionAgentState.Watch;
                TryTreat(uid, agent, agent.LastDamage, now);
                ReadyRifle(uid, agent);
                Decision(agent, "smoke-withdrawal", "recovering-after-withdrawal");
                return true;
            }
            // Losing sight or covering fire is expected. Only arrival, a blocked/unsafe
            // route, an immediate hazard or the fixed deadline ends this commitment.
            var next = agent.Route.TryPeek(out var waypoint) ? waypoint : destination;
            if (agent.CoverDestination == destination && agent.State == CMUExpeditionAgentState.PlanMove &&
                now < agent.SmokeEscapeUntil && agent.RushTarget == null &&
                !SmokeAt(destination) && GroundSafe(next) && KnownDangerPassage(uid, agent, Transform(uid).Coordinates, next) &&
                ContinueMove(uid, agent, Transform(uid), now))
            {
                ReadyRifle(uid, agent);
                Decision(agent, "smoke-withdrawal", "continuing-to-safer-ground");
                return true;
            }
            agent.SmokeEscapeDestination = null;
            if (agent.CoverDestination == destination)
            {
                ClearCover(agent);
                _steering.Unregister(uid);
            }
            if (agent.State == CMUExpeditionAgentState.PlanMove)
                agent.State = CMUExpeditionAgentState.Guard;
            agent.NextPlan = now;
        }
        if (now < agent.NextSmokeEscape || agent.RushTarget != null || !SmokeAt(Transform(uid).Coordinates) ||
            now >= agent.IncomingFireUntil && agent.Duty != CMUSquadDuty.Recover &&
            agent.SquadPhase != "withdraw" && agent.LastDamage < agent.RetreatDamage)
            return false;
        agent.NextSmokeEscape = now + TimeSpan.FromSeconds(1);
        return BeginSmokeWithdrawal(uid, agent, null,
            now < agent.ReturnFireUntil ? agent.ReturnFirePoint : agent.LastSeen, now);
    }
}

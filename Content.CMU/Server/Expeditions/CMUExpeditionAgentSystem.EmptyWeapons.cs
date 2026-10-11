using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool RunEmptyWeaponResponse(EntityUid uid, CMUExpeditionAgentComponent agent,
        TransformComponent transform, float damage, TimeSpan now)
    {
        var recentlyHit = now - agent.LastHit < TimeSpan.FromSeconds(2);
        var danger = GrenadeDanger(transform.Coordinates);
        if (!recentlyHit && !danger && (agent.LastSeen == null || now >= agent.ForgetAt))
            return false; // Quiet agents may follow orders, resupply or receive squad deliveries.

        TryLastResortStrike(uid, agent);
        // Finish an escape step instead of restarting it on each hit or loot scan.
        if (agent.State == CMUExpeditionAgentState.OutOfAmmo && ContinueMove(uid, agent, transform, now))
            return true;
        if (!recentlyHit && !danger && RunAmmoFallback(uid, agent, now))
            return true;

        var sheltered = ShelteredFromKnownThreats(uid, agent, transform.Coordinates);
        if (agent.State != CMUExpeditionAgentState.OutOfAmmo ||
            now >= agent.NextRetreat && (recentlyHit || danger || !sheltered))
            BeginRetreat(uid, agent, transform, false, now);
        if (agent.CoverDestination == null && sheltered && !recentlyHit && !danger)
        {
            agent.WeaponDecision = "empty-sheltered-awaiting-resupply";
            TryTreat(uid, agent, damage, now);
        }
        return true;
    }

    private EntityCoordinates? FindAmmoEscape(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates start)
    {
        if (agent.LastSeen is not { } threat || _timing.CurTime >= agent.ForgetAt ||
            agent.Home is not { } home || _transform.ToMapCoordinates(threat).MapId != _transform.ToMapCoordinates(start).MapId)
            return null;
        var away = start.Position - _transform.ToCoordinates(start.EntityId, _transform.ToMapCoordinates(threat)).Position;
        away = away.LengthSquared() > 0.01f ? Vector2.Normalize(away) : Vector2.UnitX;
        var side = new Vector2(-away.Y, away.X);
        var currentGap = Gap(start);
        var currentExposure = ExposureScore(uid, agent, start);
        var bestScore = float.MinValue;
        EntityCoordinates? best = null;
        // Sixteen short corridors after the bounded cover search has found no shelter.
        // Evaluate all known attack directions without tracking unseen enemy movement.
        var stride = CombatStride(uid);
        foreach (var length in new[] { stride, Math.Max(0.6f, stride * 0.5f) })
        {
            for (var direction = 0; direction < 8; direction++)
            {
                var radians = direction * MathF.PI / 4;
                var candidate = start.Offset((away * MathF.Cos(radians) + side * MathF.Sin(radians)) * length);
                if (!_transform.InRange(home, candidate, agent.LeashRange) || Reserved(uid, candidate) ||
                    GrenadeDanger(candidate) || CoverHistoryCost(agent, candidate) >= 6 ||
                    agent.FailedPosition is { } failed && _timing.CurTime < agent.AvoidPositionUntil &&
                    _transform.InRange(candidate, failed, 1) || !TraversablePassage(uid, start, candidate))
                    continue;
                var gap = Gap(candidate);
                var exposure = ExposureScore(uid, agent, candidate);
                // Less exposure or more distance must justify moving; never back into a
                // different nearby attacker merely to get farther from the selected target.
                if (gap < Math.Min(currentGap, 2) ||
                    exposure >= currentExposure - 0.25f && gap < currentGap + 0.4f)
                    continue;
                var score = (currentExposure - exposure) * 4 + (gap - currentGap) -
                    FriendlyCrowding(uid, candidate) * 1.5f;
                if (score <= bestScore)
                    continue;
                bestScore = score;
                best = candidate;
            }
        }
        return best;

        float Gap(EntityCoordinates point)
        {
            var position = _transform.ToMapCoordinates(point).Position;
            var closest = Vector2.Distance(position, _transform.ToMapCoordinates(threat).Position);
            foreach (var other in agent.VisibleThreats)
                closest = Math.Min(closest, Vector2.Distance(position, _transform.ToMapCoordinates(other).Position));
            return closest;
        }
    }
}

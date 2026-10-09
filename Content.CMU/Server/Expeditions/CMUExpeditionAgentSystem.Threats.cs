using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool FlankingContact(EntityUid uid, EntityCoordinates previous, EntityCoordinates contact)
    {
        var origin = _transform.GetWorldPosition(uid);
        var oldBearing = _transform.ToMapCoordinates(previous).Position - origin;
        var newBearing = _transform.ToMapCoordinates(contact).Position - origin;
        return oldBearing.LengthSquared() > 0.01f && newBearing.LengthSquared() > 0.01f &&
            Vector2.Dot(Vector2.Normalize(oldBearing), Vector2.Normalize(newBearing)) < 0.5f;
    }

    private void UpdateThreatSectors(EntityUid uid, CMUExpeditionAgentComponent agent,
        List<(EntityUid Target, float Distance)> visible, TimeSpan now)
    {
        agent.ExposureScores.Clear();
        Array.Clear(agent.ThreatSectors);
        agent.OccupiedThreatSectors = 0;
        agent.Crossfire = false;
        var expired = new List<EntityUid>();
        foreach (var (shooter, until) in agent.RecentShooters)
            if (now >= until || !Exists(shooter))
                expired.Add(shooter);
        foreach (var shooter in expired)
            agent.RecentShooters.Remove(shooter);

        var origin = _transform.GetWorldPosition(uid);
        var sectorCounts = new int[agent.ThreatSectors.Length];
        foreach (var (target, distance) in visible)
        {
            var offset = _transform.GetWorldPosition(target) - origin;
            var angle = MathF.Atan2(offset.Y, offset.X) + MathF.PI;
            var sector = Math.Min(7, (int) (angle * (8 / MathF.Tau)));
            sectorCounts[sector]++;
            var weight = 1 + Math.Clamp((agent.FireRange - distance) / agent.FireRange, 0, 1);
            if (agent.RecentShooters.ContainsKey(target))
                weight += 2;
            if (IsMeleeThreat(target) && distance < agent.MeleeStandoffRange + 2)
                weight += 2;
            if (agent.ThreatSectors[sector] is { } previous && previous.Weight >= weight)
                continue;
            if (agent.ThreatSectors[sector] == null)
                agent.OccupiedThreatSectors++;
            agent.ThreatSectors[sector] = new CMUExpeditionThreat(target, Transform(target).Coordinates, weight);
        }
        for (var sector = 0; sector < agent.ThreatSectors.Length; sector++)
            if (agent.ThreatSectors[sector] is { } contact)
                agent.ThreatSectors[sector] = contact with { Weight = Math.Min(6, contact.Weight + (sectorCounts[sector] - 1) * 0.35f) };

        // Use actual bearings rather than sector indices at the wraparound/bucket boundaries.
        for (var first = 0; first < agent.ThreatSectors.Length; first++)
        {
            if (agent.ThreatSectors[first] is not { } a)
                continue;
            var towardA = _transform.ToMapCoordinates(a.Position).Position - origin;
            if (towardA.LengthSquared() < 0.01f)
                continue;
            for (var second = first + 1; second < agent.ThreatSectors.Length; second++)
            {
                if (agent.ThreatSectors[second] is not { } b)
                    continue;
                var towardB = _transform.ToMapCoordinates(b.Position).Position - origin;
                if (towardB.LengthSquared() > 0.01f &&
                    Vector2.Dot(Vector2.Normalize(towardA), Vector2.Normalize(towardB)) < 0.5f)
                    agent.Crossfire = true;
            }
        }

        agent.TargetAssignments.Clear();
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
        {
            if (other == uid || !SameSquad(uid, agent, other, buddy) || !_mobs.IsAlive(other) || HasComp<ActorComponent>(other) ||
                buddy.Target is not { } target || buddy.ContactFromRadio || now - buddy.LastContact > TimeSpan.FromSeconds(1) ||
                !_transform.InRange(Transform(uid).Coordinates, Transform(other).Coordinates, 12) ||
                buddy.State is not (CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage or
                    CMUExpeditionAgentState.Peeking or CMUExpeditionAgentState.HoldAngle))
                continue;
            agent.TargetAssignments.TryGetValue(target, out var assigned);
            agent.TargetAssignments[target] = assigned + 1;
        }
    }

    private static bool SectorTarget(CMUExpeditionAgentComponent agent, EntityUid target)
    {
        foreach (var threat in agent.ThreatSectors)
            if (threat?.Target == target)
                return true;
        return false;
    }

    private float ExposureScore(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates point)
    {
        if (agent.ExposureScores.TryGetValue(point, out var score))
            return score;
        score = 0;
        foreach (var threat in agent.ThreatSectors)
            if (threat is { } contact && !Sheltered(uid, point, contact.Position))
                score += contact.Weight;
        agent.ExposureScores[point] = score;
        return score;
    }

    private bool CanMoveUnderCoveringFire(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        var available = 0;
        var covering = 0;
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
        {
            if (other == uid || !SameSquad(uid, agent, other, buddy) || !_mobs.IsAlive(other) || HasComp<ActorComponent>(other) ||
                !_transform.InRange(Transform(uid).Coordinates, Transform(other).Coordinates, 12) ||
                buddy.Target == null || _timing.CurTime - buddy.LastContact > TimeSpan.FromSeconds(2) ||
                buddy.State is CMUExpeditionAgentState.Disabled or CMUExpeditionAgentState.Incapacitated or
                    CMUExpeditionAgentState.RecoverWeapon or CMUExpeditionAgentState.OutOfAmmo or CMUExpeditionAgentState.Reloading)
                continue;
            available++;
            if (buddy.SpacingDestination == null && _timing.CurTime - buddy.LastShotAt < TimeSpan.FromSeconds(1) &&
                buddy.State is CMUExpeditionAgentState.Engage or CMUExpeditionAgentState.HoldAngle or CMUExpeditionAgentState.Recover)
                covering++;
        }
        return available == 0 || covering >= (available + 1) / 2;
    }

    private static void ClearThreatAssessment(CMUExpeditionAgentComponent agent)
    {
        agent.RecentShooters.Clear();
        agent.TargetAssignments.Clear();
        agent.ExposureScores.Clear();
        Array.Clear(agent.ThreatSectors);
        agent.Crossfire = false;
        agent.OccupiedThreatSectors = 0;
    }

    private EntityCoordinates? CrossfireStep(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates start)
    {
        if (agent.Target is not { } target || !Visible(uid, target, agent.FireRange))
            return null;
        var threat = Transform(target).Coordinates;
        var currentExposure = ExposureScore(uid, agent, start);
        var bestScore = float.MinValue;
        EntityCoordinates? best = null;
        // Comparative protection is useful even when nobody can hide from every attacker.
        // This is a fighting position, never a shelter for healing/reloading.
        var stride = CombatStride(uid);
        foreach (var length in new[] { stride, Math.Max(0.6f, stride * 0.5f) })
        for (var direction = 0; direction < 8; direction++)
        {
            var angle = direction * MathF.Tau / 8;
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * length;
            var candidate = start.Offset(offset);
            if (agent.Home is not { } home || !_transform.InRange(home, candidate, agent.LeashRange) ||
                Reserved(uid, candidate) || !TraversablePassage(uid, start, candidate) ||
                !ClearLane(uid, start, candidate, 0.35f, movement: true) ||
                !FiringLaneClear(uid, candidate, threat) || !_transform.InRange(candidate, threat, agent.FireRange - 0.5f) ||
                agent.FailedPosition is { } failed && _timing.CurTime < agent.AvoidPositionUntil && _transform.InRange(candidate, failed, 1) ||
                MeleeClearance(agent, candidate) < agent.MeleeStandoffRange)
                continue;
            var exposure = ExposureScore(uid, agent, candidate);
            if (exposure > currentExposure - 1 || ExposureScore(uid, agent, start.Offset(offset * 0.5f)) > currentExposure + 0.5f)
                continue;
            var score = (currentExposure - exposure) * 2 - length * 0.2f;
            if (score <= bestScore)
                continue;
            bestScore = score;
            best = candidate;
        }
        return best;
    }
}

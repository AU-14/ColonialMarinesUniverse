using System.Numerics;
using Content.Server.NPC.Components;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Movement.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private float CombatStride(EntityUid uid) => TryComp<MovementSpeedModifierComponent>(uid, out var speed)
        ? Math.Clamp(speed.CurrentSprintSpeed * 1.6f, 0.65f, 3f) : 3;

    private bool IsMeleeThreat(EntityUid target) => HasComp<XenoComponent>(target) ||
        HasComp<MeleeWeaponComponent>(target) && !_guns.TryGetGun(target, out _);

    private float RushDistance(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid target, float distance)
    {
        var offset = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(uid);
        var velocity = TryComp<PhysicsComponent>(target, out var body) ? body.LinearVelocity : Vector2.Zero;
        var closing = offset.LengthSquared() > 0.01f ? -Vector2.Dot(velocity, Vector2.Normalize(offset)) : 0;
        return distance - Math.Max(0, closing) * agent.RushLookAhead;
    }

    private bool KeepCombatSpacing(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.RushTarget != null)
        {
            if (now >= agent.SpacingUntil)
                BeginCombatSpacing(uid, agent, now);
            agent.SpacingUntil = now + TimeSpan.FromSeconds(1);
        }
        else if (now >= agent.SpacingUntil && agent.Crossfire && now >= agent.NextCrossfireMove &&
            !HasCoverCommitment(uid, agent, now) &&
            agent.Action == null && agent.State is CMUExpeditionAgentState.Guard or CMUExpeditionAgentState.Recover or CMUExpeditionAgentState.Watch &&
            now - agent.LastShotAt < TimeSpan.FromSeconds(1.5) &&
            (now - agent.LastHit < TimeSpan.FromSeconds(2) || agent.RecentShooters.Count >= 2))
        {
            agent.NextCrossfireMove = now + TimeSpan.FromSeconds(3);
            if (CrossfireStep(uid, agent, Transform(uid).Coordinates) is { } step)
            {
                BeginCombatSpacing(uid, agent, now);
                if (!CanMoveUnderCoveringFire(uid, agent))
                    return false;
                agent.SpacingUntil = agent.SpacingMoveUntil = now + TimeSpan.FromSeconds(2);
                agent.SpacingDestination = step;
                agent.SpacingDecision = "reducing-crossfire";
                agent.CrossfireMoves++;
                Move(uid, step, validated: true);
            }
        }
        if (now >= agent.SpacingUntil)
        {
            if (agent.SpacingUntil != TimeSpan.Zero)
                StopSpacing(uid, agent);
            return false;
        }
        if (!ManeuverSupported(uid, agent, now))
        {
            StopSpacing(uid, agent);
            agent.State = CMUExpeditionAgentState.Recover;
            agent.FireAt = now;
            return false;
        }

        var start = Transform(uid).Coordinates;
        if (agent.SpacingDestination is { } destination)
        {
            var exposedStep = agent.ExposedStepUntil > now;
            var arrived = _transform.InRange(start, destination, exposedStep ? 0.3f : 0.5f);
            var blocked = !KnownDangerPassage(uid, agent, start, destination) ||
                TryComp<NPCSteeringComponent>(uid, out var steering) && steering.Status == SteeringStatus.NoPath;
            // Keep a committed escape step unless it now leads towards the closest attacker.
            var closingGap = agent.MeleeThreats.Count > 0 &&
                MeleeClearance(agent, destination) < MeleeClearance(agent, start) - 0.25f;
            if (arrived || blocked || closingGap || now >= agent.SpacingMoveUntil)
            {
                _steering.Unregister(uid);
                ReleaseManeuver(uid, agent);
                agent.SpacingDestination = null;
                if (blocked || !arrived && now >= agent.SpacingMoveUntil)
                {
                    agent.FailedPosition = destination;
                    agent.AvoidPositionUntil = now + TimeSpan.FromSeconds(3);
                }
            }
            else
                Move(uid, destination, precise: exposedStep, validated: true);
        }
        if (agent.SpacingDestination == null && agent.RushTarget != null && now >= agent.NextSpacingSearch)
        {
            agent.NextSpacingSearch = now + TimeSpan.FromSeconds(0.65);
            if (EscapeStep(uid, agent, start) is { } escape)
            {
                agent.SpacingDestination = escape;
                agent.SpacingMoveUntil = now + TimeSpan.FromSeconds(2);
                agent.SpacingDecision = "backing-away";
                Move(uid, escape, validated: true);
            }
            else
                agent.SpacingDecision = "trapped-returning-fire";
        }
        var armed = ReadyRifle(uid, agent);
        if (!armed)
            TryLastResortStrike(uid, agent);
        if (armed && agent.Target is { } target && Visible(uid, target, agent.FireRange) &&
            agent.State is not (CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage) && now >= agent.FireAt)
            Aim(agent, now, true);
        return true;
    }

    private void BeginCombatSpacing(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        ClearScavenging(uid, agent);
        agent.ContactDestination = null;
        CancelWork(uid, agent);
        CancelPlan(uid, agent, false);
        CancelTreatment(agent);
        ClearCover(agent);
        ClearTraffic(agent);
        StopSpacing(uid, agent);
        agent.NextSpacingSearch = now;
        // Only the tactical response is accelerated; native wield/fire delays still apply.
        agent.State = CMUExpeditionAgentState.Guard;
        agent.FireAt = now;
    }

    private EntityCoordinates? EscapeStep(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates start)
    {
        if (agent.RushPosition is not { } rush)
            return null;
        var threat = _transform.ToCoordinates(start.EntityId, _transform.ToMapCoordinates(rush));
        var away = start.Position - threat.Position;
        if (away.LengthSquared() < 0.01f)
            away = Vector2.UnitX;
        away = Vector2.Normalize(away);
        var side = new Vector2(-away.Y, away.X);
        var currentGap = MeleeClearance(agent, start);
        var bestScore = float.MinValue;
        EntityCoordinates? best = null;
        // Fourteen short, direct escape corridors. No long A* job or changing goal each tick.
        var stride = CombatStride(uid);
        foreach (var length in new[] { stride, Math.Max(0.6f, stride * 0.5f) })
        {
            foreach (var degrees in new[] { 0, 30, -30, 60, -60, 90, -90 })
            {
                var radians = degrees * MathF.PI / 180;
                var offset = (away * MathF.Cos(radians) + side * MathF.Sin(radians)) * length;
                var candidate = start.Offset(offset);
                var gap = MeleeClearance(agent, candidate);
                if (gap < currentGap + 0.4f || MeleeClearance(agent, start.Offset(offset * 0.5f)) < currentGap - 0.2f ||
                    agent.Home is not { } home || !_transform.InRange(home, candidate, agent.LeashRange) ||
                    Reserved(uid, candidate) || agent.FailedPosition is { } failed && _timing.CurTime < agent.AvoidPositionUntil &&
                    _transform.InRange(candidate, failed, 1) || !TraversablePassage(uid, start, candidate) ||
                    !ClearLane(uid, start, candidate, 0.35f, movement: true))
                    continue;
                var score = Math.Min(gap, agent.MeleeStandoffRange + 2) - length * 0.15f +
                    (FiringLaneClear(uid, candidate, threat) ? 1 : 0) -
                    ExposureScore(uid, agent, candidate) * (currentGap < 2 ? 0.25f : 0.6f) -
                    FriendlyCrowding(uid, candidate) * 1.5f -
                    (agent.SquadPhase == "anti-rush" && agent.DutyPoint is { } arc ?
                        Vector2.Distance(_transform.ToMapCoordinates(candidate).Position, _transform.ToMapCoordinates(arc).Position) * .25f : 0);
                if (score <= bestScore)
                    continue;
                bestScore = score;
                best = candidate;
            }
        }
        return best;
    }

    private float MeleeClearance(CMUExpeditionAgentComponent agent, EntityCoordinates position)
    {
        var from = _transform.ToMapCoordinates(position).Position;
        var closest = float.MaxValue;
        foreach (var (point, velocity) in agent.MeleeThreats)
        {
            var threat = _transform.ToMapCoordinates(point).Position;
            closest = Math.Min(closest, Math.Min(Vector2.Distance(from, threat),
                Vector2.Distance(from, threat + velocity * agent.RushLookAhead)));
        }
        return closest;
    }

    private void StopSpacing(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (agent.SpacingDestination != null)
        {
            _steering.Unregister(uid);
            ReleaseManeuver(uid, agent);
        }
        agent.SpacingDestination = null;
        agent.SpacingUntil = TimeSpan.Zero;
        agent.SpacingDecision = "idle";
        agent.ExposedStepUntil = TimeSpan.Zero;
        agent.ExposureMovementDecision = "settled";
    }

    private bool TryDisperse(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (now < agent.NextDispersion || agent.CoverAnchor != null || agent.LastSeen is not { } threat ||
            agent.State is not (CMUExpeditionAgentState.Guard or CMUExpeditionAgentState.Recover or CMUExpeditionAgentState.Watch))
            return false;
        agent.NextDispersion = now + TimeSpan.FromSeconds(3);
        var start = Transform(uid).Coordinates;
        var crowded = false;
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out _))
        {
            // One of a pair yields. Otherwise two guards repeatedly sidestep together.
            if (other.CompareTo(uid) < 0 && _mobs.IsAlive(other) && IsFriendly(uid, other) &&
                _transform.InRange(start, Transform(other).Coordinates, 1.5f))
            {
                crowded = true;
                break;
            }
        }
        return crowded && TryAdjustPeek(uid, agent, threat, now);
    }
}

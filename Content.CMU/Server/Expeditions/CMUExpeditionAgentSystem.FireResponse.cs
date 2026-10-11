using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private FlammableSystem _nativeFire = default!;
    [Dependency] private SharedRMCFlammableSystem _rmcFireResponse = default!;

    private void CancelFireResponse(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        ClearFireEscape(uid, agent);
        if (agent.FireRescueTarget == null)
            return;
        if (agent.FireRescueDestination is { } owned && agent.CoverDestination == owned && agent.Action == null)
        {
            _steering.Unregister(uid);
            ClearCover(agent);
            agent.State = CMUExpeditionAgentState.Guard;
        }
        agent.FireRescueTarget = null;
        agent.FireRescueDestination = null;
        agent.FireRescueUntil = TimeSpan.Zero;
        agent.FireRescueHandUntil = TimeSpan.Zero;
        agent.NextFireAssist = _timing.CurTime + TimeSpan.FromSeconds(2);
        if (!HasComp<ActorComponent>(uid) && agent.Action == null && agent.Treatment == null && agent.PendingWeapon == null)
            SelectHeldRifle(uid, agent);
    }

    private bool RunFireResponse(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        var start = Transform(uid).Coordinates;
        if (RunFireEscape(uid, agent, now))
            return true;
        if (TryComp<FlammableComponent>(uid, out var burning) && burning.OnFire)
        {
            CancelFireResponse(uid, agent);
            // Prefer leaving ignition before the native roll's paralysis. With no safe exit,
            // permit one last-resort roll per ground-fire exposure, never a stand/roll loop.
            var onUnsafeGround = !GroundSafe(start);
            if (onUnsafeGround && agent.ResistedGroundFire || now < agent.NextFireResist ||
                !_doorActions.CanInteract(uid, null))
                return false;
            CancelAgentActivity(uid, agent, "extinguishing-self");
            agent.NextFireResist = now + TimeSpan.FromSeconds(1);
            _rmcFireResponse.DoStopDropRollAnimation(uid);
            if (!_nativeFire.Resist(uid, burning))
                return false;
            agent.ResistedGroundFire = onUnsafeGround;
            agent.FireRolls++;
            agent.FireResponseDecision = "native-stop-drop-roll";
            Decision(agent, "fire-response", agent.FireResponseDecision);
            return true;
        }
        if (!TryComp<FirePatterComponent>(uid, out var patter) || agent.RushTarget != null ||
            now - agent.LastHit < TimeSpan.FromSeconds(1) || agent.LastDamage >= agent.RetreatDamage ||
            agent.UtilityCleanupItem != null || agent.Action != null || agent.Treatment != null || agent.TreatmentMedicine != null || agent.PendingWeapon != null ||
            agent.WorkItem != null || agent.PreparingWork || agent.FlareItem != null || agent.ScavengeTarget != null ||
            agent.SpacingDestination != null || agent.RecoveryUntil > now ||
            !GroundSafe(start) || !ShelteredFromKnownThreats(uid, agent, start) ||
            agent.HoldPosition && agent.OrderedDestination != null || !_doorActions.CanInteract(uid, null) ||
            !_doorActions.CanComplexInteract(uid))
        {
            CancelFireResponse(uid, agent);
            return false;
        }
        if (agent.FireRescueTarget == null)
        {
            if (now < agent.NextFireAssist || CommittedMovement(agent) || HasCoverCommitment(uid, agent, now))
                return false;
            agent.NextFireAssist = now + TimeSpan.FromSeconds(1);
            var nearby = new HashSet<EntityUid>();
            _lookup.GetEntitiesInRange(uid, 4, nearby);
            EntityUid? best = null;
            EntityCoordinates? stance = null;
            var nearest = float.MaxValue;
            foreach (var other in nearby)
            {
                if (!BurningAlly(uid, agent, other, patter) || FireAllyClaimed(uid, other, now))
                    continue;
                var point = Transform(other).Coordinates;
                var distance = Vector2.DistanceSquared(_transform.ToMapCoordinates(start).Position,
                    _transform.ToMapCoordinates(point).Position);
                if (distance >= nearest)
                    continue;
                var inReach = _interaction.InRangeUnobstructed(uid, other, 1.25f);
                if (!inReach && (agent.HoldPosition || agent.OrderedDestination != null && !agent.Patrolling))
                    continue;
                var approach = inReach ? start : FireAidStance(uid, agent, other);
                if (approach == null)
                    continue;
                best = other;
                stance = approach;
                nearest = distance;
            }
            if (best == null)
                return false;
            ReleaseManeuver(uid, agent);
            CancelAimedWeapon(agent);
            ClearCornerResponse(agent);
            ClearCover(agent);
            agent.FireRescueTarget = best;
            agent.FireRescueDestination = stance;
            agent.FireRescueUntil = now + TimeSpan.FromSeconds(6);
        }
        var target = agent.FireRescueTarget.Value;
        if (now >= agent.FireRescueUntil || !BurningAlly(uid, agent, target, patter))
        {
            CancelFireResponse(uid, agent);
            return false;
        }
        if (!_interaction.InRangeUnobstructed(uid, target, 1.25f))
        {
            if (agent.HoldPosition || agent.FireRescueDestination is not { } destination ||
                !_transform.InRange(destination, Transform(target).Coordinates, 1.25f) ||
                !FireAidPassage(uid, agent, start, destination))
            {
                CancelFireResponse(uid, agent);
                return false;
            }
            // This short direct approach never borrows a generic detour or vault route.
            agent.State = CMUExpeditionAgentState.PlanMove;
            agent.CoverDestination = destination;
            agent.FireResponseDecision = "approaching-burning-ally";
            Move(uid, destination, validated: true);
            Decision(agent, "fire-response", agent.FireResponseDecision);
            return true;
        }
        _steering.Unregister(uid);
        ClearCover(agent);
        agent.State = CMUExpeditionAgentState.Watch;
        agent.FireResponseDecision = "patting-burning-ally";
        agent.RifleLoweredUntil = now + TimeSpan.FromSeconds(1);
        if (_guns.TryGetGun(uid, out var gun))
        {
            _wield.TryUnwield(gun.Owner, uid);
            StowOtherWeapons(uid, gun.Owner);
        }
        if (agent.FireRescueHandUntil == TimeSpan.Zero)
            agent.FireRescueHandUntil = now + TimeSpan.FromSeconds(0.6);
        var freeHand = false;
        foreach (var hand in _hands.EnumerateHands(uid))
        {
            if (_hands.TryGetHeldItem(uid, hand, out _))
                continue;
            _hands.TrySetActiveHand(uid, hand);
            if (_hands.GetActiveHand(uid) != hand || !_doorActions.CanInteract(uid, target))
                continue;
            freeHand = true;
            if (now < patter.LastPat + patter.Cooldown)
                break;
            var previous = patter.LastPat;
            // InteractHand assumes range checks by its caller. All native pat restrictions,
            // cooldown, stack changes, contact events and sound remain owned by RMC.
            _interaction.InteractHand(uid, target);
            if (patter.LastPat > previous)
                agent.FirePats++;
            break;
        }
        if (!freeHand && now >= agent.FireRescueHandUntil)
        {
            agent.FireResponseDecision = "fire-assist-hand-blocked";
            CancelFireResponse(uid, agent);
            return false;
        }
        Decision(agent, "fire-response", agent.FireResponseDecision);
        return true;
    }

    private bool BurningAlly(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid other, FirePatterComponent patter) =>
        other != uid && Exists(other) && IsFriendly(uid, other) && !_mobs.IsDead(other) &&
        HasComp<CanBeFirePattedComponent>(other) && !_supplyWhitelist.IsWhitelistPass(patter.Blacklist, other) &&
        TryComp<FlammableComponent>(other, out var fire) && fire.OnFire && fire.CanExtinguish &&
        Visible(uid, other, 4) && GroundSafe(Transform(other).Coordinates) &&
        ShelteredFromKnownThreats(uid, agent, Transform(other).Coordinates);

    private bool FireAllyClaimed(EntityUid uid, EntityUid target, TimeSpan now)
    {
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
            if (other != uid && buddy.FireRescueTarget == target && now < buddy.FireRescueUntil &&
                _mobs.IsAlive(other) && !HasComp<ActorComponent>(other))
                return true;
        return false;
    }

    private EntityCoordinates? FireAidStance(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid target)
    {
        var start = Transform(uid).Coordinates;
        var center = _transform.ToCoordinates(start.EntityId, _transform.GetMapCoordinates(target));
        var away = start.Position - center.Position;
        var angle = MathF.Atan2(away.Y, away.X);
        for (var i = 0; i < 8; i++)
        {
            var direction = angle + i * MathF.Tau / 8;
            var point = center.Offset(new Vector2(MathF.Cos(direction), MathF.Sin(direction)));
            if (FireAidPassage(uid, agent, start, point) && !Reserved(uid, point) &&
                _interaction.InRangeUnobstructed(_transform.ToMapCoordinates(point), target, 1.25f,
                    predicate: entity => entity == uid))
                return point;
        }
        return null;
    }

    private bool FireAidPassage(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates start, EntityCoordinates end) =>
        agent.Home is { } home && _transform.InRange(home, end, agent.LeashRange) &&
        TraversablePassage(uid, start, end, allowVault: false) && TrafficPassageClear(uid, start, end) &&
        KnownDangerPassage(uid, agent, start, end) && FireAidSheltered(uid, agent, start, end);

    private bool FireAidSheltered(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates start, EntityCoordinates end)
    {
        var from = _transform.ToMapCoordinates(start);
        var to = _transform.ToMapCoordinates(end);
        var budget = 16;
        return from.MapId == to.MapId && Content.Shared.CMU14.Expeditions.CMUCornerResponsePolicy.ShelteredPassage(
            from.Position, to.Position, sample => ShelteredFromKnownThreats(uid, agent,
                _transform.ToCoordinates(start.EntityId, new MapCoordinates(sample, from.MapId))), ref budget);
    }

    private void ClearFireEscape(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (agent.FireEscapeDestination is { } owned && agent.SpacingDestination == owned)
            StopSpacing(uid, agent);
        agent.FireEscapeDestination = null;
        agent.FireEscapeUntil = TimeSpan.Zero;
    }

    private bool RunFireEscape(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        var start = Transform(uid).Coordinates;
        if (FirePointSafe(start))
        {
            ClearFireEscape(uid, agent);
            agent.ResistedGroundFire = false;
            return false;
        }
        if (agent.FireEscapeDestination is { } escape)
        {
            if (agent.SpacingDestination == escape && now < agent.FireEscapeUntil && GroundSafe(escape) &&
                TraversablePassage(uid, start, escape, escapingHazard: true, allowVault: false, hazardEscapeRange: 6))
            {
                Move(uid, escape, validated: true);
                Decision(agent, "fire-response", "leaving-ground-fire");
                return true;
            }
            ClearFireEscape(uid, agent);
        }
        if (now < agent.NextFireEscape)
            return false;
        agent.NextFireEscape = now + TimeSpan.FromSeconds(1.5);
        EntityCoordinates? best = null;
        var score = float.MaxValue;
        // At most 192 point checks and 16 physical corridor checks. Stop at the first
        // clear stance on each bearing rather than choosing a far escape unnecessarily.
        for (var direction = 0; direction < 16; direction++)
        {
            var angle = direction * MathF.Tau / 16;
            var unit = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            for (var step = 1; step <= 12; step++)
            {
                var distance = step * 0.5f;
                var point = start.Offset(unit * distance);
                if (!GroundSafe(point))
                    continue;
                if (agent.Home is not { } home || !_transform.InRange(home, point, agent.LeashRange) ||
                    Reserved(uid, point) ||
                    MeleeClearance(agent, point) < Math.Min(3, MeleeClearance(agent, start)) ||
                    !TraversablePassage(uid, start, point, escapingHazard: true, allowVault: false, hazardEscapeRange: 6) ||
                    !KnownDangerPassage(uid, agent, start, point))
                    break;
                var candidateScore = distance + FriendlyCrowding(uid, point) + ExposureScore(uid, agent, point) * 0.25f;
                if (candidateScore < score)
                {
                    score = candidateScore;
                    best = point;
                }
                break;
            }
        }
        if (best is not { } destination)
        {
            agent.FireResponseDecision = "fire-no-clear-escape";
            return false;
        }
        CancelFireResponse(uid, agent);
        CancelAimedWeapon(agent);
        CancelFlare(uid, agent);
        BeginCombatSpacing(uid, agent, now);
        agent.FireEscapeDestination = destination;
        agent.FireEscapeUntil = now + TimeSpan.FromSeconds(6);
        agent.SpacingDestination = destination;
        agent.SpacingUntil = agent.SpacingMoveUntil = agent.FireEscapeUntil;
        agent.SpacingDecision = "escaping-ground-fire";
        agent.FireResponseDecision = "leaving-ground-fire";
        agent.FireEscapes++;
        Move(uid, destination, validated: true);
        Decision(agent, "fire-response", agent.FireResponseDecision);
        return true;
    }

    private bool FireMovementAllowed(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates destination)
    {
        var start = Transform(uid).Coordinates;
        var escaping = !FirePointSafe(start) &&
            (agent.FireEscapeDestination == destination || agent.SpacingDestination == destination);
        var safe = escaping
            ? TraversablePassage(uid, start, destination, escapingHazard: true, allowVault: false,
                hazardEscapeRange: agent.FireEscapeDestination == destination ? 6 : 1.5f)
            : FirePassageSafe(start, destination, AgentBodyRadius);
        if (safe)
            return true;
        _steering.Unregister(uid);
        ClearTraffic(agent);
        agent.LastMoveFailed = true;
        agent.MoveUntil = _timing.CurTime;
        agent.FireResponseDecision = "holding-clear-of-fire";
        return false;
    }
}

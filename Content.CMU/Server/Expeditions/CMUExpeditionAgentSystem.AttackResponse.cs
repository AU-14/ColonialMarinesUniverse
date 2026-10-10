using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared.CMU14.Threats.Mobs.Biomorph;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Projectiles;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private void InitializeAttackResponse() =>
        SubscribeLocalEvent<CMUExpeditionAgentComponent, DamageChangedEvent>(OnAgentAttacked);

    private void OnAgentAttacked(Entity<CMUExpeditionAgentComponent> ent, ref DamageChangedEvent args)
    {
        if (!_npcs.Enabled || HasComp<ActorComponent>(ent) || !_mobs.IsAlive(ent) ||
            !args.DamageIncreased || !args.InterruptsDoAfters)
            return;
        var now = _timing.CurTime;
        ent.Comp.PendingInterruptingDamage = true;
        ent.Comp.LastHit = now;
        RememberIncomingFire(ent, ent.Comp, Transform(ent).Coordinates, now, hit: true);
        ent.Comp.NextThink = now;
        ent.Comp.ImmediateFireUntil = now + TimeSpan.FromSeconds(0.8);
        ent.Comp.NextMovingBurst = now;
        // A hit accelerates self-defence, but its damage attribution is not a sensor
        // that reveals the current location of an attacker behind smoke or walls.
        if (args.Origin is { } source && Exists(source) && AcceptOrderedContact(ent, ent.Comp, source) &&
            Visible(ent, source, ent.Comp.DetectionRange))
        {
            if (args.Impact.Delivery == DamageImpactDelivery.Projectile && !IsMeleeThreat(source))
                ObserveIncomingLane(ent, ent.Comp, _transform.GetMapCoordinates(ent),
                    _transform.GetWorldPosition(ent) - _transform.GetWorldPosition(source), now);
            if (!ent.Comp.RecentShooters.ContainsKey(source) && ent.Comp.RecentShooters.Count >= 16)
                ent.Comp.RecentShooters.Remove(ent.Comp.RecentShooters.MinBy(pair => pair.Value).Key);
            ent.Comp.RecentShooters[source] = now + TimeSpan.FromSeconds(2);
            if (IsMeleeThreat(source))
                RememberMelee(ent.Comp, source, Transform(source).Coordinates, now);
        }
    }

    private static void ExpireMeleeMemory(CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        foreach (var (target, memory) in agent.MeleeMemory.ToArray())
            if (memory.Until <= now)
                agent.MeleeMemory.Remove(target);
    }

    private static void RememberMelee(CMUExpeditionAgentComponent agent, EntityUid target, EntityCoordinates point, TimeSpan now)
    {
        if (!agent.MeleeMemory.ContainsKey(target) && agent.MeleeMemory.Count >= 8)
            agent.MeleeMemory.Remove(agent.MeleeMemory.MinBy(pair => pair.Value.Until).Key);
        agent.MeleeMemory[target] = (point, now + TimeSpan.FromSeconds(3));
    }

    private void AddRememberedMelee(EntityUid uid, CMUExpeditionAgentComponent agent,
        List<(EntityUid Target, float Distance)> visible, TimeSpan now, ref float nearest)
    {
        foreach (var (target, memory) in agent.MeleeMemory)
        {
            if (memory.Until <= now || visible.Exists(contact => contact.Target == target))
                continue;
            // Frozen positions only: neither transform nor velocity is read for lost contacts.
            var point = _transform.ToMapCoordinates(memory.Position);
            var origin = _transform.GetMapCoordinates(uid);
            if (point.MapId != origin.MapId)
                continue;
            var distance = Vector2.Distance(point.Position, origin.Position);
            agent.MeleeThreats.Add((memory.Position, Vector2.Zero));
            if (distance >= nearest)
                continue;
            nearest = distance;
            agent.RushTarget = target;
            agent.RushPosition = memory.Position;
        }
    }

    private float FriendlyCrowding(EntityUid uid, EntityCoordinates point)
    {
        var crowding = 0f;
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent, TransformComponent>();
        while (query.MoveNext(out var other, out var buddy, out var transform))
        {
            if (other == uid || !_mobs.IsAlive(other) || !IsFriendly(uid, other))
                continue;
            if (_transform.InRange(point, transform.Coordinates, 2.5f))
                crowding++;
            if (buddy.SpacingDestination is { } destination && _transform.InRange(point, destination, 2.5f))
                crowding++;
        }
        return Math.Min(6, crowding);
    }

    private bool AvoidAlienAttack(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        // A validated fire escape already owns its short movement. The fire response
        // rechecks its ground corridor every think; do not replace it with a smaller dodge.
        if (agent.FireEscapeDestination != null)
            return false;
        if (now < agent.NextHazardScan || now < agent.NextHazardDodge)
            return false;
        agent.NextHazardScan = now + TimeSpan.FromSeconds(0.3);
        var start = Transform(uid).Coordinates;
        var origin = _transform.GetMapCoordinates(uid);
        var unsafeGround = !GroundSafe(start);
        Vector2? incoming = null;
        Vector2? projectilePosition = null;
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(origin.MapId, origin.Position, 6, nearby);
        var soonest = float.MaxValue;
        foreach (var item in nearby)
        {
            if ((!HasComp<XenoAcidProjectileComponent>(item) && !HasComp<XenoProjectileComponent>(item) &&
                    !HasComp<BiomorphProjectileComponent>(item)) ||
                !TryComp<ProjectileComponent>(item, out var projectile) || projectile.ProjectileSpent ||
                projectile.Shooter is { } shooter && IsFriendly(uid, shooter) ||
                !TryComp<PhysicsComponent>(item, out var physics) || physics.LinearVelocity.LengthSquared() < 1 ||
                !Visible(uid, item, 6))
                continue;
            var position = _transform.GetWorldPosition(item);
            var offset = origin.Position - position;
            var time = Vector2.Dot(offset, physics.LinearVelocity) / physics.LinearVelocity.LengthSquared();
            if (time < 0 || time > 0.6f || time >= soonest ||
                (offset - physics.LinearVelocity * time).LengthSquared() > 1.2f * 1.2f)
                continue;
            soonest = time;
            incoming = Vector2.Normalize(physics.LinearVelocity);
            projectilePosition = position;
        }
        if (!unsafeGround && incoming == null)
        {
            agent.HazardDecision = "clear";
            return false;
        }
        EntityCoordinates? best = null;
        var bestScore = float.MinValue;
        for (var angle = 0; angle < 8; angle++)
        {
            var offset = new Vector2(MathF.Cos(angle * MathF.Tau / 8), MathF.Sin(angle * MathF.Tau / 8)) * 1.75f;
            var candidate = _transform.ToCoordinates(start.EntityId, origin.Offset(offset));
            if (agent.Home is not { } home || !_transform.InRange(home, candidate, agent.LeashRange) ||
                Reserved(uid, candidate) || !GroundSafe(candidate) ||
                !TraversablePassage(uid, start, candidate, escapingHazard: unsafeGround) ||
                MeleeClearance(agent, candidate) < Math.Min(3, MeleeClearance(agent, start)))
                continue;
            var score = -FriendlyCrowding(uid, candidate) - ExposureScore(uid, agent, candidate) * 0.3f;
            if (incoming is { } direction && projectilePosition is { } projectileOrigin)
            {
                var delta = origin.Position + offset - projectileOrigin;
                var separation = (delta - direction * Vector2.Dot(delta, direction)).Length();
                if (separation < 1.2f)
                    continue;
                score += separation * 3;
            }
            if (score <= bestScore)
                continue;
            best = candidate;
            bestScore = score;
        }
        agent.NextHazardDodge = now + TimeSpan.FromSeconds(1.2);
        agent.ImmediateFireUntil = now + TimeSpan.FromSeconds(1);
        if (best is not { } escape)
        {
            agent.HazardDecision = "hazard-no-safe-exit";
            return false;
        }
        CancelMedical(uid, agent, "evading-acid");
        BeginCombatSpacing(uid, agent, now);
        agent.SpacingDestination = escape;
        agent.SpacingUntil = agent.SpacingMoveUntil = now + TimeSpan.FromSeconds(1.2);
        agent.HazardDecision = unsafeGround ? "leaving-hazard" : "sidestepping-spit";
        agent.HazardDodges++;
        ReadyRifle(uid, agent);
        Move(uid, escape, validated: true);
        return true;
    }
}

using System.Numerics;
using Content.Server.NPC.Components;
using Content.Shared.CombatMode;
using Content.Shared.NPC;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private SharedCombatModeSystem _combat = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private void OnShotAttempted(Entity<CMUExpeditionAgentComponent> ent, ref ShotAttemptedEvent args)
    {
        if (HasComp<ActorComponent>(ent))
            return;
        EnsureComp<CMUExpeditionWeaponComponent>(args.Used);
        if (!_npcs.Enabled || !_mobs.IsAlive(ent) || ent.Comp.State != CMUExpeditionAgentState.Engage ||
            _timing.CurTime >= ent.Comp.BurstEnd || ent.Comp.ShotsFired >= VolleySize(ent.Comp) ||
            !TryComp<GunComponent>(args.Used, out var gun) || !TryAimPoint(ent, ent.Comp, gun, out var point) ||
            !SafeShot(ent, ent.Comp, gun, point))
            args.Cancel();
    }

    private void OnTakeAmmo(Entity<CMUExpeditionWeaponComponent> ent, ref TakeAmmoEvent args)
    {
        if (args.User is not { } user || HasComp<ActorComponent>(user) ||
            !TryComp<CMUExpeditionAgentComponent>(user, out var agent))
            return;
        args.Shots = Math.Min(args.Shots, Math.Max(0, VolleySize(agent) - agent.ShotsFired));
    }

    private void OnGunShot(Entity<CMUExpeditionWeaponComponent> ent, ref GunShotEvent args)
    {
        if (HasComp<ActorComponent>(args.User) || !TryComp<CMUExpeditionAgentComponent>(args.User, out var agent))
            return;
        agent.ShotsFired += args.Ammo.Count;
        if (agent.ShotsFired >= VolleySize(agent))
            EndBurst(args.User, agent, _timing.CurTime);
    }

    private void UpdateFire(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.State == CMUExpeditionAgentState.HoldAngle && now >= agent.FireAt)
            Aim(agent, now, true);
        if (agent.State == CMUExpeditionAgentState.Recover && agent.CoverAnchor == null && now >= agent.FireAt &&
            CanLeaveCover(uid, agent))
            Aim(agent, now);
        if (agent.State == CMUExpeditionAgentState.Aim && now >= agent.FireAt)
        {
            agent.State = CMUExpeditionAgentState.Engage;
            agent.ShotsFired = 0;
            agent.BurstEnd = now + agent.BurstDuration;
        }
        if (agent.State != CMUExpeditionAgentState.Engage)
            return;
        if (now >= agent.BurstEnd)
        {
            EndBurst(uid, agent, now);
            return;
        }
        if (!_guns.TryGetGun(uid, out var gun) || !_guns.CanShoot(gun))
        {
            agent.LastFireCheck = "weapon-not-ready";
            return;
        }
        if (!TryAimPoint(uid, agent, gun, out var point))
        {
            agent.LastFireCheck = "no-visible-aim-point";
            if (agent.PeekPosition is { } unusable)
            {
                // A moving target can invalidate a formerly clear peek. Do not repeat that
                // exposure indefinitely just because the remembered position still has a clear ray.
                agent.FailedPosition = unusable;
                agent.AvoidPositionUntil = now + TimeSpan.FromSeconds(8);
                agent.NextReposition = now;
            }
            EndBurst(uid, agent, now, false);
            return;
        }
        if (!FiringLaneClear(uid, Transform(uid).Coordinates, point))
        {
            agent.LastFireCheck = "obstructed-firing-cone";
            if (TryAdjustPeek(uid, agent, point, now))
                return;
            if (agent.PeekPosition is { } failed)
            {
                agent.FailedPosition = failed;
                agent.AvoidPositionUntil = now + TimeSpan.FromSeconds(8);
            }
            agent.NextReposition = now;
            EndBurst(uid, agent, now, false);
            return;
        }
        if (!SafeShot(uid, agent, gun, point))
        {
            agent.LastFireCheck = "friendly-in-firing-cone";
            agent.BlockedShotSince ??= now;
            if (now - agent.BlockedShotSince > TimeSpan.FromSeconds(1) && TryAdjustPeek(uid, agent, point, now))
                return;
            // Do not dump a late volley when an ally clears the muzzle. Reacquire briefly first.
            if (agent.ShotsFired > 0 || agent.CoverAnchor != null)
                EndBurst(uid, agent, now, false);
            else
            {
                agent.State = CMUExpeditionAgentState.Aim;
                agent.FireAt = now + ThinkInterval;
            }
            return;
        }
        agent.BlockedShotSince = null;
        _steering.Unregister(uid);
        if (TryComp<CombatModeComponent>(uid, out var combat))
            _combat.SetInCombatMode(uid, true, combat);
        var direction = _transform.ToMapCoordinates(point).Position - _transform.GetWorldPosition(uid);
        _transform.SetWorldRotation(uid, direction.ToWorldAngle());
        agent.LastFireCheck = _guns.AttemptShoot(uid, gun, point, agent.Target) ? "trigger-accepted" : "native-trigger-rejected";
    }

    private void EndBurst(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now, bool allowPress = true)
    {
        if (allowPress && agent.CoverAnchor != null && agent.Initiative >= 0.75f && agent.Stress < 0.3f &&
            agent.FollowupBursts == 0 && agent.ShotsFired >= VolleySize(agent))
        {
            agent.FollowupBursts++;
            agent.State = CMUExpeditionAgentState.HoldAngle;
            agent.FireAt = now + TimeSpan.FromSeconds(0.35);
            return;
        }
        if (agent.CoverAnchor is { } shelter)
        {
            if (agent.ShotsFired > 0 && agent.LastDamage <= agent.PeekInitialDamage)
                RecordTactic(uid, agent, true, false);
            BeginMove(uid, agent, shelter, CMUExpeditionAgentState.Withdraw, now);
        }
        else
        {
            agent.State = CMUExpeditionAgentState.Recover;
            agent.FireAt = now + RecoveryDelay(agent);
        }
    }

    private bool TryAimPoint(EntityUid uid, CMUExpeditionAgentComponent agent, GunComponent gun, out EntityCoordinates point)
    {
        point = default;
        if (agent.Target is not { } target || !_mobs.IsAlive(target) ||
            !Visible(uid, target, agent.FireRange) || !TryComp<TransformComponent>(target, out var transform))
            return false;
        var velocity = TryComp<PhysicsComponent>(target, out var body) ? body.LinearVelocity : Vector2.Zero;
        var from = _transform.GetWorldPosition(uid);
        var position = _transform.GetWorldPosition(transform);
        // RMC bullets are usually much faster than the generic NPC controller's assumed 20 m/s.
        var flight = Math.Min(Vector2.Distance(from, position) / Math.Max(1, gun.ProjectileSpeedModified), 0.4f);
        var map = new MapCoordinates(position + velocity * flight, transform.MapID);
        point = _transform.ToCoordinates(Transform(uid).MapUid!.Value, map);
        return true;
    }

    private bool SafeShot(EntityUid uid, CMUExpeditionAgentComponent agent, GunComponent gun, EntityCoordinates point, EntityCoordinates? origin = null)
    {
        var start = origin ?? Transform(uid).Coordinates;
        var from = _transform.ToMapCoordinates(start);
        var to = _transform.ToMapCoordinates(point);
        var distance = Vector2.Distance(from.Position, to.Position);
        // Check the body/muzzle and both sides of the entire native scatter cone, not one grazing ray.
        var spread = (float) Math.Tan(Math.Min(gun.MaxAngleModified.Theta, Math.PI / 2) / 2);
        if (!FiringLaneClear(uid, start, point))
            return false;

        var direction = Vector2.Normalize(to.Position - from.Position);
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(uid, agent.FireRange + 3, nearby);
        foreach (var entity in nearby)
        {
            if (entity == uid || entity == agent.Target || !IsFriendly(uid, entity) ||
                _mobs.IsDead(entity) || !TryComp<TransformComponent>(entity, out var transform) || transform.MapID != from.MapId)
                continue;
            var relative = _transform.GetWorldPosition(transform) - from.Position;
            // Include a teammate about to cross the lane during this bullet's flight.
            var velocity = TryComp<PhysicsComponent>(entity, out var body) ? body.LinearVelocity : Vector2.Zero;
            for (var sample = 0; sample < 2; sample++)
            {
                var offset = relative + velocity * (sample * Math.Min(distance / Math.Max(1, gun.ProjectileSpeedModified), 0.4f));
                var along = Vector2.Dot(offset, direction);
                if (along < -0.3f || along > distance + 2)
                    continue;
                if ((offset - direction * along).Length() < 0.55f + Math.Max(0, along) * spread)
                    return false;
            }
        }
        return true;
    }
}

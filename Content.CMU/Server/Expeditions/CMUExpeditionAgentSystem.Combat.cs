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
        // Native sniper completion must re-check the actual shot destination and visible
        // target. Flash guesses and radio contacts never authorize an entity-locked shot.
        if (ent.Comp.AimedWeapon == args.Used &&
            (ent.Comp.AimedTarget is not { } aimedTarget || ent.Comp.Target != aimedTarget ||
                !Visible(ent, aimedTarget, ent.Comp.FireRange) || !AcceptOrderedContact(ent, ent.Comp, aimedTarget) ||
                !TryComp<GunComponent>(args.Used, out var aimedGun) || aimedGun.ShootCoordinates is not { } destination ||
                !SafeShot(ent, ent.Comp, (args.Used, aimedGun), destination)))
        {
            args.Cancel();
            return;
        }
        var stationary = ent.Comp.State == CMUExpeditionAgentState.Engage && ent.Comp.Action == null &&
            ent.Comp.PendingWeapon == null && _timing.CurTime < ent.Comp.BurstEnd && ent.Comp.ShotsFired < VolleySize(ent.Comp);
        if (!_npcs.Enabled || !_mobs.IsAlive(ent) || ent.Comp.FlareItem != null || (!stationary && !MovingShotAllowed(ent, ent.Comp)) ||
            !TryComp<GunComponent>(args.Used, out var gun) || !TryAimPoint(ent, ent.Comp, (args.Used, gun), out var point) ||
            !SafeShot(ent, ent.Comp, (args.Used, gun), point))
            args.Cancel();
    }

    private void OnTakeAmmo(Entity<CMUExpeditionWeaponComponent> ent, ref TakeAmmoEvent args)
    {
        if (args.User is not { } user || HasComp<ActorComponent>(user) ||
            !TryComp<CMUExpeditionAgentComponent>(user, out var agent))
            return;
        args.Shots = Math.Min(args.Shots, Math.Max(0,
            VolleySize(agent) - (agent.MovingFire && CanFireWhileMoving(user, agent) ? agent.MovingShotsFired : agent.ShotsFired)));
    }

    private void OnGunShot(Entity<CMUExpeditionWeaponComponent> ent, ref GunShotEvent args)
    {
        if (HasComp<ActorComponent>(args.User) || !TryComp<CMUExpeditionAgentComponent>(args.User, out var agent))
            return;
        agent.LastShotAt = _timing.CurTime;
        if (agent.FiringAtFlash)
            agent.FlashShots += args.Ammo.Count;
        agent.LastFiredWeapon = ent.Owner;
        if (agent.AimedWeapon == ent.Owner)
        {
            agent.AimedWeapon = null;
            agent.AimedTarget = null;
        }
        if (TryComp<CMUExpeditionWeaponRoleComponent>(ent, out var role) && role.Rocket)
        {
            agent.RocketsFired++;
            agent.NextRocket = _timing.CurTime + TimeSpan.FromSeconds(20);
            agent.NextWeaponChoice = _timing.CurTime;
            var squad = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
            while (squad.MoveNext(out var other, out var buddy))
                if (SameSquad(args.User, agent, other, buddy))
                    buddy.NextRocket = agent.NextRocket;
        }
        if (agent.MovingFire && CanFireWhileMoving(args.User, agent))
        {
            if (agent.MovingShotsFired == 0)
                agent.MovingBurstEnd = _timing.CurTime + agent.BurstDuration;
            agent.MovingShotsFired += args.Ammo.Count;
            agent.TotalMovingShots += args.Ammo.Count;
            if (agent.MovingShotsFired >= VolleySize(agent))
                agent.NextMovingBurst = _timing.CurTime + RecoveryDelay(agent);
            return;
        }
        // Readiness delays must not consume the volley before the rifle actually fires.
        if (agent.ShotsFired == 0)
            agent.BurstEnd = _timing.CurTime + agent.BurstDuration;
        agent.ShotsFired += args.Ammo.Count;
        if (agent.ShotsFired >= VolleySize(agent))
            EndBurst(args.User, agent, _timing.CurTime);
    }

    private void UpdateFire(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.FlareItem != null || agent.AimedWeapon != null)
            return;
        if (CanFireWhileMoving(uid, agent))
        {
            UpdateMovingFire(uid, agent, now);
            return;
        }
        agent.MovingFire = false;
        if (agent.Action != null || agent.PendingWeapon != null)
            return;
        var resuming = agent.State == CMUExpeditionAgentState.HoldAngle ||
            agent.State == CMUExpeditionAgentState.Recover && agent.CoverAnchor == null;
        var canResume = resuming &&
            (now >= agent.FireAt || UrgentFire(agent, now)) &&
            (agent.Target is { } target && CombatTargetAlive(target) && Visible(uid, target, agent.FireRange) || TryFlashAim(uid, agent, out _));
        if (canResume && UrgentFire(agent, now))
            agent.FireAt = now;
        if (agent.State == CMUExpeditionAgentState.HoldAngle && now >= agent.FireAt && canResume)
            Aim(agent, now, true);
        if (agent.State == CMUExpeditionAgentState.Recover && agent.CoverAnchor == null && now >= agent.FireAt &&
            canResume && CanLeaveCover(uid, agent))
            Aim(agent, now);
        if (agent.State == CMUExpeditionAgentState.Aim && UrgentFire(agent, now))
            agent.FireAt = now;
        if (agent.State == CMUExpeditionAgentState.Aim && now >= agent.FireAt)
        {
            agent.State = CMUExpeditionAgentState.Engage;
            // A lane-clearing sidestep continues the same limited volley.
            if (!agent.ResumeVolley)
                agent.ShotsFired = 0;
            agent.ResumeVolley = false;
            agent.BurstEnd = now + agent.BurstDuration;
        }
        if (agent.State != CMUExpeditionAgentState.Engage)
            return;
        if (now >= agent.BurstEnd)
        {
            // Do not spend an unfired volley on a native wield/readiness delay and then
            // impose another aim/pause cycle. Sight and lane failures still abort below.
            if (agent.ShotsFired == 0 && now < agent.FireAt + TimeSpan.FromSeconds(3))
                agent.BurstEnd = now + TimeSpan.FromSeconds(0.2);
            else
            {
                EndBurst(uid, agent, now);
                return;
            }
        }
        if (!_guns.TryGetGun(uid, out var gun) || !_guns.CanShoot(gun))
        {
            agent.LastFireCheck = "weapon-not-ready";
            return;
        }
        if (!TryAimPoint(uid, agent, gun, out var point))
        {
            agent.LastFireCheck = "no-visible-aim-point";
            agent.LostAimSince ??= now;
            // Hold the stance through a brief loss behind a tree; never fire without sight.
            if (now - agent.LostAimSince < TimeSpan.FromSeconds(0.35))
                return;
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
        agent.LostAimSince = null;
        if (!FiringLaneClear(uid, Transform(uid).Coordinates, point))
        {
            agent.LastFireCheck = "obstructed-firing-cone";
            if (now < agent.SpacingUntil)
                return;
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
            if (now < agent.SpacingUntil)
                return;
            agent.BlockedShotSince ??= now;
            // A crossing teammate pauses this volley without repeatedly resetting aim. If the
            // lane stays occupied, make one deliberate sidestep or yield the attack slot.
            if (now - agent.BlockedShotSince >= TimeSpan.FromSeconds(0.35))
            {
                if (TryAdjustPeek(uid, agent, point, now))
                    return;
                agent.NextReposition = now;
                EndBurst(uid, agent, now, false);
            }
            return;
        }
        agent.BlockedShotSince = null;
        if (agent.SpacingDestination == null)
            _steering.Unregister(uid);
        if (TryComp<CombatModeComponent>(uid, out var combat))
            _combat.SetInCombatMode(uid, true, combat);
        var direction = _transform.ToMapCoordinates(point).Position - _transform.GetWorldPosition(uid);
        _transform.SetWorldRotation(uid, direction.ToWorldAngle());
        if (StartAimedWeapon(uid, agent, gun))
            return;
        agent.LastFireCheck = _guns.AttemptShoot(uid, gun, point, agent.FiringAtFlash ? null : agent.Target)
            ? "trigger-accepted" : "native-trigger-rejected";
    }

    private void EndBurst(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now, bool allowPress = true)
    {
        agent.LostAimSince = null;
        agent.BlockedShotSince = null;
        if (now < agent.SpacingUntil)
        {
            agent.State = CMUExpeditionAgentState.Recover;
            agent.FireAt = now + TimeSpan.FromSeconds(0.2);
            return;
        }
        ValidateCover(uid, agent, false, now);
        // Keep a productive stance across volleys. A covering commitment also survives ordinary
        // pressure; a lost lane, empty gun, rush, grenade or emergency injury invalidates it.
        if (allowPress && (HasCoverCommitment(uid, agent, now) || KeepFightingPosition(uid, agent, now)))
        {
            agent.State = CMUExpeditionAgentState.HoldAngle;
            agent.FireAt = now + RecoveryDelay(agent);
            return;
        }
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

    private bool TryAimPoint(EntityUid uid, CMUExpeditionAgentComponent agent, Entity<GunComponent> gun, out EntityCoordinates point)
    {
        point = default;
        agent.FiringAtFlash = false;
        if (agent.Target is not { } target || !CombatTargetAlive(target) ||
            !AcceptOrderedContact(uid, agent, target) || !Visible(uid, target, agent.FireRange) ||
            !TryComp(target, out TransformComponent? transform))
        {
            // A guessed flash position is unsuitable for blast weapons or homing entity locks.
            if (TryComp<CMUExpeditionWeaponRoleComponent>(gun.Owner, out var role) && role.Rocket ||
                !TryFlashAim(uid, agent, out point))
            {
                agent.VisionDecision = "no-visible-target-or-flash";
                return false;
            }
            agent.FiringAtFlash = true;
            agent.VisionDecision = "firing-at-flash-position";
            return true;
        }
        agent.VisionDecision = "visible-target";
        var velocity = TryComp<PhysicsComponent>(target, out var body) ? body.LinearVelocity : Vector2.Zero;
        var from = _transform.GetWorldPosition(uid);
        var position = _transform.GetWorldPosition(transform);
        // RMC bullets are usually much faster than the generic NPC controller's assumed 20 m/s.
        var flight = Math.Min(Vector2.Distance(from, position) / Math.Max(1, gun.Comp.ProjectileSpeedModified), 0.4f);
        var map = new MapCoordinates(position + velocity * flight, transform.MapID);
        point = _transform.ToCoordinates(Transform(uid).MapUid!.Value, map);
        return true;
    }

    private bool SafeShot(EntityUid uid, CMUExpeditionAgentComponent agent, Entity<GunComponent> gun, EntityCoordinates point,
        EntityCoordinates? origin = null, HashSet<EntityUid>? nearby = null)
    {
        var start = origin ?? Transform(uid).Coordinates;
        var from = _transform.ToMapCoordinates(start);
        var to = _transform.ToMapCoordinates(point);
        var distance = Vector2.Distance(from.Position, to.Position);
        if (from.MapId != to.MapId || distance < 0.1f || !SafeWeaponEffect(uid, agent, gun, start, point))
            return false;
        // Match the next native shot's recoil, including recovery since the previous shot.
        // Using maximum sustained-fire scatter made whole squads wait on empty lanes.
        var elapsed = (_timing.CurTime - gun.Comp.LastFire).TotalSeconds;
        var scatter = Math.Clamp(gun.Comp.CurrentAngle.Theta + gun.Comp.AngleIncreaseModified.Theta -
            gun.Comp.AngleDecayModified.Theta * elapsed, gun.Comp.MinAngleModified.Theta, gun.Comp.MaxAngleModified.Theta);
        var spread = (float) Math.Tan(Math.Min(scatter, Math.PI / 2) / 2);
        if (!FiringLaneClear(uid, start, point))
            return false;

        var direction = Vector2.Normalize(to.Position - from.Position);
        if (nearby == null)
        {
            nearby = new HashSet<EntityUid>();
            _lookup.GetEntitiesInRange(uid, agent.FireRange + 3, nearby);
        }
        foreach (var entity in nearby)
        {
            if (entity == uid || !IsFriendly(uid, entity) ||
                _mobs.IsDead(entity) || !TryComp(entity, out TransformComponent? transform) || transform.MapID != from.MapId ||
                IffPassesFriendly(uid, gun.Owner, entity))
                continue;
            var relative = _transform.GetWorldPosition(transform) - from.Position;
            // Include a teammate about to cross the lane during this bullet's flight.
            var velocity = TryComp<PhysicsComponent>(entity, out var body) ? body.LinearVelocity : Vector2.Zero;
            for (var sample = 0; sample < 2; sample++)
            {
                var offset = relative + velocity * (sample * Math.Min(distance / Math.Max(1, gun.Comp.ProjectileSpeedModified), 0.4f));
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

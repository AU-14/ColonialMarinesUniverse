using System.Numerics;
using Content.Server.NPC.Components;
using Content.Shared.Doors.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private static void ClearDoorFocus(CMUExpeditionAgentComponent agent)
    {
        agent.DoorClearanceDestination = null;
        agent.DoorClearanceUntil = TimeSpan.Zero;
    }

    private bool TryOpenContactDoor(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        // Use remembered coordinates even if a tracked enemy has since moved out of sight.
        // Investigation's standoff range must not prevent interacting with a nearby door.
        var destination = agent.OrderedDestination ?? (now < agent.ForgetAt ? agent.LastSeen : null);
        return destination is { } contact && TryOpenContactDoor(uid, agent, contact, now);
    }

    private bool TryOpenContactDoor(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates contact, TimeSpan now)
    {
        if (agent.TrafficNudgeDestination != null || agent.HoldPosition || agent.Entrench || agent.CornerHolding || agent.Action != null ||
            agent.Treatment != null || agent.PendingWeapon != null || agent.AimedWeapon != null ||
            agent.FlareItem != null || agent.WorkItem != null || agent.PreparingWork || agent.ScavengeTarget != null ||
            agent.RushTarget != null || agent.SpacingDestination != null || agent.VaultTarget != null ||
            agent.State is CMUExpeditionAgentState.Withdraw or CMUExpeditionAgentState.Retreat or CMUExpeditionAgentState.OutOfAmmo)
            return false;
        if (agent.WaitingForDoor != null)
            WaitingAtDoor(uid, agent);
        if (agent.WaitingForDoor == null && now < agent.NextContactDoor)
            return false;
        agent.NextContactDoor = now + TimeSpan.FromSeconds(0.35);
        var start = Transform(uid).Coordinates;
        var from = _transform.ToMapCoordinates(start);
        var to = _transform.ToMapCoordinates(contact);
        var delta = to.Position - from.Position;
        if (from.MapId != to.MapId || delta.LengthSquared() < 0.01f)
            return false;
        var forward = Vector2.Normalize(delta);
        EntityUid? first = null;
        var nearest = float.MaxValue;
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(from.MapId, from.Position, 1.6f, nearby);
        foreach (var entity in nearby)
        {
            if (!TryComp<DoorComponent>(entity, out _) ||
                !TryComp<PhysicsComponent>(entity, out var body) || !body.CanCollide ||
                !CanNavigateDoor(uid, entity) || !_interaction.InRangeUnobstructed(uid, entity, range: 1.5f))
                continue;
            var location = _transform.GetWorldPosition(entity);
            var projection = Vector2.Dot(location - from.Position, forward);
            var distance = Vector2.DistanceSquared(location, from.Position);
            if (projection < -0.1f || projection > delta.Length() ||
                SegmentDistance(location, from.Position, to.Position) > 0.6f || distance >= nearest)
                continue;
            var beyond = _transform.ToCoordinates(start.EntityId, new MapCoordinates(location + forward * 0.7f, from.MapId));
            if (!RoutePassage(uid, start, beyond, allowVault: false) || !KnownDangerPassage(uid, agent, start, beyond))
                continue;
            first = entity;
            nearest = distance;
        }
        return first is { } door && RequestDoorOpening(uid, agent, door, now);
    }

    private bool TryResolveDoorFiringLane(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates threat, TimeSpan now)
    {
        if (agent.TrafficNudgeDestination != null)
            return false;
        var start = Transform(uid).Coordinates;
        // Friendly-fire rejection alone must never provoke door opening or an extra move.
        if (FiringLaneClear(uid, start, threat))
            return false;
        if (TryOpenContactDoor(uid, agent, threat, now))
            return true;
        if (agent.DoorClearanceDestination is { } active && now < agent.DoorClearanceUntil &&
            agent.State == CMUExpeditionAgentState.Peeking && agent.CoverDestination == active)
            return true;
        ClearDoorFocus(agent);
        if (now < agent.NextDoorClearance || agent.HoldPosition || agent.Entrench || agent.CornerHolding ||
            agent.Action != null || agent.Treatment != null || agent.PendingWeapon != null || agent.AimedWeapon != null ||
            agent.FlareItem != null || agent.WorkItem != null || agent.PreparingWork || agent.ScavengeTarget != null ||
            agent.RushTarget != null || agent.SpacingDestination != null || agent.VaultTarget != null ||
            agent.State is CMUExpeditionAgentState.Withdraw or CMUExpeditionAgentState.Retreat or CMUExpeditionAgentState.OutOfAmmo ||
            CommittedMovement(agent) && TryComp<NPCSteeringComponent>(uid, out var steering) && steering.Status == SteeringStatus.Moving ||
            !_guns.TryGetGun(uid, out var gun))
            return false;
        agent.NextDoorClearance = now + TimeSpan.FromSeconds(2);
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(uid, 1.15f, nearby);
        var atThreshold = false;
        foreach (var entity in nearby)
        {
            if (TryComp<DoorComponent>(entity, out var door) && door.State is DoorState.Open or DoorState.Opening &&
                TryComp<PhysicsComponent>(entity, out var body) && !body.CanCollide)
            {
                atThreshold = true;
                break;
            }
        }
        if (!atThreshold)
            return false;
        var delta = _transform.ToCoordinates(start.EntityId, _transform.ToMapCoordinates(threat)).Position - start.Position;
        if (delta.LengthSquared() < 0.01f)
            return false;
        var forward = Vector2.Normalize(delta);
        var exposure = ExposureScore(uid, agent, start);
        // Sideways peeks hit the door frame in narrow passages. Check short axial steps,
        // preferring retreat from the threshold before crossing into the observed lane.
        foreach (var distance in new[] { -0.65f, 0.65f, -1f, 1f, -1.35f, 1.35f })
        {
            var candidate = start.Offset(forward * distance);
            if (agent.Home is not { } home || !_transform.InRange(home, candidate, agent.LeashRange) ||
                agent.CoverAnchor is { } anchor && !_transform.InRange(anchor, candidate, 3.6f) ||
                agent.FailedPosition is { } failed && now < agent.AvoidPositionUntil && _transform.InRange(failed, candidate, 0.6f) ||
                Reserved(uid, candidate) || !TraversablePassage(uid, start, candidate, allowVault: false) ||
                !TrafficPassageClear(uid, start, candidate) || !KnownDangerPassage(uid, agent, start, candidate) ||
                CrossesCoveringFire(uid, agent, start, candidate) ||
                ExposureScore(uid, agent, candidate) > exposure + 0.25f ||
                ExposureScore(uid, agent, start.Offset(forward * distance / 2)) > exposure + 0.25f ||
                !_transform.InRange(candidate, threat, WeaponFireRange(uid, agent)) || !SafeShot(uid, agent, gun, threat, candidate))
                continue;
            agent.PeekPosition = candidate;
            agent.FightingPosition = null;
            agent.ContactDestination = null;
            agent.DoorClearanceDestination = candidate;
            agent.DoorClearanceUntil = now + TimeSpan.FromSeconds(2);
            agent.DoorDecision = "clearing-door-firing-lane";
            BeginMove(uid, agent, candidate, CMUExpeditionAgentState.Peeking, now);
            return true;
        }
        return false;
    }
}

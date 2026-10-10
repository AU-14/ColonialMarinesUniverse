using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Inventory;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Light.Components;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private static readonly ProtoId<TagPrototype> FlareTag = "Flare";
    private static readonly ProtoId<TagPrototype> FlarePackTag = "CMFlarePack";
    private readonly List<(EntityUid Flare, EntityCoordinates Point, TimeSpan Until)> _flareLandings = new();

    private bool IsFlare(EntityUid uid) => HasComp<ExpendableLightComponent>(uid) &&
        (_tags.HasTag(uid, FlareTag) || MetaData(uid).EntityPrototype is { } prototype &&
            ProtoMan.EnumerateParents<EntityPrototype>(prototype.ID).Any(parent => parent.ID == "CMFlare"));

    private bool FreshFlare(EntityUid uid) => IsFlare(uid) && !HasComp<FlareSignalComponent>(uid) &&
        Comp<ExpendableLightComponent>(uid).CurrentState == ExpendableLightState.BrandNew;

    private IEnumerable<EntityUid> PackedFlares(EntityUid pack)
    {
        if (!_tags.HasTag(pack, FlarePackTag) || !HasComp<CMItemSlotsComponent>(pack) ||
            !TryComp<ItemSlotsComponent>(pack, out var slots))
            yield break;
        foreach (var slot in slots.Slots.Values)
            if (slot.Item is { } flare && FreshFlare(flare))
                yield return flare;
    }

    private int FlareSupplyCount(EntityUid item) => FreshFlare(item) ? 1 : PackedFlares(item).Count();

    private IEnumerable<EntityUid> CarriedFlares(EntityUid uid)
    {
        foreach (var item in SupplyItems(uid))
        {
            if (FreshFlare(item))
                yield return item;
            else
                foreach (var flare in PackedFlares(item))
                    yield return flare;
        }
    }

    private bool TakeCarriedFlare(EntityUid uid, EntityUid flare)
    {
        if (_hands.GetEmptyHandCount(uid) == 0 || !FreshFlare(flare))
            return false;
        if (SupplyItems(uid).Contains(flare))
            return _hands.TryPickupAnyHand(uid, flare);
        if (!_containers.TryGetContainingContainer((flare, null, null), out var container) ||
            !SupplyItems(uid).Contains(container.Owner) || !PackedFlares(container.Owner).Contains(flare) ||
            !TryComp<ItemSlotsComponent>(container.Owner, out var slots))
            return false;
        var slot = slots.Slots.Values.FirstOrDefault(candidate => candidate.Item == flare);
        if (slot == null || !_itemSlots.TryEject(container.Owner, slot, uid, out var ejected))
            return false;
        if (_hands.TryPickupAnyHand(uid, ejected.Value))
            return true;
        // A failed pickup must not consume the pack's flare or strand it unnecessarily.
        if (!_itemSlots.TryInsert(container.Owner, slot, ejected.Value, uid))
            StoreSupply(uid, ejected.Value);
        return false;
    }

    private void CancelFlare(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (agent.FlareItem is { } item && Exists(item) && !HasComp<ActorComponent>(uid) &&
            _hands.IsHolding(uid, item, out _))
        {
            if ((!FreshFlare(item) || !StoreSupply(uid, item)) && !_hands.TryDrop(uid, item))
            {
                // Native drop may be blocked during an interruption. Retain ownership
                // so the offhand is retried once interaction becomes possible again.
                agent.FlareCleanupPending = true;
                agent.FlareDestination = null;
                agent.VisionDecision = "flare-cleanup-blocked";
                return;
            }
        }
        agent.FlareItem = null;
        agent.FlareCleanupPending = false;
        agent.FlareDestination = null;
        agent.RifleLoweredUntil = TimeSpan.Zero;
    }

    private bool RunFlare(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.FlareItem is { } item)
        {
            if (agent.FlareCleanupPending)
            {
                CancelFlare(uid, agent);
                return false;
            }
            if (!Exists(item) || now >= agent.FlareUntil || agent.LastHit > agent.FlareStarted ||
                agent.Action != null || agent.Treatment != null || agent.RushTarget != null ||
                agent.PendingWeapon != null || agent.ScavengeTarget != null || agent.WorkItem != null || agent.PreparingWork ||
                agent.FlareDestination is not { } point ||
                !_transform.InRange(Transform(uid).Coordinates, point, 7) || !GroundSafe(point) ||
                FlareAreaCovered(uid, point) || !ClearLane(uid, Transform(uid).Coordinates, point, 0.3f))
            {
                CancelFlare(uid, agent);
                agent.NextFlare = now + TimeSpan.FromSeconds(5);
                return false;
            }
            if (now < agent.FlareReadyAt)
                return true;
            if (!_hands.IsHolding(uid, item, out _))
            {
                if (!TakeCarriedFlare(uid, item))
                {
                    CancelFlare(uid, agent);
                    return false;
                }
                if (_guns.TryGetGun(uid, out var heldGun))
                    ActivateWeapon(uid, heldGun.Owner);
                agent.FlareReadyAt = now + TimeSpan.FromSeconds(0.6);
                return true;
            }
            // Ignite through the normal held-item action, then throw the actual finite item.
            _interaction.UseInHandInteraction(uid, item);
            if (!Comp<ExpendableLightComponent>(item).Activated || !_hands.TryDrop(uid, item))
            {
                CancelFlare(uid, agent);
                return false;
            }
            var thrown = _throwing.TryThrow(item, point, user: uid, compensateFriction: true);
            if (thrown)
            {
                agent.FlaresUsed++;
                _flareLandings.Add((item, point, now + TimeSpan.FromSeconds(3)));
            }
            agent.FlareItem = null;
            CancelFlare(uid, agent);
            _nextLightSnapshot = TimeSpan.Zero;
            DelaySquadFlares(uid, agent, now + TimeSpan.FromSeconds(thrown ? 30 : 5));
            return true;
        }
        if (now < agent.NextFlare || agent.UtilityCleanupItem != null || agent.Action != null || agent.PendingWeapon != null || agent.Treatment != null ||
            agent.WorkItem != null || agent.PreparingWork || agent.ScavengeTarget != null || agent.RushTarget != null ||
            agent.LastDamage >= agent.RetreatDamage || now - agent.LastHit < TimeSpan.FromSeconds(0.75) ||
            GrenadeDanger(Transform(uid).Coordinates) || HasCoverCommitment(uid, agent, now))
            return false;
        agent.NextFlare = now + TimeSpan.FromSeconds(2);
        var interest = agent.LastSeen is { } contact && now < agent.ForgetAt ? contact :
            agent.OrderRoute.TryPeek(out var waypoint) ? waypoint : agent.OrderedDestination ?? agent.GuardAnchor;
        if (interest is not { } area || Illumination(area) >= agent.MinimumSightLight)
            return false;
        var flare = CarriedFlares(uid).FirstOrDefault();
        if (flare == default)
            return false;
        var position = Transform(uid).Coordinates;
        var destination = _transform.ToCoordinates(position.EntityId, _transform.ToMapCoordinates(area));
        var delta = destination.Position - position.Position;
        if (delta.LengthSquared() < 1)
        {
            var ahead = _transform.GetMapCoordinates(uid).Offset(_transform.GetWorldRotation(uid).RotateVec(new Vector2(0, -3)));
            delta = _transform.ToCoordinates(position.EntityId, ahead).Position - position.Position;
        }
        var throwAt = position.Offset(Vector2.Normalize(delta) * Math.Min(6, delta.Length()));
        // A distant dark objective may lie beyond an already lit landing spot. Check
        // the actual throw area, including friendly throws still in preparation/flight.
        if (!GroundSafe(throwAt) || FlareAreaCovered(uid, throwAt) ||
            !ClearLane(uid, position, throwAt, 0.3f) || SmokeOccludes(position, throwAt))
            return false;
        CancelWork(uid, agent);
        PrepareUtilityHand(uid);
        agent.FlareItem = flare;
        agent.FlareCleanupPending = false;
        agent.FlareDestination = throwAt;
        agent.FlareStarted = now;
        agent.FlareReadyAt = now + TimeSpan.FromSeconds(0.2);
        agent.FlareUntil = now + TimeSpan.FromSeconds(2);
        agent.RifleLoweredUntil = agent.FlareUntil;
        agent.VisionDecision = "preparing-flare";
        DelaySquadFlares(uid, agent, agent.FlareUntil + TimeSpan.FromSeconds(1));
        return true;
    }

    private void PrepareUtilityHand(EntityUid uid)
    {
        // Only release a virtual grip when it occupies the hand needed for the item.
        // Picking up into the offhand preserves the active gun and its native restrictions.
        if (_hands.GetEmptyHandCount(uid) == 0 && _guns.TryGetGun(uid, out var gun))
            _wield.TryUnwield(gun.Owner, uid);
    }

    private bool FlareAreaCovered(EntityUid uid, EntityCoordinates point)
    {
        var now = _timing.CurTime;
        var map = _transform.ToMapCoordinates(point);
        RefreshLightSnapshot();
        if (_visionLights.TryGetValue(map.MapId, out var lights))
        foreach (var light in lights)
        {
            if (!IsFlare(light) || !TryComp<ExpendableLightComponent>(light, out var flare) ||
                !flare.Activated || flare.CurrentState == ExpendableLightState.Fading && flare.StateExpiryTime < 8 ||
                _containers.IsEntityOrParentInContainer(light) ||
                !TryComp(light, out TransformComponent? transform) || transform.MapID != map.MapId)
                continue;
            var radius = flare.CurrentState == ExpendableLightState.Fading
                ? Math.Min(5, 1 + 6 * Math.Clamp(flare.StateExpiryTime / Math.Max(0.01f, (float) flare.FadeOutDuration.TotalSeconds), 0, 1))
                : 5;
            if (_transform.InRange(transform.Coordinates, point, radius) &&
                SightLine(light, point, radius))
                return true;
        }
        _flareLandings.RemoveAll(landing => landing.Until <= now || !Exists(landing.Flare));
        foreach (var landing in _flareLandings)
            if (_transform.InRange(landing.Point, point, 5) && ClearLane(uid, landing.Point, point, 0.1f))
                return true;
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent, TransformComponent>();
        while (query.MoveNext(out var other, out var buddy, out var transform))
            if (other != uid && transform.MapID == map.MapId && _mobs.IsAlive(other) && !HasComp<ActorComponent>(other) &&
                IsFriendly(uid, other) && buddy.FlareItem != null && now < buddy.FlareUntil &&
                buddy.FlareDestination is { } reserved && _transform.InRange(reserved, point, 5) &&
                ClearLane(uid, reserved, point, 0.1f))
                return true;
        return false;
    }

    private void DelaySquadFlares(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan until)
    {
        agent.NextFlare = until;
        var squad = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (squad.MoveNext(out var other, out var buddy))
            if (SameSquad(uid, agent, other, buddy) && _transform.InRange(Transform(uid).Coordinates, Transform(other).Coordinates, 16))
                buddy.NextFlare = until;
    }
}

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
            if (!FreshFlare(item) || !Supplies(uid, out var bag) ||
                !StoreOwnedItem(uid, item, bag))
                _hands.TryDrop(uid, item);
        }
        agent.FlareItem = null;
        agent.FlareDestination = null;
        agent.RifleLoweredUntil = TimeSpan.Zero;
    }

    private bool RunFlare(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.FlareItem is { } item)
        {
            if (!Exists(item) || now >= agent.FlareUntil || agent.LastHit > agent.FlareStarted ||
                agent.Action != null || agent.Treatment != null || agent.RushTarget != null ||
                agent.FlareStartPosition is not { } start || !_transform.InRange(start, Transform(uid).Coordinates, 0.4f) ||
                agent.FlareDestination is not { } point || !ClearLane(uid, Transform(uid).Coordinates, point, 0.3f))
            {
                CancelFlare(uid, agent);
                agent.NextFlare = now + TimeSpan.FromSeconds(5);
                return false;
            }
            _steering.Unregister(uid);
            if (now < agent.FlareReadyAt)
                return true;
            if (!_hands.IsHolding(uid, item, out _))
            {
                if (!TakeCarriedFlare(uid, item))
                {
                    CancelFlare(uid, agent);
                    return false;
                }
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
            _throwing.TryThrow(item, point, user: uid, compensateFriction: true);
            agent.FlaresUsed++;
            agent.FlareItem = null;
            CancelFlare(uid, agent);
            _nextLightSnapshot = TimeSpan.Zero;
            DelaySquadFlares(uid, agent, now + TimeSpan.FromSeconds(30));
            return true;
        }
        if (now < agent.NextFlare || agent.Action != null || agent.PendingWeapon != null || agent.Treatment != null ||
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
        if (!GroundSafe(throwAt) || !ClearLane(uid, position, throwAt, 0.3f) || SmokeOccludes(position, throwAt))
            return false;
        CancelWork(uid, agent);
        if (_guns.TryGetGun(uid, out var gun))
            _wield.TryUnwield(gun.Owner, uid);
        agent.FlareItem = flare;
        agent.FlareDestination = throwAt;
        agent.FlareStartPosition = position;
        agent.FlareStarted = now;
        agent.FlareReadyAt = now + TimeSpan.FromSeconds(0.2);
        agent.FlareUntil = now + TimeSpan.FromSeconds(2);
        agent.RifleLoweredUntil = agent.FlareUntil;
        agent.VisionDecision = "preparing-flare";
        _steering.Unregister(uid);
        DelaySquadFlares(uid, agent, agent.FlareUntil + TimeSpan.FromSeconds(1));
        return true;
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

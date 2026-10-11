using System.Numerics;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Storage;
using Content.Shared.Throwing;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private TriggerSystem _triggers = default!;
    [Dependency] private PullingSystem _pulling = default!;
    private readonly List<(EntityCoordinates Point, TimeSpan Until, float Radius)> _grenadeHazards = new();

    private void InitializeEquipment() => SubscribeLocalEvent<CMUExpeditionAgentComponent, CMUExpeditionUtilityDoAfterEvent>(OnUtilityFinished);

    private bool Supplies(EntityUid uid, out StorageComponent storage)
    {
        storage = default!;
        return _inventory.TryGetSlotEntity(uid, "back", out var bag) && TryComp(bag, out storage!);
    }

    private EntityUid? SpareMagazine(EntityUid uid)
    {
        if (!_guns.TryGetGun(uid, out var gun) || !_itemSlots.TryGetSlot(gun.Owner, "gun_magazine", out var slot) || !Supplies(uid, out var supplies))
            return null;
        foreach (var item in supplies.Container.ContainedEntities)
        {
            var ammo = new GetAmmoCountEvent();
            RaiseLocalEvent(item, ref ammo);
            if (ammo.Count > 0 && _itemSlots.CanInsert(gun, slot, item, uid, swap: true))
                return item;
        }
        return null;
    }

    private EntityUid? Grenade(EntityUid uid, bool smoke)
    {
        if (!Supplies(uid, out var supplies))
            return null;
        foreach (var item in supplies.Container.ContainedEntities)
        {
            if (TryComp<CMUExpeditionGrenadeComponent>(item, out var grenade) && grenade.Smoke == smoke &&
                !HasComp<ActiveTimerTriggerComponent>(item))
                return item;
        }
        return null;
    }

    private bool GrenadeDanger(EntityCoordinates position)
    {
        foreach (var hazard in _grenadeHazards)
        {
            if (hazard.Until > _timing.CurTime && _transform.InRange(position, hazard.Point, hazard.Radius))
                return true;
        }
        return false;
    }

    private bool SafeGrenade(EntityUid uid, EntityCoordinates destination, EntityUid item)
    {
        if (!TryComp<CMUExpeditionGrenadeComponent>(item, out var grenade) ||
            !_transform.InRange(Transform(uid).Coordinates, destination, 10) ||
            !ClearLane(uid, Transform(uid).Coordinates, destination, 0.4f))
            return false;
        if (grenade.Smoke)
            return true;
        var nearby = new HashSet<EntityUid>();
        var landing = _transform.ToMapCoordinates(destination);
        _lookup.GetEntitiesInRange(landing.MapId, landing.Position, grenade.SafeRadius + 5, nearby);
        foreach (var entity in nearby)
        {
            if (!IsFriendly(uid, entity) || _mobs.IsDead(entity))
                continue;
            var coordinates = Transform(entity).Coordinates;
            if (_transform.InRange(coordinates, destination, grenade.SafeRadius))
                return false;
            // Consider the current movement heading over the fuse, but cap prediction to avoid absurd velocities.
            if (TryComp<PhysicsComponent>(entity, out var body))
            {
                var offset = body.LinearVelocity * 4;
                if (offset.LengthSquared() > 25)
                    offset = Vector2.Normalize(offset) * 5;
                var future = _transform.GetMapCoordinates(entity).Offset(offset);
                if (Vector2.Distance(future.Position, _transform.ToMapCoordinates(destination).Position) < grenade.SafeRadius)
                    return false;
            }
            if (TryComp<CMUExpeditionAgentComponent>(entity, out var ally) &&
                ally.CoverDestination is { } planned && _transform.InRange(planned, destination, grenade.SafeRadius))
                return false;
        }
        return true;
    }

    private bool StartUtility(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid item, TimeSpan delay)
    {
        if (!_guns.TryGetGun(uid, out var gun))
            return false;
        _wield.TryUnwield(gun.Owner, uid);
        if (!_hands.TryPickupAnyHand(uid, item))
            return false;
        _steering.Unregister(uid);
        agent.ActionItem = item;
        var args = new DoAfterArgs(EntityManager, uid, delay, new CMUExpeditionUtilityDoAfterEvent(), uid, used: item)
        {
            NeedHand = true, BreakOnMove = true, BreakOnDamage = true, DamageThreshold = 0.1f,
            ExtraCheck = () => _mobs.IsAlive(uid) && !HasComp<ActorComponent>(uid) && _npcs.Enabled &&
                (agent.Action != CMUTacticalAction.Reload || TreatmentSafe(uid, agent)),
        };
        return _doAfter.TryStartDoAfter(args, out agent.ActionDoAfter);
    }

    private void OnUtilityFinished(Entity<CMUExpeditionAgentComponent> ent, ref CMUExpeditionUtilityDoAfterEvent args)
    {
        var agent = ent.Comp;
        if (agent.ActionDoAfter != args.DoAfter.Id)
            return;
        agent.ActionDoAfter = null;
        args.Repeat = false;
        if (args.Cancelled || !_npcs.Enabled || HasComp<ActorComponent>(ent) || !_mobs.IsAlive(ent) ||
            agent.ActionItem is not { } item || !Exists(item))
        {
            CancelPlan(ent, agent, true);
            return;
        }
        var success = false;
        if (agent.Action == CMUTacticalAction.Reload && TreatmentSafe(ent, agent) && _guns.TryGetGun(ent, out var gun) &&
            _itemSlots.TryGetSlot(gun.Owner, "gun_magazine", out var slot) &&
            _itemSlots.CanInsert(gun, slot, item, ent, swap: true))
        {
            EntityUid? old = null;
            if (!slot.HasItem || _itemSlots.TryEject(gun.Owner, "gun_magazine", ent, out old))
            {
                success = _itemSlots.TryInsert(gun.Owner, "gun_magazine", item, ent);
                if (!success && old is { } previous)
                    _itemSlots.TryInsert(gun.Owner, "gun_magazine", previous, ent);
            }
            if (success)
                agent.Reloads++;
        }
        else if (agent.Action == CMUTacticalAction.ThrowGrenade && agent.GrenadeTarget is { } target && SafeGrenade(ent, target, item))
        {
            if (_hands.TryDrop(ent.Owner, item) && _throwing.TryThrow(item, target, user: ent, compensateFriction: true))
            {
                // Prime only after a real throw succeeds; interrupted preparation never leaves a live grenade in hand.
                success = _triggers.ActivateTimerTrigger(item, ent);
                if (success)
                {
                    agent.GrenadesThrown++;
                    agent.LastGrenade = _timing.CurTime;
                    agent.GrenadeReservationUntil = TimeSpan.Zero;
                    agent.NextGrenade = _timing.CurTime + TimeSpan.FromSeconds(35);
                    var grenade = Comp<CMUExpeditionGrenadeComponent>(item);
                    if (!grenade.Smoke)
                        _grenadeHazards.Add((target, _timing.CurTime + Comp<TimerTriggerComponent>(item).Delay + TimeSpan.FromSeconds(2), grenade.SafeRadius));
                }
            }
        }
        if (success)
        {
            agent.ActionItem = null;
            agent.ActionComplete = true;
        }
        else
            CancelPlan(ent, agent, true);
    }

    private void ReleaseCasualty(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (agent.Casualty is { } casualty && TryComp<PullableComponent>(casualty, out var pulled) && pulled.Puller == uid)
            _pulling.TryStopPull(casualty, pulled, uid);
    }
}

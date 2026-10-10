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
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Whitelist;
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
    [Dependency] private EntityWhitelistSystem _supplyWhitelist = default!;
    private readonly List<(EntityCoordinates Point, TimeSpan Until, float Radius)> _grenadeHazards = new();

    private void InitializeEquipment() => SubscribeLocalEvent<CMUExpeditionAgentComponent, CMUExpeditionUtilityDoAfterEvent>(OnUtilityFinished);

    private bool Supplies(EntityUid uid, out StorageComponent storage)
    {
        storage = default!;
        return _inventory.TryGetSlotEntity(uid, "back", out var bag) && TryComp(bag, out storage!);
    }

    private EntityUid? SpareAmmunition(EntityUid uid, EntityUid? weapon = null)
    {
        if (weapon == null && _guns.TryGetGun(uid, out var active))
            weapon = active;
        if (weapon is not { } gun || !HasComp<GunComponent>(gun))
            return null;
        foreach (var item in SupplyItems(uid))
        {
            if (CompatibleAmmunition(uid, gun, item))
                return item;
        }
        return null;
    }

    private bool CompatibleAmmunition(EntityUid uid, EntityUid gun, EntityUid item)
    {
        var ammo = new GetAmmoCountEvent();
        RaiseLocalEvent(item, ref ammo);
        return _itemSlots.TryGetSlot(gun, "gun_magazine", out var slot) && ammo.Count > 0 &&
            _itemSlots.CanInsert(gun, slot, item, uid, swap: true) ||
            TryComp<BallisticAmmoProviderComponent>(gun, out var tube) &&
            TryComp<CartridgeAmmoComponent>(item, out var cartridge) && !cartridge.Spent &&
            !_supplyWhitelist.IsWhitelistFailOrNull(tube.Whitelist, item);
    }

    private EntityUid? Grenade(EntityUid uid, bool smoke)
    {
        foreach (var item in SupplyItems(uid))
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
        return grenade.Smoke || SafeBlast(uid, destination, grenade.SafeRadius,
            TryComp<TimerTriggerComponent>(item, out var timer) ? (float) timer.Delay.TotalSeconds : 4);
    }

    private bool SafeBlast(EntityUid uid, EntityCoordinates destination, float radius, float prediction)
    {
        var nearby = new HashSet<EntityUid>();
        var landing = _transform.ToMapCoordinates(destination);
        _lookup.GetEntitiesInRange(landing.MapId, landing.Position, radius + 5, nearby);
        foreach (var entity in nearby)
        {
            if (!IsFriendly(uid, entity) || _mobs.IsDead(entity))
                continue;
            var coordinates = Transform(entity).Coordinates;
            if (_transform.InRange(coordinates, destination, radius + (VehicleBody(entity) ? 4 : 0)))
                return false;
            // Consider the current movement heading over the fuse, but cap prediction to avoid absurd velocities.
            if (TryComp<PhysicsComponent>(entity, out var body))
            {
                var offset = body.LinearVelocity * prediction;
                if (offset.LengthSquared() > 25)
                    offset = Vector2.Normalize(offset) * 5;
                var current = _transform.GetMapCoordinates(entity).Position;
                // Check the swept segment, not only its far endpoint: a buddy may cross
                // the blast circle and leave it again before the fuse expires.
                var along = offset.LengthSquared() > 0.001f
                    ? Math.Clamp(Vector2.Dot(landing.Position - current, offset) / offset.LengthSquared(), 0, 1) : 0;
                if (Vector2.Distance(current + offset * along, landing.Position) < radius)
                    return false;
            }
            if (TryComp<CMUExpeditionAgentComponent>(entity, out var ally) &&
                (ally.CoverDestination is { } planned && _transform.InRange(planned, destination, radius) ||
                 ally.SpacingDestination is { } escape && _transform.InRange(escape, destination, radius)))
                return false;
        }
        return true;
    }

    private bool StartUtility(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid item, TimeSpan delay)
    {
        if (agent.Action == CMUTacticalAction.Reload && !_guns.TryGetGun(uid, out _))
            return false;
        if (_guns.TryGetGun(uid, out var gun))
            _wield.TryUnwield(gun.Owner, uid);
        if (!_hands.TryPickupAnyHand(uid, item))
            return false;
        _steering.Unregister(uid);
        agent.ActionItem = item;
        var args = new DoAfterArgs(EntityManager, uid, delay, new CMUExpeditionUtilityDoAfterEvent(), uid, used: item)
        {
            NeedHand = true, BreakOnMove = true, BreakOnDamage = true, DamageThreshold = 0.1f,
            ExtraCheck = () => _mobs.IsAlive(uid) && !HasComp<ActorComponent>(uid) && _npcs.Enabled &&
                (agent.Action != CMUTacticalAction.Reload || ReloadSafe(uid, agent)),
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
        if (agent.Action == CMUTacticalAction.Reload && ReloadSafe(ent, agent) && _guns.TryGetGun(ent, out var gun) &&
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
        else if (agent.Action == CMUTacticalAction.Reload && ReloadSafe(ent, agent) &&
            _guns.TryGetGun(ent, out var tubeGun) && TryComp<BallisticAmmoProviderComponent>(tubeGun, out var tube) &&
            _guns.CanInsertBallistic((tubeGun.Owner, tube), item))
        {
            // Consume a real shell from the carried handful using the native insertion path.
            success = _guns.TryAmmoInsert((tubeGun.Owner, tube), item, ent, tubeGun.Owner, 0);
            if (Exists(item) && _hands.IsHolding(ent.Owner, item, out _))
                StoreSupply(ent, item);
            if (success)
            {
                agent.Reloads++;
                // Under contact, one live shell is enough to return fire. Top off only
                // without a visible target; otherwise a shotgun can spend the whole fight loading.
                var returnFire = agent.Target is { } contact && Visible(ent, contact, agent.FireRange)
                    && WeaponAmmo(tubeGun) > 0;
                if (!returnFire && SpareAmmunition(ent, tubeGun) is { } next && _timing.CurTime < agent.ActionUntil)
                {
                    agent.ActionItem = next;
                    agent.ActionStarted = _timing.CurTime;
                    return;
                }
            }
        }
        else if (agent.Action == CMUTacticalAction.ThrowGrenade && agent.GrenadeTarget is { } target &&
            (agent.SmokeGrenade || HasGrenadeContact(ent, agent, target)) && SafeGrenade(ent, target, item))
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
                    if (grenade.Smoke)
                    {
                        agent.SmokesThrown++;
                        _smokeScreens.Add((target, _timing.CurTime + TimeSpan.FromSeconds(35)));
                    }
                    else
                        _grenadeHazards.Add((target, _timing.CurTime + Comp<TimerTriggerComponent>(item).Delay + TimeSpan.FromSeconds(2), grenade.SafeRadius));
                }
            }
        }
        if (success)
        {
            agent.ActionItem = null;
            agent.ActionComplete = true;
            if (agent.Action == CMUTacticalAction.Reload)
            {
                // Release the utility state and ready the weapon immediately. A known
                // visible target does not need a second aim/recovery cycle after reloading.
                var now = _timing.CurTime;
                ContinueAction(ent, agent, agent.LastDamage, now);
                agent.RifleLoweredUntil = now;
                agent.NextThink = now;
                agent.NextReposition = now + agent.BurstDuration;
                agent.NextPlan = now + agent.BurstDuration;
                if (ReadyRifle(ent, agent) && _guns.TryGetGun(ent, out var ready)
                    && TryAimPoint(ent, agent, ready, out var aim) && SafeShot(ent, agent, ready, aim))
                {
                    Aim(agent, now, true);
                    agent.FireAt = now;
                }
            }
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

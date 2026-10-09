using System.Linq;
using Content.Shared._RMC14.Storage;
using Content.Shared.Lock;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Trigger.Components;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private SharedStorageSystem _scavengeStorage = default!;
    [Dependency] private RMCStorageSystem _scavengeRmcStorage = default!;

    private bool ScavengeSource(EntityUid uid, EntityUid item, out EntityUid source)
    {
        source = item;
        if (!Exists(item) || Transform(item).Anchored)
            return false;
        if (_containers.TryGetContainingContainer((item, null, null), out var container))
        {
            source = container.Owner;
            if (TryComp<StorageComponent>(source, out var storage))
            {
                if (TryComp<LockComponent>(source, out var padlock) && padlock.Locked ||
                    !_scavengeStorage.CanInteract(uid, (source, storage)) || !_scavengeRmcStorage.CanEject(source, uid, out _))
                    return false;
                if (_containers.TryGetContainingContainer((source, null, null), out var worn))
                {
                    // Loot one accessible bag/belt layer on a dead body. Living allies,
                    // patients in crit and arbitrary nested/locked containers are excluded.
                    source = worn.Owner;
                    if (!_mobs.IsDead(source))
                        return false;
                }
            }
            else if (!_mobs.IsDead(source) ||
                !_hands.IsHolding(source, item, out _) &&
                !_inventory.CanUnequip(uid, source, container.ID, out _))
                return false;
        }
        return source != uid && !_containers.IsEntityOrParentInContainer(source) &&
            Visible(uid, source, 4) && TrySquadCoordinates(Transform(source).Coordinates, out var point) && GroundSafe(point);
    }

    private bool KnownLootGrenade(EntityUid item, out bool smoke)
    {
        smoke = false;
        if (!HasComp<TimerTriggerComponent>(item) || HasComp<ActiveTimerTriggerComponent>(item))
            return false;
        if (TryComp<CMUExpeditionGrenadeComponent>(item, out var grenade))
        {
            smoke = grenade.Smoke;
            return true;
        }
        // Only native grenades with a known executor/blast envelope are adopted.
        // Incendiaries, custom chemicals and unknown ordnance need their own safety rules.
        var prototype = MetaData(item).EntityPrototype?.ID;
        smoke = prototype == "CMGrenadeSmoke";
        return smoke || prototype == "CMGrenadeHighExplosive";
    }

    private bool UsefulLoot(EntityUid uid, EntityUid item, bool armed)
    {
        if (HasComp<GunComponent>(item))
            return !armed && WeaponAmmo(item) > 0 &&
                !(TryComp<CMUExpeditionWeaponRoleComponent>(item, out var role) && role.Rocket);
        if (!Supplies(uid, out var supplies) || !_inventory.TryGetSlotEntity(uid, "back", out var bag) ||
            !_scavengeStorage.CanInsert(bag.Value, item, out _) ||
            !_scavengeRmcStorage.CanInsert((bag.Value, supplies), item, uid, out _))
            return false;
        if (KnownLootGrenade(item, out var smoke))
            return !supplies.Container.ContainedEntities.Any(other => KnownLootGrenade(other, out var otherSmoke) && otherSmoke == smoke);
        foreach (var gun in CarriedWeapons(uid))
        {
            if (!CompatibleAmmunition(uid, gun, item))
                continue;
            var reserve = supplies.Container.ContainedEntities.Count(other => CompatibleAmmunition(uid, gun, other));
            if (reserve < (HasComp<CartridgeAmmoComponent>(item) ? 12 : 2))
                return true;
        }
        return false;
    }

    private IEnumerable<EntityUid> NearbyLoot(IEnumerable<EntityUid> nearby)
    {
        var items = new HashSet<EntityUid>();
        foreach (var source in nearby.Where(item => HasComp<StorageComponent>(item) || _mobs.IsDead(item) ||
                     HasComp<GunComponent>(item) || HasComp<BallisticAmmoProviderComponent>(item) ||
                     HasComp<CartridgeAmmoComponent>(item) || KnownLootGrenade(item, out _)).Take(24))
        {
            items.Add(source);
            if (TryComp<StorageComponent>(source, out var looseStorage))
                items.UnionWith(looseStorage.Container.ContainedEntities.Take(32));
            if (!_mobs.IsDead(source))
                continue;
            foreach (var hand in _hands.EnumerateHands(source))
                if (_hands.TryGetHeldItem(source, hand, out var held))
                    items.Add(held.Value);
            var slots = _inventory.GetSlotEnumerator(source);
            while (slots.MoveNext(out var slot))
            {
                if (slot.ContainedEntity is not { } worn)
                    continue;
                items.Add(worn);
                if (TryComp<StorageComponent>(worn, out var storage))
                    items.UnionWith(storage.Container.ContainedEntities.Take(32));
            }
        }
        return items.Take(128);
    }

    private bool StoreScavengedSupply(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid item)
    {
        if (!Supplies(uid, out var supplies) || !_inventory.TryGetSlotEntity(uid, "back", out var bag) ||
            !_scavengeStorage.CanInsert(bag.Value, item, out _) ||
            !_scavengeRmcStorage.CanInsert((bag.Value, supplies), item, uid, out _) ||
            !_scavengeStorage.Insert(bag.Value, item, out _, user: uid))
            return false;
        if (KnownLootGrenade(item, out var smoke))
        {
            var grenade = EnsureComp<CMUExpeditionGrenadeComponent>(item);
            grenade.Smoke = smoke;
            grenade.SafeRadius = smoke ? 1 : 6;
        }
        agent.SuppliesScavenged++;
        agent.WeaponDecision = "scavenged-supplies";
        agent.NextPlan = _timing.CurTime;
        return true;
    }
}

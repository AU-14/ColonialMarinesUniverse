using System.Linq;
using Content.Shared._RMC14.Storage;
using Content.Shared.Lock;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Trigger.Components;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private SharedStorageSystem _scavengeStorage = default!;
    [Dependency] private RMCStorageSystem _scavengeRmcStorage = default!;
    [Dependency] private SharedEntityStorageSystem _supplyCrates = default!;

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
            else if (TryComp<EntityStorageComponent>(source, out var crate))
            {
                if (TryComp<LockComponent>(source, out var crateLock) && crateLock.Locked ||
                    !crate.Open && !_supplyCrates.CanOpen(uid, source, silent: true, crate))
                    return false;
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
        {
            if (TryComp<CMUExpeditionWeaponRoleComponent>(item, out var role) && role.Rocket)
                return Comp<CMUExpeditionAgentComponent>(uid).AntiVehicle && WeaponAmmo(item) > 0 && !HasReadyRocket(uid) &&
                    (_inventory.CanEquip(uid, item, "suitStorage", out _) || CanStoreSupply(uid, item));
            return !armed && WeaponAmmo(item) > 0 &&
                !(TryComp<CMUExpeditionWeaponRoleComponent>(item, out var firearm) && firearm.Rocket);
        }
        if (!armed && Comp<CMUExpeditionAgentComponent>(uid) is { LastSeen: not null } agent &&
            _timing.CurTime < agent.ForgetAt && !CarriedWeapons(uid).Any(gun => CompatibleAmmunition(uid, gun, item)))
            return false; // An empty combatant needs ammunition, not optional stock for the backpack.
        return CanStoreSupply(uid, item) && (WantsSupply(uid, item) || WantsMedicalTool(uid, item) || RunnerNeedsItem(uid, item));
    }

    private bool LootApproach(EntityUid uid, EntityUid source, out Robust.Shared.Map.EntityCoordinates point)
    {
        point = Transform(uid).Coordinates;
        if (_interaction.InRangeUnobstructed(uid, source))
            return true;
        if (!TrySquadCoordinates(Transform(source).Coordinates, out var center))
            return false;
        foreach (var candidate in NearbySquadPositions(center, 1))
        {
            if (!ValidOrderPoint(uid, candidate) || !TraversablePassage(uid, point, candidate) ||
                !_interaction.InRangeUnobstructed(_transform.ToMapCoordinates(candidate), _transform.GetMapCoordinates(source), 2,
                    predicate: entity => entity == uid || entity == source))
                continue;
            point = candidate;
            return true;
        }
        return false;
    }

    private IEnumerable<EntityUid> NearbyLoot(IEnumerable<EntityUid> nearby)
    {
        var items = new HashSet<EntityUid>();
        foreach (var source in nearby.Where(item => HasComp<StorageComponent>(item) || HasComp<EntityStorageComponent>(item) || _mobs.IsDead(item) ||
                     HasComp<GunComponent>(item) || HasComp<BallisticAmmoProviderComponent>(item) ||
                     HasComp<CartridgeAmmoComponent>(item) || KnownLootGrenade(item, out _) || FlareSupplyCount(item) > 0 || StockDressing(item) || FreshMedicalTool(item)).Take(24))
        {
            items.Add(source);
            if (TryComp<StorageComponent>(source, out var looseStorage))
                items.UnionWith(looseStorage.Container.ContainedEntities.Take(32));
            if (TryComp<EntityStorageComponent>(source, out var crate))
                items.UnionWith(crate.Contents.ContainedEntities.Take(32));
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
        if (!StoreSupply(uid, item))
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

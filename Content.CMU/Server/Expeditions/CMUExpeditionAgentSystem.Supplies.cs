using System.Linq;
using Content.Shared.Medical.Healing;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private IEnumerable<StorageComponent> SupplyStores(EntityUid uid)
    {
        // Belt first: ready ammunition should not compete with a rifle for backpack space.
        foreach (var slot in new[] { "belt", "pocket2", "pocket1", "outerClothing", "back" })
            if (_inventory.TryGetSlotEntity(uid, slot, out var bag) && TryComp<StorageComponent>(bag, out var storage) &&
                _scavengeStorage.CanInteract(uid, (bag.Value, storage)) && _scavengeRmcStorage.CanEject(bag.Value, uid, out _))
                yield return storage;
    }

    private IEnumerable<EntityUid> SupplyItems(EntityUid uid) => SupplyStores(uid).SelectMany(bag => bag.Container.ContainedEntities);

    private bool CanStoreSupply(EntityUid uid, EntityUid item) => SupplyStores(uid).Any(bag =>
        _scavengeStorage.CanInsert(bag.Container.Owner, item, out _) &&
        _scavengeRmcStorage.CanInsert((bag.Container.Owner, bag), item, uid, out _));

    private bool StoreSupply(EntityUid uid, EntityUid item)
    {
        foreach (var bag in SupplyStores(uid))
            if (StoreOwnedItem(uid, item, bag))
                return true;
        return false;
    }

    private bool StockDressing(EntityUid item) => HasComp<HealingComponent>(item) &&
        (!TryComp<StackComponent>(item, out var stack) || stack.Count > 0);

    private EntityUid? PersonalDressing(EntityUid uid)
    {
        if (_inventory.TryGetSlotEntity(uid, "pocket1", out var pocket) && StockDressing(pocket.Value))
            return pocket;
        return SupplyItems(uid).Where(StockDressing).Cast<EntityUid?>().FirstOrDefault();
    }

    private int SupplyQuantity(EntityUid item)
    {
        if (HasComp<CartridgeAmmoComponent>(item))
            return TryComp<StackComponent>(item, out var stack) ? stack.Count : 1;
        return 1;
    }

    private int AmmoReserve(EntityUid uid, EntityUid gun) => SupplyItems(uid)
        .Where(item => CompatibleAmmunition(uid, gun, item)).Sum(SupplyQuantity);

    private bool WantsSupply(EntityUid uid, EntityUid item, bool lowOnly = false)
    {
        if (FreshMedicalTool(item))
            return WantsMedicalTool(uid, item);
        var items = SupplyItems(uid).ToArray();
        if (KnownLootGrenade(item, out var smoke))
            return items.Count(other => KnownLootGrenade(other, out var otherSmoke) && smoke == otherSmoke) < (lowOnly ? 1 : 2);
        if (FlareSupplyCount(item) > 0)
        {
            var reserve = items.Sum(FlareSupplyCount);
            return reserve < (lowOnly ? 2 : 8) && (FreshFlare(item) || reserve <= 2);
        }
        if (StockDressing(item))
            return lowOnly ? PersonalDressing(uid) == null : items.Count(StockDressing) < (HasComp<CMUExpeditionMedicComponent>(uid) ? 3 : 1);
        foreach (var gun in CarriedWeapons(uid))
            if (CompatibleAmmunition(uid, gun, item) && AmmoReserve(uid, gun) <
                (HasComp<CartridgeAmmoComponent>(item) ? lowOnly ? 4 : 24 : lowOnly ? 1 : 6))
                return true;
        return false;
    }

    private bool CanDonate(EntityUid uid, EntityUid item)
    {
        var items = SupplyItems(uid).ToArray();
        if (!items.Contains(item))
            return false;
        if (FreshMedicalTool(item))
            return !HasComp<CMUExpeditionMedicComponent>(uid) || items.Count(other => FreshMedicalTool(other) &&
                HasComp<Content.Shared.Medical.DefibrillatorComponent>(other) == HasComp<Content.Shared.Medical.DefibrillatorComponent>(item)) > 1;
        if (KnownLootGrenade(item, out var smoke))
            return items.Count(other => KnownLootGrenade(other, out var otherSmoke) && smoke == otherSmoke) > 1;
        if (FlareSupplyCount(item) is > 0 and var flareCount)
            return items.Sum(FlareSupplyCount) - flareCount >= 2;
        if (StockDressing(item))
            return PersonalDressing(uid) is { } dressing && dressing != item || items.Count(StockDressing) > 1;
        var compatible = false;
        foreach (var gun in CarriedWeapons(uid))
        {
            if (!CompatibleAmmunition(uid, gun, item))
                continue;
            compatible = true;
            if (AmmoReserve(uid, gun) - SupplyQuantity(item) < (HasComp<CartridgeAmmoComponent>(item) ? 6 : 1))
                return false;
        }
        return compatible || HasComp<CartridgeAmmoComponent>(item) || HasComp<BallisticAmmoProviderComponent>(item);
    }
}

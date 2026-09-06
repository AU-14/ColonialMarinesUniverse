using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Inventory;
using Content.Shared.Zombies;

namespace Content.Server._CMU14.Weapons.Ranged;

// Entities that turn hostile (zombies, pathogen walkers, thralls, mimics) keep their old IFF
// Clearing UserIFFComponent alone is not enough, an equipped dogtag's ItemIFF relays through
// the id slot and friendly bullets still phase through them
public sealed class CMUHostileIFFSystem : EntitySystem
{
    [Dependency] private GunIFFSystem _gunIFF = default!;
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<EntityZombifiedEvent>(OnZombified);
    }

    private void OnZombified(ref EntityZombifiedEvent ev)
    {
        StripIFF(ev.Target);
    }

    /// <summary>
    ///     Removes every IFF source from an entity that turned hostile. Callers re-add a user
    ///     faction afterwards if the new form should still be friendly to someone.
    /// </summary>
    public void StripIFF(EntityUid ent)
    {
        _gunIFF.ClearUserFactions(ent);

        if (_inventory.TryGetSlotEntity(ent, "id", out var id))
            RemComp<ItemIFFComponent>(id.Value);
    }
}

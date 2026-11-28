using Content.Server.Antag;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Round.Antags;

/// <summary>
///     Antag starting gear goes into fixed slots, and when the job already filled that slot the item was deleted.
///     A WY agent with full pockets lost their data link that way (BUG-1524). This puts anything that never
///     arrived into the antag's own bags, or a free hand. Never the floor, where it would out them.
/// </summary>
public sealed class CMUAntagGearRecoverySystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AfterAntagEntitySelectedEvent>(OnAntagSelected);
    }

    private void OnAntagSelected(ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Def.StartingGear is not { } gearId || !_prototypes.TryIndex(gearId, out var gear))
            return;

        var antag = args.EntityUid;
        foreach (var item in gear.Equipment.Values)
        {
            if (Carries(antag, item))
                continue;

            var spawned = Spawn(item, Transform(antag).Coordinates);
            if (!TryStow(antag, spawned) && !_hands.TryPickupAnyHand(antag, spawned, checkActionBlocker: false))
                Del(spawned);
        }
    }

    private bool Carries(EntityUid antag, EntProtoId item)
    {
        foreach (var held in _inventory.GetHandOrInventoryEntities(antag))
        {
            if (IsOrContains(held, item))
                return true;
        }

        return false;
    }

    private bool IsOrContains(EntityUid uid, EntProtoId item)
    {
        if (MetaData(uid).EntityPrototype?.ID == item.Id)
            return true;

        if (!TryComp(uid, out ContainerManagerComponent? manager))
            return false;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (IsOrContains(contained, item))
                    return true;
            }
        }

        return false;
    }

    private bool TryStow(EntityUid antag, EntityUid item)
    {
        if (!_inventory.TryGetContainerSlotEnumerator(antag, out var slots))
            return false;

        while (slots.NextItem(out var worn))
        {
            if (TryComp(worn, out StorageComponent? storage) &&
                _storage.Insert(worn, item, out _, user: antag, storageComp: storage, playSound: false))
            {
                return true;
            }
        }

        return false;
    }
}

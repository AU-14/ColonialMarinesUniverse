using Content.Shared.Atmos.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Storage;

namespace Content.Shared.Body.Systems;

/// <summary>
/// Internals also look for gas tanks inside the storage a mob is wearing: satchels, pouches, webbing and so on.
/// </summary>
public abstract partial class SharedInternalsSystem
{
    /// <summary>How many storages deep to look, e.g. a pouch inside a satchel.</summary>
    private const int CMUMaxStorageDepth = 2;

    private IEnumerable<EntityUid> CMUGetWornStorageContents(Entity<HandsComponent?, InventoryComponent?> user)
    {
        var enumerator = _inventory.GetSlotEnumerator((user.Owner, user.Comp2));
        while (enumerator.NextItem(out var worn))
        {
            foreach (var item in CMUGetStorageContents(worn, 1))
            {
                yield return item;
            }
        }
    }

    private IEnumerable<EntityUid> CMUGetStorageContents(EntityUid storage, int depth)
    {
        if (!TryComp(storage, out StorageComponent? comp) || comp.Container == null)
            yield break;

        foreach (var item in comp.Container.ContainedEntities)
        {
            yield return item;

            if (depth < CMUMaxStorageDepth)
            {
                foreach (var nested in CMUGetStorageContents(item, depth + 1))
                {
                    yield return nested;
                }
            }
        }
    }

    /// <summary>Best gas tank found inside worn storage; non-jetpack tanks win, like the slot search.</summary>
    private Entity<GasTankComponent>? CMUFindStoredGasTank(Entity<HandsComponent?, InventoryComponent?> user, out Entity<GasTankComponent>? jetpack)
    {
        jetpack = null;
        foreach (var item in CMUGetWornStorageContents(user))
        {
            if (!TryComp(item, out GasTankComponent? tank) || !_gasTank.CanConnectToInternals((item, tank)))
                continue;

            if (HasComp<Content.Shared.Movement.Components.JetpackComponent>(item))
            {
                jetpack ??= (item, tank);
                continue;
            }

            return (item, tank);
        }

        return null;
    }
}

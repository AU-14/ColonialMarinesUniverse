using System.Collections.Generic;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Medical.Treatment.FieldCare;

public sealed partial class CMUFieldTreatmentItemDiscoverySystem : EntitySystem
{
    private const float CraftingRange = 2f;

    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public List<EntityUid> GetAccessibleItems(EntityUid user)
    {
        var items = new List<EntityUid>();
        var seen = new HashSet<EntityUid>();

        void Add(EntityUid item)
        {
            if (!seen.Add(item))
                return;

            if (TryComp<StorageComponent>(item, out var storage))
            {
                foreach (var stored in storage.Container.ContainedEntities)
                    Add(stored);
            }

            items.Add(item);
        }

        foreach (var held in _hands.EnumerateHeld(user))
            Add(held);

        if (_inventory.TryGetContainerSlotEnumerator(user, out var slots))
        {
            while (slots.MoveNext(out var slot))
            {
                if (slot.ContainedEntity is { } contained)
                    Add(contained);
            }
        }

        var pos = _transform.GetMapCoordinates(user);
        foreach (var near in _lookup.GetEntitiesInRange(
                     pos,
                     CraftingRange,
                     LookupFlags.Contained | LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Approximate))
        {
            if (near == user)
                continue;

            if (!_interaction.InRangeUnobstructed(pos, near, CraftingRange) ||
                !_containers.IsInSameOrParentContainer(user, near))
            {
                continue;
            }

            Add(near);
        }

        return items;
    }
}

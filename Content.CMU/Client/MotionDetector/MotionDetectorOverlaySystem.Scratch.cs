using Content.Client.Hands.Systems;
using Content.Shared._RMC14.MotionDetector;
using Content.Shared.Inventory;
using Content.Shared.Storage;

namespace Content.Client._RMC14.MotionDetector;

public sealed partial class MotionDetectorOverlaySystem
{
    private readonly List<EntityUid> _cmuDetectorItems = new();

    private List<EntityUid> CMUGetDetectorItems(EntityUid player)
    {
        _cmuDetectorItems.Clear();
        foreach (var held in _entity.System<HandsSystem>().EnumerateHeld(player))
            _cmuDetectorItems.Add(held);

        if (_entity.System<InventorySystem>().TryGetContainerSlotEnumerator(player, out var inventory))
        {
            while (inventory.NextItem(out var item))
            {
                _cmuDetectorItems.Add(item);
                if (!_entity.HasComponent<PropagateDetectorsComponent>(item) ||
                    !_entity.TryGetComponent<StorageComponent>(item, out var storage))
                    continue;

                foreach (var stored in storage.StoredItems.Keys)
                    _cmuDetectorItems.Add(stored);
            }
        }

        return _cmuDetectorItems;
    }
}

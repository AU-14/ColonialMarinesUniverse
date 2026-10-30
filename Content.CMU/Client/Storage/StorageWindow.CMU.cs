using System.Numerics;
using Content.Client.Hands.Systems;
using Content.Client.Items.Systems;
using Content.Client.Storage.Systems;
using Content.Shared._RMC14.Storage;
using Content.Shared.Item;
using Content.Shared.Storage;
using Robust.Client.UserInterface;
using Robust.Shared.Collections;

namespace Content.Client.UserInterface.Systems.Storage.Controls;

public sealed partial class StorageWindow
{
    private bool CMUUpdatePlacementSnapshot(StorageComponent storage, Box2i bounds)
    {
        var contained = storage.Container.ContainedEntities;
        var unchanged = _lastUpdate.Grid.Equals(bounds) &&
                        _lastUpdate.Contained.Length == contained.Count &&
                        _lastUpdate.Stored.Count == storage.StoredItems.Count;
        for (var i = 0; unchanged && i < contained.Count; i++)
            unchanged = _lastUpdate.Contained[i] == contained[contained.Count - i - 1];

        if (unchanged)
        {
            foreach (var (uid, location) in storage.StoredItems)
            {
                if (!_lastUpdate.Stored.TryGetValue(uid, out var previous) || previous != location)
                {
                    unchanged = false;
                    break;
                }
            }
        }

        if (unchanged)
            return false;

        // Never alias StoredItems: network updates and local rearrangement mutate it in place.
        _lastUpdate.Grid = bounds;
        if (_lastUpdate.Contained.Length != contained.Count)
            _lastUpdate.Contained = new EntityUid[contained.Count];
        _contained.Clear();
        for (var i = contained.Count - 1; i >= 0; i--)
        {
            _lastUpdate.Contained[contained.Count - i - 1] = contained[i];
            _contained.Add(contained[i]);
        }

        _lastUpdate.Stored.Clear();
        foreach (var (uid, location) in storage.StoredItems)
            _lastUpdate.Stored.Add(uid, location);
        return true;
    }

    private void CMUUpdateSavedLocationHighlights(Entity<StorageComponent> storage)
    {
        if (storage.Comp.SavedLocations.Count == 0)
            return;

        var storageSystem = _entity.System<StorageSystem>();
        EntityUid current;
        if (_storageController.IsDragging && _storageController.DraggingGhost is { } dragging)
            current = dragging.Entity;
        else if (_entity.System<HandsSystem>().GetActiveHandEntity() is { } held &&
                 storageSystem.CanInsert(storage, held, out _, storageComp: storage.Comp, ignoreLocation: true) &&
                 _entity.System<RMCStorageSystem>().CanInsert((storage.Owner, storage.Comp), held, _player.LocalEntity, out _))
            current = held;
        else
            return;

        if (!_entity.TryGetComponent<ItemComponent>(current, out var item) ||
            !_entity.TryGetComponent<MetaDataComponent>(current, out var meta) ||
            !storage.Comp.SavedLocations.TryGetValue(meta.EntityName, out var locations) || locations.Count == 0)
            return;

        var items = _entity.System<ItemSystem>();
        var occupancy = storageSystem.CMUGetPlacementSnapshot(storage, current);
        float spot = 0;
        var marked = new ValueList<Control>();
        foreach (var location in locations)
        {
            var shape = items.GetAdjustedItemShape((storage.Owner, storage.Comp), (current, item), location);
            var bound = shape.GetBoundingBox();
            var free = occupancy.Fits(shape, location.Position);
            if (free)
                spot++;

            for (var y = bound.Bottom; y <= bound.Top; y++)
            {
                for (var x = bound.Left; x <= bound.Right; x++)
                {
                    if (!TryGetBackgroundCell(x, y, out var cell) || !shape.Contains(x, y) || marked.Contains(cell))
                        continue;

                    marked.Add(cell);
                    cell.ModulateSelfOverride = free
                        ? Color.FromHsv(new Vector4(0.18f, 1 / spot, 0.5f / spot + 0.5f, 1f))
                        : Color.FromHex("#2222CC");
                }
            }
        }
    }
}

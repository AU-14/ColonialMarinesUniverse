using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.Storage;
using Content.Shared.Storage;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Storage;

/// <summary>
/// Sets the empty, open, and closed layer visibility for a storage item.
/// </summary>
public sealed partial class CMStorageVisualizerSystem : VisualizerSystem<CMStorageVisualizerComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid,
        CMStorageVisualizerComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        // If empty
        if (!AppearanceSystem.TryGetData<int>(uid, StorageVisuals.StorageUsed, out var used, args.Component))
            return;

        // If item has holster and the holster isn't empty,
        //  don't count holster's contents towards fill status
        if (TryComp(uid, out StorageComponent? storage) &&
            TryComp(uid, out CMHolsterComponent? holster) &&
            storage.Container.ContainedEntities.Count == holster.Contents.Count)
            used = 0;

        // Lockable storage uses a custom presentation:
        // locked storage rests closed but still shows open while actively viewed,
        // while unlocked storage rests open when empty and closed when it contains items.
        if (TryComp(uid, out RMCIdLockableStorageComponent? lockable))
        {
            if (component.StorageEmpty != null)
                _sprite.LayerSetVisible((uid, args.Sprite), component.StorageEmpty, false);

            if (!AppearanceSystem.TryGetData<bool>(uid, StorageVisuals.Open, out var lockableOpen, args.Component))
                return;

            if (lockable.Locked)
            {
                if (component.StorageOpen != null)
                    _sprite.LayerSetVisible((uid, args.Sprite), component.StorageOpen, lockableOpen);
                if (component.StorageClosed != null)
                    _sprite.LayerSetVisible((uid, args.Sprite), component.StorageClosed, !lockableOpen);
                return;
            }

            var emptyUnlocked = used == 0;
            var showOpen = lockableOpen || emptyUnlocked;
            if (component.StorageOpen != null)
                _sprite.LayerSetVisible((uid, args.Sprite), component.StorageOpen, showOpen);
            if (component.StorageClosed != null)
                _sprite.LayerSetVisible((uid, args.Sprite), component.StorageClosed, !showOpen);
            return;
        }

        if (used == 0)
        {
            if (!component.ShowOpenClosedWhenEmpty)
            {
                if (component.StorageOpen != null)
                    _sprite.LayerSetVisible((uid, args.Sprite), component.StorageOpen, false);
                if (component.StorageClosed != null)
                    _sprite.LayerSetVisible((uid, args.Sprite), component.StorageClosed, false);
                if (component.StorageEmpty != null)
                    _sprite.LayerSetVisible((uid, args.Sprite), component.StorageEmpty, true);
                return;
            }

            if (component.StorageEmpty != null)
                _sprite.LayerSetVisible((uid, args.Sprite), component.StorageEmpty, false);
        }
        else
        {
            if (component.StorageEmpty != null)
                _sprite.LayerSetVisible((uid, args.Sprite), component.StorageEmpty, false);
        }

        // Open or closed
        if (!AppearanceSystem.TryGetData<bool>(uid, StorageVisuals.Open, out var open, args.Component))
            return;

        if (open)
        {
            if (component.StorageOpen != null)
                _sprite.LayerSetVisible((uid, args.Sprite), component.StorageOpen, true);
            if (component.StorageClosed != null)
                _sprite.LayerSetVisible((uid, args.Sprite), component.StorageClosed, false);
        }
        else
        {
            if (component.StorageOpen != null)
                _sprite.LayerSetVisible((uid, args.Sprite), component.StorageOpen, false);
            if (component.StorageClosed != null)
                _sprite.LayerSetVisible((uid, args.Sprite), component.StorageClosed, true);
        }
    }
}

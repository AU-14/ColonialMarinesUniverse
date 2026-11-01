using Content.Shared.ActionBlocker;
using Content.Shared.Interaction;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Inventory;

/// <summary>
/// Asks to open a storage item the player can reach: on the ground, in hand, or inside a storage they have open.
/// Sent by the open storage keybind.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUOpenStorageRequestEvent(NetEntity storage) : EntityEventArgs
{
    public readonly NetEntity Storage = storage;
}

public sealed partial class CMUOpenStorageSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        SubscribeAllEvent<CMUOpenStorageRequestEvent>(OnOpenStorageRequest);
    }

    private void OnOpenStorageRequest(CMUOpenStorageRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { Valid: true } user ||
            !TryGetEntity(ev.Storage, out var target) ||
            target is not { } storage ||
            !TryComp<StorageComponent>(storage, out var storageComp) ||
            !_actionBlocker.CanInteract(user, storage) ||
            !_interaction.InRangeAndAccessible(user, storage))
        {
            return;
        }

        _storage.OpenStorageUI(storage, user, storageComp, silent: false);
    }
}

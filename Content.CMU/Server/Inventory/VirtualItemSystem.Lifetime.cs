using Content.Shared.Inventory.VirtualItem;

namespace Content.Server.Inventory;

public sealed partial class VirtualItemSystem
{
    private readonly Dictionary<EntityUid, Dictionary<EntityUid, EntityUid>> _itemsByBlocker = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VirtualItemComponent, ComponentShutdown>(OnVirtualItemShutdown);
        EntityManager.EntityDeleted += OnBlockingEntityDeleted;
    }

    public override void Shutdown()
    {
        EntityManager.EntityDeleted -= OnBlockingEntityDeleted;
        _itemsByBlocker.Clear();
        base.Shutdown();
    }

    protected override void CMUOnVirtualItemCreated(Entity<VirtualItemComponent> item, EntityUid user)
    {
        if (!_itemsByBlocker.TryGetValue(item.Comp.BlockingEntity, out var items))
            _itemsByBlocker.Add(item.Comp.BlockingEntity, items = new Dictionary<EntityUid, EntityUid>());
        items[item] = user;
    }

    private void OnVirtualItemShutdown(Entity<VirtualItemComponent> item, ref ComponentShutdown args)
    {
        if (!_itemsByBlocker.TryGetValue(item.Comp.BlockingEntity, out var items))
            return;

        items.Remove(item);
        if (items.Count == 0)
            _itemsByBlocker.Remove(item.Comp.BlockingEntity);
    }

    private void OnBlockingEntityDeleted(Entity<MetaDataComponent> blocker)
    {
        if (!_itemsByBlocker.Remove(blocker, out var items))
            return;

        foreach (var (uid, user) in items)
        {
            if (TerminatingOrDeleted(uid) || !TryComp<VirtualItemComponent>(uid, out var item))
                continue;

            if (!TerminatingOrDeleted(user))
                DeleteVirtualItem((uid, item), user);
            // Queueing alone leaves the deleted blocker in this tick's network state.
            Del(uid);
        }
    }
}

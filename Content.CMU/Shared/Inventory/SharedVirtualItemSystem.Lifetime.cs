namespace Content.Shared.Inventory.VirtualItem;

public abstract partial class SharedVirtualItemSystem
{
    protected virtual void CMUOnVirtualItemCreated(Entity<VirtualItemComponent> item, EntityUid user)
    {
    }
}

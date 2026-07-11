using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Inventory;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCMInventorySystem))]
public sealed partial class RMCPickupDroppedItemsComponent : Component
{
    [DataField]
    public List<EntityUid> DroppedItems = new();
}

using Robust.Shared.GameStates;
using Content.Shared.Inventory;

namespace Content.Shared._RMC14.Clothing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OuterClothingAccessoryHolderComponent : Component
{
    [DataField, AutoNetworkedField]
    public SlotFlags Slot = SlotFlags.OUTERCLOTHING;
}
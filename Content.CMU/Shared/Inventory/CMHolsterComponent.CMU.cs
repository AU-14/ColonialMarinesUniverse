using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Inventory;

public sealed partial class CMHolsterComponent
{
    /// <summary>Covering outerwear can hide the contents of this holster on examine.</summary>
    [DataField, AutoNetworkedField]
    public bool Concealable;
}

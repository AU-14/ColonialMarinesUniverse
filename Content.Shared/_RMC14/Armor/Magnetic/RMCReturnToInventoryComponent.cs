using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Armor.Magnetic;

[RegisterComponent, NetworkedComponent]
[Access(typeof(RMCMagneticSystem))]
public sealed partial class RMCReturnToInventoryComponent : Component
{
    [DataField]
    public EntityUid User;

    [DataField]
    public EntityUid Magnetizer;

    [DataField]
    public bool Returned;

    [DataField]
    public EntityUid? ReceivingItem;

    [DataField]
    public string ReceivingContainer;
}

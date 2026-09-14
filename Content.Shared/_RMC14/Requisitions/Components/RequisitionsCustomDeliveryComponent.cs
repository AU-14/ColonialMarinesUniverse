using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes; // CMU14

namespace Content.Shared._RMC14.Requisitions.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedRequisitionsSystem))]
public sealed partial class RequisitionsCustomDeliveryComponent : Component
{
    [DataField] // CMU14
    public string Faction = string.Empty;
}

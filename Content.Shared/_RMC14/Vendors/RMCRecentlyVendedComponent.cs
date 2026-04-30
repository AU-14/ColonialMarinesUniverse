using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Vendors;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
[Access(typeof(SharedCMAutomatedVendorSystem))]
public sealed partial class RMCRecentlyVendedComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> PreventCollide = new();
}

using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Vendors;

[RegisterComponent, NetworkedComponent, UnsavedComponent]
[Access(typeof(SharedCMAutomatedVendorSystem))]
public sealed partial class RMCRecentlyVendedComponent : Component;

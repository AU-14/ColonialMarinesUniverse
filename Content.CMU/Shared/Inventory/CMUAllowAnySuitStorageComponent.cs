using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Inventory;

/// <summary>
/// Fits in the suit storage slot of any suit that has one, ignoring the suit's whitelist.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUAllowAnySuitStorageComponent : Component;

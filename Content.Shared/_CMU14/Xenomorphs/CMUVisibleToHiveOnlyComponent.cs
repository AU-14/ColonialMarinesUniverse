using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Xenomorphs;

/// <summary>
/// Marker component added to the Overmind while in incorporeal "eye" form.
/// A client-side system hides the entity's base sprite layer from anyone
/// who isn't a member of the same hive (mirroring RMCVisibleToGhostsOnlyComponent).
/// Removed when the Overmind manifests into its physical form.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVisibleToHiveOnlyComponent : Component;
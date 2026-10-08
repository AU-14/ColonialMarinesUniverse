using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.ThreeD;

/// <summary>Opt-in map support for the player first-person renderer and its bounded view subscription.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMU3DMapComponent : Component;

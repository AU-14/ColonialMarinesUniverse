using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Underground;

/// <summary>
/// Invisible anchored marker placed on underground tiles that have been cleared (mined).
/// Prevents rocks from re-spawning on this tile.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedUndergroundMapSystem))]
public sealed partial class UndergroundDugMarkerComponent : Component
{
}

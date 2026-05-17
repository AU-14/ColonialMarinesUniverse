using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Underground.Markers;

/// <summary>
/// Placed on the surface map by mappers. When the underground is created,
/// a paired tunnel entrance is spawned at this location (surface + underground).
/// The tile is also pre-dug.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class UndergroundEntranceMarkerComponent : Component;
